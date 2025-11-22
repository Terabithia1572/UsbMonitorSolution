using System;
using System.Threading.Tasks;
using System.Windows;

namespace UsbMonitorAgent.UI.Windows
{
    public partial class NotificationWindow : Window
    {
        public NotificationWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Sağ Alt Köşeye Konumlandır
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 20;
            this.Top = desktopWorkingArea.Bottom - this.Height - 20;

            // 3 Saniye Bekle
            await Task.Delay(3000);

            // Kapat (Fade out efekti eklenebilir ama düz kapatmak performanslıdır)
            this.Close();
        }
    }
}