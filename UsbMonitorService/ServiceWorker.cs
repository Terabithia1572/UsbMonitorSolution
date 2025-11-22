using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.AccessControl; // 🔥 GEREKLİ
using System.Security.Principal;     // 🔥 GEREKLİ
using UsbMonitorService.Core;
using System;

namespace UsbMonitorService
{
    public class ServiceWorker : BackgroundService
    {
        private readonly ILogger<ServiceWorker> _logger;
        private readonly UsbLogRepository _repo;
        private readonly FileWatcherService _watcher;
        private const string PipeName = "UsbMonitorPipe";

        public ServiceWorker(ILogger<ServiceWorker> logger, UsbLogRepository repo)
        {
            _logger = logger;
            _repo = repo;
            _watcher = new FileWatcherService(_repo);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Log klasörünü oluştur
            Directory.CreateDirectory(@"C:\ProgramData\UsbMonitor");
            _logger.LogInformation("UsbMonitorService başlatılıyor...");

            // 1) USB İZLEME BAŞLASIN
            Task.Run(() => _watcher.StartWatching(), stoppingToken);

            // 2) PIPE DİNLEME BAŞLASIN
            await ListenPipeAsync(stoppingToken);
        }

        private async Task ListenPipeAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Named Pipe dinleme döngüsü başladı.");
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            while (!stoppingToken.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    // 🔥 GÜVENLİK AYARLARI (ACL)
                    // Hem SYSTEM (Servis) hem de Authenticated Users (Standart Kullanıcı) erişebilsin.
                    var pipeSecurity = new PipeSecurity();

                    // 1. Kural: Authenticated Users -> Okuma/Yazma
                    var usersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                    pipeSecurity.AddAccessRule(new PipeAccessRule(usersSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                    // 2. Kural: SYSTEM -> Tam Yetki
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                    pipeSecurity.AddAccessRule(new PipeAccessRule(systemSid, PipeAccessRights.FullControl, AccessControlType.Allow));

                    // 🔥 PIPE OLUŞTURMA
                    // .NET 6/7/8 uyumlu ACL Create metodu
                    server = NamedPipeServerStreamAcl.Create(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        0, // Default in buffer
                        0, // Default out buffer
                        pipeSecurity // İzinler buraya
                    );

                    // Bağlantı bekleniyor...
                    await server.WaitForConnectionAsync(stoppingToken);

                    // Veri Okuma
                    var buffer = new byte[8192];
                    var sb = new StringBuilder();

                    do
                    {
                        int read = await server.ReadAsync(buffer, 0, buffer.Length, stoppingToken);
                        if (read <= 0) break;
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    }
                    while (!server.IsMessageComplete);

                    string json = sb.ToString();

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var log = JsonSerializer.Deserialize<UsbLogEntry>(json, jsonOptions);
                        if (log != null)
                        {
                            _repo.InsertLog(log);
                            _logger.LogInformation("Pipe üzerinden log alındı: {file}", log.FileName);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break; // Servis durduruluyor
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Pipe hatası! Servis çalışmaya devam edecek.");
                    // Hata olsa bile döngü kırılmasın, biraz bekle tekrar dene
                    await Task.Delay(2000, stoppingToken);
                }
                finally
                {
                    // Her bağlantıdan sonra veya hatada temizle
                    if (server != null)
                    {
                        if (server.IsConnected) server.Disconnect();
                        server.Dispose();
                    }
                }
            }
        }
    }
}