using System;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;

namespace UsbMonitorService.Core
{
    public static class UsbInfoHelper
    {

            /// <summary>
            /// Sürücü harfinden (F:, G:) yola çıkarak gerçek donanım kimliğini (VID/PID/SN) bulur.
            /// Çapraz sorgu (Cross-Reference) tekniği kullanır.
            /// </summary>
            public static string GetUsbIdentity(string driveLetter)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(driveLetter)) return "Unknown";

                    // Format Temizliği: "F:\" -> "F:"
                    driveLetter = driveLetter.TrimEnd('\\');
                    if (!driveLetter.EndsWith(":")) driveLetter += ":";

                    // 1. ADIM: LogicalDisk -> Partition -> DiskDrive zincirini kur
                    string diskQuery = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";

                    using var partitionSearcher = new ManagementObjectSearcher(diskQuery);
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        string driveQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";

                        using var driveSearcher = new ManagementObjectSearcher(driveQuery);
                        foreach (ManagementObject drive in driveSearcher.Get())
                        {
                            // DiskDrive bulundu. Şimdi içinden PNPDeviceID alalım.
                            // Örnek: USBSTOR\DISK&VEN_KINGSTON&PROD_DATATRAVELER...\0019E06B9C85F93167430362&0
                            string pnpId = drive["PNPDeviceID"]?.ToString() ?? "";

                            // Seri numarasını cımbızla (En sondaki parça)
                            string serial = ExtractSerialNumberFromPnp(pnpId);

                            if (!string.IsNullOrEmpty(serial))
                            {
                                // 2. ADIM: Seri numarası üzerinden USB ağacını tara (Cross-Reference)
                                // DiskDrive'da VID/PID yazmaz, ama aynı seri nolu USB Entity'de yazar.
                                string realIdentity = FindVidPidBySerial(serial);
                                if (!string.IsNullOrEmpty(realIdentity))
                                {
                                    return realIdentity; // BINGO! Gerçek VID/PID
                                }
                            }
                        }
                    }
                }
                catch { }

                return "VID:Unknown PID:Unknown SN:Unknown";
            }

            // Service tarafı için Retry mekanizması
            public static string GetUsbIdentityWithRetry(string driveRoot)
            {
                for (int i = 0; i < 5; i++)
                {
                    string id = GetUsbIdentity(driveRoot);
                    if (!string.IsNullOrWhiteSpace(id) && !id.Contains("Unknown"))
                        return id;
                    Thread.Sleep(300);
                }
                return "VID:Unknown PID:Unknown SN:Unknown";
            }

            /// <summary>
            /// USBSTOR stringinden temiz seri numarası çıkarır.
            /// </summary>
            private static string ExtractSerialNumberFromPnp(string pnpId)
            {
                try
                {
                    if (string.IsNullOrEmpty(pnpId)) return null;

                    // Son '\' işaretinden sonrasını al
                    int lastSlash = pnpId.LastIndexOf('\\');
                    if (lastSlash >= 0 && lastSlash < pnpId.Length - 1)
                    {
                        string serial = pnpId.Substring(lastSlash + 1);

                        // Genelde sonuna "&0" veya "&1" eklenir, onu temizle
                        if (serial.Contains("&"))
                        {
                            serial = serial.Substring(0, serial.IndexOf('&'));
                        }
                        return serial;
                    }
                }
                catch { }
                return null;
            }

            /// <summary>
            /// Seri numarasını kullanarak Win32_PnPEntity tablosunda "USB\VID_..." kaydını arar.
            /// </summary>
            private static string FindVidPidBySerial(string serial)
            {
                try
                {
                    // Seri numarası içeren TÜM aygıtları getir
                    // LIKE sorgusu ile arıyoruz
                    string query = $"SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%{serial}%'";

                    using var searcher = new ManagementObjectSearcher(query);
                    foreach (ManagementObject device in searcher.Get())
                    {
                        string pnp = device["PNPDeviceID"]?.ToString() ?? "";

                        // Bize "USB\VID_xxxx" ile başlayan kayıt lazım (USBSTOR değil!)
                        if (pnp.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase))
                        {
                            // Bulduk! Şimdi parse edelim.
                            // Örnek: USB\VID_0951&PID_1666\0019E06B9C85F93167430362

                            string vid = ExtractHex(pnp, "VID_");
                            string pid = ExtractHex(pnp, "PID_");

                            return $"VID:{vid} PID:{pid} SN:{serial}";
                        }
                    }
                }
                catch { }

                // Bulamazsak en azından seri numarasını dön
                return $"VID:Unknown PID:Unknown SN:{serial}";
            }

            private static string ExtractHex(string input, string key)
            {
                if (string.IsNullOrEmpty(input)) return "Unknown";

                var match = Regex.Match(input, key + "([0-9A-F]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                return "Unknown";
            }
        }
    }


