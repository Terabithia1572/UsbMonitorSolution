using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;   //  BUNU EKLE

namespace UsbMonitorService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService()                          //  Artýk hata vermeyecek
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<UsbLogRepository>();
                services.AddHostedService<ServiceWorker>();
            });

    }
}
