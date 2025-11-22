using System;
using System.IO;
using System.Windows;
using UsbMonitorAgent.Helpers;
using UsbMonitorAgent.UI.Windows;


namespace UsbMonitorAgent
{
    public partial class UsbInfoWindow : Window
    {
        public string DriveLetter { get; set; }
        public string VolumeLabel { get; set; }
        public string FileSystem { get; set; }
        public string SizeInfo { get; set; }
        public string VidPid { get; set; }
        public string SerialNumber { get; set; }
        public string VolumeGuid { get; set; }

        public UsbInfoWindow(string driveRoot)
        {
            InitializeComponent();

            LoadUsbInfo(driveRoot);

            DataContext = this;
        }

        private void LoadUsbInfo(string driveRoot)
        {
            try
            {
                // 🔥 Çoklu USB’den doğru olanı al
                var deviceInfo = UsbWatcherState.Get(driveRoot);

                var di = new DriveInfo(driveRoot);

                DriveLetter = driveRoot;

                // Etiket
                VolumeLabel = deviceInfo?.Label;
                if (string.IsNullOrWhiteSpace(VolumeLabel))
                    VolumeLabel = di.VolumeLabel;

                FileSystem = di.DriveFormat;

                double total = di.TotalSize / 1024.0 / 1024.0 / 1024.0;
                double free = di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                SizeInfo = $"{total:F2} GB (Boş: {free:F2} GB)";

                // VID/PID – agent yakaladıysa onu kullan
                string identity = deviceInfo?.Identity;

                if (string.IsNullOrWhiteSpace(identity))
                {
                    // Fallback WMI
                    identity = UsbInfoHelper.GetUsbIdentity(driveRoot.Replace("\\", ""));
                }

                VidPid = ExtractVidPid(identity);
                SerialNumber = ExtractSerial(identity);

                VolumeGuid = GetVolumeSerial(driveRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show("USB bilgisi alınırken hata oluştu: " + ex.Message,
                                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ExtractVidPid(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity) ||
                identity.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                return "Unknown";

            var parts = identity.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return parts[0] + " " + parts[1];

            return identity;
        }

        private string ExtractSerial(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
                return "Unknown";

            int idx = identity.IndexOf("SN:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return "Unknown";

            string sn = identity.Substring(idx + 3).Trim();

            if (string.IsNullOrWhiteSpace(sn))
                return "Unknown";

            return sn;
        }

        private string GetVolumeSerial(string driveRoot)
        {
            try
            {
                var di = new DriveInfo(driveRoot);
                return di.VolumeLabel + " (" + driveRoot + ")";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }
}
