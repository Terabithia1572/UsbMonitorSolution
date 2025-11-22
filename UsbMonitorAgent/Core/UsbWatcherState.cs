using System.Collections.Generic;

namespace UsbMonitorAgent
{
    /// <summary>
    /// Birden fazla USB aygıtının bilgilerini saklayan global state.
    /// </summary>
    public static class UsbWatcherState
    {
        /// <summary>
        /// DriveRoot (örn: E:\) → USB bilgisi
        /// </summary>
        public static Dictionary<string, UsbDeviceInfo> Devices { get; } = new();

        /// <summary>
        /// USB ekleme veya güncelleme
        /// </summary>
        public static void AddOrUpdate(string driveRoot, string label, string identity)
        {
            if (string.IsNullOrWhiteSpace(driveRoot))
                return;

            Devices[driveRoot] = new UsbDeviceInfo
            {
                DriveRoot = driveRoot,
                Label = label,
                Identity = identity
            };
        }

        /// <summary>
        /// USB kaldırıldığında sil
        /// </summary>
        public static void Remove(string driveRoot)
        {
            if (string.IsNullOrWhiteSpace(driveRoot))
                return;

            if (Devices.ContainsKey(driveRoot))
                Devices.Remove(driveRoot);
        }

        /// <summary>
        /// Tek bir USB’nin bilgilerini getir
        /// </summary>
        public static UsbDeviceInfo? Get(string driveRoot)
        {
            if (Devices.TryGetValue(driveRoot, out var info))
                return info;

            return null;
        }

        /// <summary>
        /// Tüm USB'leri getir (USB listesi penceresi için)
        /// </summary>
        public static IEnumerable<UsbDeviceInfo> GetAll()
        {
            return Devices.Values;
        }
    }

    /// <summary>
    /// Tek bir USB aygıtına ait metadata
    /// </summary>
    public class UsbDeviceInfo
    {
        public string DriveRoot { get; set; } = "";
        public string Label { get; set; } = "";
        public string Identity { get; set; } = ""; // VID/PID/SN
    }
}
