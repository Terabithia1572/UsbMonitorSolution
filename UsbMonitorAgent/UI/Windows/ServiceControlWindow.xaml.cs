using System;
using System.ServiceProcess; // Referans eklemeyi unutma!
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace UsbMonitorAgent.UI.Windows
{
    public partial class ServiceControlWindow : Window
    {
        private const string ServiceName = "UsbMonitorService";

        public ServiceControlWindow()
        {
            InitializeComponent();
            // Pencereyi sürükleme özelliği
            this.MouseLeftButtonDown += (s, e) => DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Servis durumunu kontrol et ve UI'ı güncelle
        private void RefreshStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                var status = sc.Status;

                if (status == ServiceControllerStatus.Running)
                {
                    StatusText.Text = "Servis ÇALIŞIYOR";
                    StatusLight.Fill = new SolidColorBrush(Colors.Green);

                    BtnStart.IsEnabled = false;
                    BtnStart.Opacity = 0.5;

                    BtnStop.IsEnabled = true;
                    BtnStop.Opacity = 1;

                    BtnRestart.IsEnabled = true;
                    BtnRestart.Opacity = 1;
                }
                else if (status == ServiceControllerStatus.Stopped)
                {
                    StatusText.Text = "Servis DURDURULDU";
                    StatusLight.Fill = new SolidColorBrush(Colors.Red);

                    BtnStart.IsEnabled = true;
                    BtnStart.Opacity = 1;

                    BtnStop.IsEnabled = false;
                    BtnStop.Opacity = 0.5;

                    BtnRestart.IsEnabled = true; // Durmuşsa da yeniden başlat (Start gibi çalışır)
                    BtnRestart.Opacity = 1;
                }
                else
                {
                    StatusText.Text = "İşlem Yapılıyor...";
                    StatusLight.Fill = new SolidColorBrush(Colors.Orange);
                    DisableAllButtons();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Servis Bulunamadı veya Erişim Yok";
                StatusLight.Fill = new SolidColorBrush(Colors.Gray);
                DisableAllButtons();
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableAllButtons()
        {
            BtnStart.IsEnabled = false; BtnStart.Opacity = 0.5;
            BtnStop.IsEnabled = false; BtnStop.Opacity = 0.5;
            BtnRestart.IsEnabled = false; BtnRestart.Opacity = 0.5;
        }

        // --- BUTON AKSİYONLARI ---

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            await RunServiceOperation(async () =>
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
            });
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            await RunServiceOperation(async () =>
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
            });
        }

        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            await RunServiceOperation(async () =>
            {
                using var sc = new ServiceController(ServiceName);

                // Önce durdur (Eğer çalışıyorsa)
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }

                // Sonra başlat
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            });
        }

        // Ortak İşlem Yürütücü (Hata Yakalama ve UI Güncelleme)
        private async Task RunServiceOperation(Func<Task> operation)
        {
            try
            {
                StatusText.Text = "İşlem Yapılıyor...";
                StatusLight.Fill = new SolidColorBrush(Colors.Orange);
                DisableAllButtons();

                // Arka planda çalıştır ki arayüz donmasın
                await Task.Run(operation);

                // İşlem bitince durumu güncelle
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem başarısız oldu. Yönetici yetkisi gerekebilir.\n\nHata: " + ex.Message,
                                "Yetki Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshStatus();
            }
        }
    }
}