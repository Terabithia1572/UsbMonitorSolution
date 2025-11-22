using System;
using System.Collections.Concurrent; // Thread-safe dictionary için
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UsbMonitorAgent.UI.Windows;

namespace UsbMonitorAgent
{
    public class FileWatcherService
    {
        private readonly Dictionary<string, FileSystemWatcher> _activeWatchers = new();
        private ManagementEventWatcher? _volumeWatcher;

        // 🔥 YENİ SİSTEM: Debounce Tokenları (Spam engellemek ama Overwrite'a izin vermek için)
        // ConcurrentDictionary kullanarak thread çakışmalarını engelliyoruz.
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();

        public void StartWatching()
        {
            Console.WriteLine("[AGENT] USB izleme başlatıldı...");
            StartUsbVolumeWatcher();
            InitializeExistingUsbDrives();
        }

        // ... (InitializeExistingUsbDrives ve StartUsbVolumeWatcher AYNI) ...
        private void InitializeExistingUsbDrives()
        {
            foreach (var di in DriveInfo.GetDrives())
            {
                try
                {
                    if (di.DriveType == DriveType.Removable && di.IsReady)
                        OnUsbInserted(di.RootDirectory.FullName);
                }
                catch { }
            }
        }

        private void StartUsbVolumeWatcher()
        {
            string query = "SELECT * FROM Win32_VolumeChangeEvent";
            _volumeWatcher = new ManagementEventWatcher(new WqlEventQuery(query));
            _volumeWatcher.EventArrived += (s, e) =>
            {
                try
                {
                    int type = Convert.ToInt32(e.NewEvent["EventType"]);
                    string? root = e.NewEvent["DriveName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(root)) return;
                    if (type == 2) OnUsbInserted(root);
                    else if (type == 3) OnUsbRemoved(root);
                }
                catch { }
            };
            _volumeWatcher.Start();
        }

        // ... (GetClipboardSourceFile, DetectRealSource, CheckClipboardForName, BuildFinalPath AYNI) ...
        private string GetClipboardSourceFile(string targetFileName)
        {
            string result = "";
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsFileDropList())
                        {
                            var files = System.Windows.Clipboard.GetFileDropList();
                            foreach (string path in files)
                            {
                                if (Path.GetFileName(path).Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
                                {
                                    result = path;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            catch { }
            return result;
        }

        private string DetectRealSource(string targetFullPath, string excludeDriveRoot)
        {
            string cleanExclude = excludeDriveRoot.TrimEnd('\\');
            if (!cleanExclude.EndsWith(":")) cleanExclude += ":";

            try
            {
                string currentPathToCheck = targetFullPath;
                string relativePath = "";

                for (int i = 0; i < 10; i++)
                {
                    if (string.IsNullOrEmpty(currentPathToCheck)) break;
                    string root = Path.GetPathRoot(currentPathToCheck);
                    if (currentPathToCheck.Equals(root, StringComparison.OrdinalIgnoreCase)) break;

                    string searchName = Path.GetFileName(currentPathToCheck);
                    string clipboardMatch = CheckClipboardForName(searchName);
                    if (!string.IsNullOrEmpty(clipboardMatch))
                        return BuildFinalPath(clipboardMatch, relativePath);

                    string explorerMatch = "";
                    var t = new Thread(() =>
                    {
                        explorerMatch = ExplorerHelper.FindPathInExplorerOrDesktop(searchName, cleanExclude);
                    });
                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();
                    t.Join();

                    if (!string.IsNullOrEmpty(explorerMatch))
                        return BuildFinalPath(explorerMatch, relativePath);

                    if (string.IsNullOrEmpty(relativePath)) relativePath = searchName;
                    else relativePath = Path.Combine(searchName, relativePath);

                    currentPathToCheck = Path.GetDirectoryName(currentPathToCheck);
                }
            }
            catch { }
            return "";
        }

        private string CheckClipboardForName(string name)
        {
            string found = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsFileDropList())
                    {
                        var list = System.Windows.Clipboard.GetFileDropList();
                        foreach (string path in list)
                        {
                            string clipName = Path.GetFileName(path.TrimEnd('\\'));
                            if (clipName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                found = path; break;
                            }
                        }
                    }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            return found;
        }

        private string BuildFinalPath(string rootSource, string relativePart)
        {
            if (string.IsNullOrEmpty(relativePart)) return rootSource;
            string fullPath = Path.Combine(rootSource, relativePart);
            if (File.Exists(fullPath)) return fullPath;
            return fullPath;
        }

        // -------------------------------------------------------------
        // 🔥🔥 MERKEZİ İŞLEYİCİ (DEBOUNCE EKLENDİ) 🔥🔥
        // -------------------------------------------------------------
        private void OnFileDetected(object sender, FileSystemEventArgs e, string label, string identity, string driveRoot)
        {
            string fullPath = e.FullPath;

            // 1. Varsa eski sayacı iptal et (Çünkü dosya hala yazılıyor/değişiyor)
            if (_debounceTokens.TryGetValue(fullPath, out var existingTokenSource))
            {
                existingTokenSource.Cancel();
                existingTokenSource.Dispose();
            }

            // 2. Yeni sayaç başlat
            var newTokenSource = new CancellationTokenSource();
            _debounceTokens[fullPath] = newTokenSource;

            Task.Run(async () =>
            {
                try
                {
                    // 🔥 1 SANİYE BEKLE (Sessizlik süresi)
                    // Eğer bu süre içinde dosyaya tekrar yazılırsa, bu görev iptal olur ve log atmaz.
                    // Sadece SON işlem log atar.
                    await Task.Delay(1000, newTokenSource.Token);

                    // Eğer buraya geldiysek, 1 saniyedir dosyaya dokunulmadı demektir.
                    // Artık loglayabiliriz.

                    // Token listesinden çıkar
                    _debounceTokens.TryRemove(fullPath, out _);

                    var fi = new FileInfo(fullPath);
                    if (!fi.Exists) return;

                    // Geçici dosyaları atla
                    if (fi.Name.StartsWith("~$") || fi.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)) return;

                    if (!WaitUntilFileReady(fi.FullName)) return;

                    string source = DetectRealSource(fi.FullName, driveRoot);
                    if (string.IsNullOrWhiteSpace(source)) source = "(Kaynak Tespit Edilemedi)";

                    string hash = await ComputeShaAsync(fi.FullName);
                    fi.Refresh();

                    var log = new UsbLogModel
                    {
                        Username = Environment.UserName,
                        FileName = fi.Name,
                        SourcePath = source,
                        DestPath = fi.FullName,
                        DriveLabel = label,
                        DeviceIdentity = identity,
                        FileSize = fi.Length,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        FileHash = hash
                    };

                    await PipeClientService.SendLogAsync(log);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { new NotificationWindow($"Kayıt Alındı:\n{fi.Name}").Show(); } catch { }
                    });
                }
                catch (TaskCanceledException)
                {
                    // Bu task iptal edildi, demek ki yeni bir event geldi. Sessizce çık.
                }
                catch { }
            });
        }

