using System.Linq;
using System.Windows;

namespace UsbMonitorAgent.UI.Windows
{
    public partial class UsbDeviceListWindow : Window
    {
        public UsbDeviceListWindow()
        {
            InitializeComponent();

            UsbGrid.ItemsSource = UsbWatcherState.GetAll().ToList();
        }

        private void UsbGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (UsbGrid.SelectedItem is not UsbDeviceInfo dev)
                return;

            var win = new UsbInfoWindow(dev.DriveRoot);
            win.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
