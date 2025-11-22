using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UsbMonitorService.Core
{
    public class FileWatcherService
    {
        private readonly Dictionary<string, FileSystemWatcher> _activeWatchers = new();

        // 🔥 DEĞİŞİKLİK 1: HashSet yerine zaman damgalı Dictionary
        // Dosya Yolu -> Son İşlenme Zamanı
        private readonly Dictionary<string, DateTime> _processedFiles = new();

        private ManagementEventWatcher? _volumeWatcher;
        private readonly UsbLogRepository _repo;

        public FileWatcherService(UsbLogRepository repo)
        {
            _repo = repo;
        }

        // -------------------------------------------------------------
        // SERVICE START
        // -------------------------------------------------------------
        public void StartWatching()
        {
            Console.WriteLine("[SERVICE] USB izleme başlatıldı...");
            StartUsbVolumeWatcher();
            InitializeExistingUsbDrives();
        }

        // -------------------------------------------------------------
        // AGENT mutex kontrolü
        // -------------------------------------------------------------
        private bool IsAgentRunning()
        {
            try
            {
                using var m = Mutex.OpenExisting(@"Global\UsbMonitorAgentRunning");
                return true;
            }
            catch
            {
                return false;
            }
        }

        // -------------------------------------------------------------
        // Giriş yapan kullanıcı (SYSTEM fallback korunuyor)
        // -------------------------------------------------------------
        private string GetInteractiveUsernameSafe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI");

                var raw = key?.GetValue("LastLoggedOnUser") as string;
                if (string.IsNullOrWhiteSpace(raw))
                    return Environment.UserName;

                var parts = raw.Split('\\');
                return parts.Length == 2 ? parts[1] : raw;
            }
            catch
            {
                return Environment.UserName;
            }
        }

        // -------------------------------------------------------------
        // Mevcut USB'ler için watcher başlat
        // -------------------------------------------------------------
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

        // -------------------------------------------------------------
        // USB Insert/Remove EVENT
        // -------------------------------------------------------------
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
                catch (Exception ex)
                {
                    Console.WriteLine("[SERVICE] Volume event error: " + ex.Message);
                }
            };

            _volumeWatcher.Start();
        }

        // -------------------------------------------------------------
        // HASH – büyük dosya için güvenli okuma
        // -------------------------------------------------------------
        private async Task<string> ComputeShaAsync(string path)
        {
            for (int i = 0; i < 40; i++) // up to ~20 seconds
            {
                try
                {
                    using var sha = SHA256.Create();

                    using var fs = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete // büyük dosya fix
                    );

                    var hash = await Task.Run(() => sha.ComputeHash(fs));
                    return BitConverter.ToString(hash).Replace("-", "");
                }
                catch
                {
                    await Task.Delay(500);
                }
            }

            return "(Hesaplanamadı)";
        }

        // -------------------------------------------------------------
        // Dosya yazımının bitmesini bekleme
        // -------------------------------------------------------------
        private bool WaitUntilFileIsReady(string path)
        {
            const int maxAttempts = 40;
            long lastSize = -1;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var fi = new FileInfo(path);

                    if (!fi.Exists)
                    {
                        Thread.Sleep(300);
                        continue;
                    }

                    if (fi.Length > 0 && fi.Length == lastSize)
                        return true;

                    lastSize = fi.Length;
                }
                catch
                {
                    // fail silently
                }

                Thread.Sleep(500);
            }

            return false;
        }

        // -------------------------------------------------------------
        // VID / PID / SN alma
        // -------------------------------------------------------------
        private string GetUsbIdentityFromDrive(string driveRoot)
        {
            try
            {
                string normalized = driveRoot.TrimEnd('\\'); // "F:\" → "F:"
                string id = UsbInfoHelper.GetUsbIdentityWithRetry(normalized);

                if (string.IsNullOrWhiteSpace(id) ||
                    id.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return "(Service)";
                }

                return id;
            }
            catch
            {
                return "(Service)";
            }
        }

        // -------------------------------------------------------------
        // USB TAKILDI – WATCHER BAŞLAT
        // -------------------------------------------------------------
        private void OnUsbInserted(string driveRoot)
        {
            try
            {
                if (!Directory.Exists(driveRoot)) return;
                var di = new DriveInfo(driveRoot);
                if (di.DriveType != DriveType.Removable) return;
                if (_activeWatchers.ContainsKey(driveRoot)) return;

                string label = "";
                try { label = di.VolumeLabel; } catch { }

                string identity = GetUsbIdentityFromDrive(driveRoot);

                var watcher = new FileSystemWatcher(driveRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.Size |
                                   NotifyFilters.LastWrite
                };

                watcher.Created += (s, e) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (IsAgentRunning()) return; // Agent açıksa Service karışmasın

                            var fi = new FileInfo(e.FullPath);
                            if (!fi.Exists) return;

                            if (!WaitUntilFileIsReady(fi.FullName))
                            {
                                Console.WriteLine("[SERVICE] Dosya hazır olmadı → " + fi.FullName);
                                return;
                            }

                            // 🔥 DEĞİŞİKLİK 2: ZAMAN KONTROLLÜ OVERWRITE İZNİ
                            lock (_processedFiles)
                            {
                                if (_processedFiles.TryGetValue(fi.FullName, out DateTime lastTime))
                                {
                                    // Eğer 5 saniye içinde tekrar geldiyse (Duplicate Event) engelle
                                    if ((DateTime.UtcNow - lastTime).TotalSeconds < 5)
                                        return;
                                }

                                _processedFiles[fi.FullName] = DateTime.UtcNow;

                                // Hafıza şişmesin diye temizlik
                                if (_processedFiles.Count > 5000) _processedFiles.Clear();
                            }

                            string hash = await ComputeShaAsync(fi.FullName);

                            // 🔥 DEĞİŞİKLİK 3: 0 BYTE SORUNU ÇÖZÜMÜ
                            fi.Refresh();

                            var log = new UsbLogEntry
                            {
                                Username = GetInteractiveUsernameSafe(),
                                FileName = fi.Name,
                                SourcePath = "(Bilinmiyor - Agent Kapalı)", // Servis clipboard/explorer bilemez
                                DestPath = fi.FullName,
                                DriveLabel = label,
                                DeviceIdentity = identity,
                                DriveSerial = "",
                                FileSize = fi.Length, // Artık güncel boyut
                                TimestampUtc = DateTime.UtcNow.ToString("o"),
                                FileHash = hash
                            };

                            _repo.InsertLog(log);
                            Console.WriteLine("[SERVICE] Yeni dosya loglandı → " + fi.FullName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[SERVICE] File error: " + ex.Message);
                        }
                    });
                };

                watcher.EnableRaisingEvents = true;
                _activeWatchers[driveRoot] = watcher;

                Console.WriteLine("[SERVICE] Watcher başlatıldı → " + driveRoot);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SERVICE] Insert error: " + ex.Message);
            }
        }

        // -------------------------------------------------------------
        // USB ÇIKTI – Watcher sonlandır
        // -------------------------------------------------------------
        private void OnUsbRemoved(string driveRoot)
        {
            Console.WriteLine("[SERVICE] USB çıkarıldı: " + driveRoot);

            if (_activeWatchers.TryGetValue(driveRoot, out var w))
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
                _activeWatchers.Remove(driveRoot);
            }

            lock (_processedFiles)
            {
                _processedFiles.Clear();
            }
        }
    }
}