        // --- WAIT & HASH (AYNI) ---
        private bool WaitUntilFileReady(string path)
        {
            const int maxAttempts = 40;
            long lastSize = -1;
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var fi = new FileInfo(path);
                    if (!fi.Exists) { Thread.Sleep(300); continue; }
                    if (fi.Length > 0 && fi.Length == lastSize) return true;
                    lastSize = fi.Length;
                }
                catch { }
                Thread.Sleep(500);
            }
            return false;
        }

        private async Task<string> ComputeShaAsync(string path)
        {
            for (int i = 0; i < 40; i++)
            {
                try
                {
                    using var sha = SHA256.Create();
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var hash = await Task.Run(() => sha.ComputeHash(fs));
                    return BitConverter.ToString(hash).Replace("-", "");
                }
                catch { await Task.Delay(500); }
            }
            return "(Hesaplanamadı)";
        }

        // --- INSERT ---
        private void OnUsbInserted(string driveRoot)
        {
            try
            {
                if (!Directory.Exists(driveRoot)) return;
                if (_activeWatchers.ContainsKey(driveRoot)) return;

                var di = new DriveInfo(driveRoot);
                if (di.DriveType != DriveType.Removable) return;

                string label = di.VolumeLabel;
                string cleanRoot = driveRoot.TrimEnd('\\');
                if (cleanRoot.EndsWith(":")) cleanRoot = cleanRoot.Substring(0, cleanRoot.Length - 1);

                string identity = UsbInfoHelper.GetUsbIdentity(cleanRoot);
                UsbWatcherState.AddOrUpdate(driveRoot, label, identity);

                var watcher = new FileSystemWatcher(driveRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size |
                                   NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                // 🔥 HEM OLUŞTURMA HEM DEĞİŞTİRME AYNI YERE GİDİYOR
                // Debounce sistemi sayesinde çift log oluşmaz.
                watcher.Created += (s, e) => OnFileDetected(s, e, label, identity, driveRoot);
                watcher.Changed += (s, e) => OnFileDetected(s, e, label, identity, driveRoot);
                watcher.Renamed += (s, e) => OnFileDetected(s, e, label, identity, driveRoot);

                watcher.EnableRaisingEvents = true;
                _activeWatchers[driveRoot] = watcher;
            }
            catch { }
        }

        private void OnUsbRemoved(string driveRoot)
        {
            UsbWatcherState.Remove(driveRoot);
            if (_activeWatchers.TryGetValue(driveRoot, out var w))
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
                _activeWatchers.Remove(driveRoot);
            }
            _debounceTokens.Clear(); // Temizlik
        }
    }
}