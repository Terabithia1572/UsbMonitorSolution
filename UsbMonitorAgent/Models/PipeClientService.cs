using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Security.Principal; // TokenImpersonationLevel için

namespace UsbMonitorAgent
{
    public static class PipeClientService
    {
        private const string PipeName = "UsbMonitorPipe";

        public static async Task SendLogAsync(UsbLogModel log)
        {
            try
            {
                // Impersonation ile bağlan (Servis kimin gönderdiğini anlasın)
                using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Impersonation);

                // 🔥 Timeout süresini 5000ms (5 saniye) yaptık. 
                // Servis yeniden başlıyorsa hata vermesin.
                await client.ConnectAsync(5000);

                using var sw = new StreamWriter(client);
                var payload = new
                {
                    Username = log.Username ?? "",
                    FileName = log.FileName ?? "",
                    SourcePath = log.SourcePath ?? "",
                    DestPath = log.DestPath ?? "",
                    DriveLabel = log.DriveLabel ?? "",
                    DeviceIdentity = log.DeviceIdentity ?? "",
                    DriveSerial = "",
                    FileSize = log.FileSize,
                    TimestampUtc = log.TimestampUtc,
                    FileHash = log.FileHash ?? ""
                };

                string json = JsonSerializer.Serialize(payload);
                await sw.WriteAsync(json);
                await sw.FlushAsync();
            }
            catch
            {
                // Log gidemedi, Agent çökmeyecek.
            }
        }

        public static async Task<bool> CheckServiceAsync()
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Impersonation);

                await client.ConnectAsync(1000); // Kontrol için 1 sn yeter
                return true;
            }
            catch { return false; }
        }
    }
}