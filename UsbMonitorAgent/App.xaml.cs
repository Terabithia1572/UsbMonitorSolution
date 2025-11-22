using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using UsbMonitorAgent.Helpers;
using UsbMonitorAgent.UI.Windows;
using WinForms = System.Windows.Forms;

namespace UsbMonitorAgent
{
    public partial class App : Application
    {
        private WinForms.NotifyIcon _trayIcon;
        private MainWindow _window;
        private static Mutex? _agentMutex;

        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show("HATA: " + e.Exception.Message);
                e.Handled = true;
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                bool createdNew = false;
                _agentMutex = new Mutex(true, @"Global\UsbMonitorAgentRunning", out createdNew);
            }
            catch { } // Users grubu hatası yutulur

            _window = new MainWindow();
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string iconFolder = Path.Combine(basePath, "Assets", "Icons");

            string trayIconPath = Path.Combine(iconFolder, "app.ico");
            Icon trayIco = File.Exists(trayIconPath) ? new Icon(trayIconPath) : SystemIcons.Application;

            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = trayIco,
                Visible = true,
                Text = "USB Monitor Agent"
            };

            var menu = new WinForms.ContextMenuStrip();

            // 1. LOGLARI GÖSTER
            menu.Items.Add("Logları Göster", LoadIconPhysical(iconFolder, "icon_logs.png"), (s, a) =>
            {
                if (!SecurityHelper.EnsureAuthenticated()) return;
                if (_window == null || !_window.IsLoaded) _window = new MainWindow();
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            });

            // 2. SERVİS DURUMU
            menu.Items.Add("Servis Durumu", LoadIconPhysical(iconFolder, "icon_service.png"), async (s, a) =>
            {
                bool ok = await PipeClientService.CheckServiceAsync();
                MessageBox.Show(ok ? "Servis çalışıyor." : "Servise ulaşılamadı.", "Durum");
            });

            // 3. USB BİLGİSİ
            menu.Items.Add("USB Bilgisini Göster", LoadIconPhysical(iconFolder, "icon_usb_info.png"), (s, a) =>
            {
                if (!SecurityHelper.EnsureAuthenticated()) return;
                if (UsbWatcherState.Devices.Count == 0) { MessageBox.Show("Takılı USB yok."); return; }
                var first = UsbWatcherState.Devices.Values.FirstOrDefault();
                if (first != null) new UsbInfoWindow(first.DriveRoot).ShowDialog();
            });

            // 4. TAKILI AYGITLAR
            menu.Items.Add("Takılı USB Aygıtları", LoadIconPhysical(iconFolder, "icon_usb_list.png"), (s, a) =>
            {
                if (!SecurityHelper.EnsureAuthenticated()) return;
                new UsbDeviceListWindow().Show();
            });

            // 🔥 5. ADMIN AYARLARI (Şifre Değiştirme Buradan Yapılacak)
            menu.Items.Add("Admin Ayarları", LoadIconPhysical(iconFolder, "icon_settings.png"), (s, a) =>
            {
                // Giriş yapmadan ayar değiştiremesin
                if (!SecurityHelper.EnsureAuthenticated()) return;

                // Ayarlar penceresini aç
                new UserSettingsWindow().ShowDialog();
            });
            // ... Diğer menü öğeleri ...

            // 🔥🔥 YENİ: SERVİS YÖNETİMİ 🔥🔥
            menu.Items.Add(
                "Servis Yönetimi",
                LoadIconPhysical(iconFolder, "icon_service_managee.png"), // İkon adını kendine göre ayarla
                (s, a) =>
                {
                    // 1. Güvenlik: Şifre Sor
                    if (!SecurityHelper.EnsureAuthenticated()) return;

                    // 2. Şifre doğruysa Yönetim Penceresini Aç
                    new ServiceControlWindow().ShowDialog();
                });

            // ... Çıkış vb. ...

            // 6. ÇIKIŞ
            menu.Items.Add("Çıkış", LoadIconPhysical(iconFolder, "icon_exit.png"), (s, a) =>
            {
                if (!SecurityHelper.EnsureAuthenticated()) return;
                _trayIcon.Visible = false;
                try { _agentMutex?.ReleaseMutex(); _agentMutex?.Dispose(); } catch { }
                Shutdown();
            });

            _trayIcon.ContextMenuStrip = menu;

            var watcher = new FileWatcherService();
            watcher.StartWatching();
        }

        private System.Drawing.Image LoadIconPhysical(string folder, string file)
        {
            try
            {
                string path = Path.Combine(folder, file);
                if (!File.Exists(path)) return null;
                return System.Drawing.Image.FromFile(path);
            }
            catch { return null; }
        }
    }
}