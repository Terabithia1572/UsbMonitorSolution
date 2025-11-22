using Microsoft.Data.Sqlite;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using OfficeOpenXml;
using OfficeOpenXml.Style;


namespace UsbMonitorAgent
{
    public partial class MainWindow : Window
    {
        private string _dbPath = @"C:\ProgramData\UsbMonitor\usb_logs.db";
        public ObservableCollection<UsbLogModel> Logs { get; set; } = new();
        private DispatcherTimer _refreshTimer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                LoadLogs();
                StartAutoRefresh();
            };
        }

        private void LoadLogs()
        {
            if (!File.Exists(_dbPath)) return;
            Logs.Clear();

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();

            // 🔥 HASH dahil tüm kolonlar seçiliyor
            cmd.CommandText = @"
                SELECT 
                    Username,
                    FileName,
                    SourcePath,
                    DestPath,
                    DriveLabel,
                    DeviceIdentity,
                    FileSize,
                    TimestampUtc,
                    FileHash
                FROM UsbLogs
                ORDER BY Id DESC
                LIMIT 300
            ";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string rawTime = r.GetString(7);
                string displayTime = rawTime;

                try
                {
                    var dtUtc = DateTime.Parse(rawTime, null, DateTimeStyles.RoundtripKind);
                    var dtLocal = dtUtc.ToLocalTime();
                    displayTime = dtLocal.ToString("dd.MM.yyyy HH:mm:ss");
                }
                catch { }

                Logs.Add(new UsbLogModel
                {
                    Username = r.GetString(0),
                    FileName = r.GetString(1),
                    SourcePath = r.GetString(2),
                    DestPath = r.GetString(3),
                    DriveLabel = r.GetString(4),
                    DeviceIdentity = r.GetString(5),
                    FileSize = r.GetInt64(6),
                    TimestampUtc = displayTime,
                    FileHash = r.GetString(8)       // 🔥 HASH artık modele geliyor
                });
            }

            LogsGrid.ItemsSource = Logs;
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(3)
            };
            _refreshTimer.Tick += (s, e) => LoadLogs();
            _refreshTimer.Start();
        }
        private void OpenUsbList_Click(object sender, RoutedEventArgs e)
        {
            // Şifre koruması
            if (!Helpers.SecurityHelper.EnsureAuthenticated())
                return;

            var win = new UsbMonitorAgent.UI.Windows.UsbDeviceListWindow();
            win.ShowDialog();
        }


        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (LogsGrid.Items.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak kayıt yok!", "Bilgi");
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Dosyası (*.csv)|*.csv",
                FileName = "UsbLogs.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dlg.FileName, false, new System.Text.UTF8Encoding(true)))
                {
                    // Başlık satırı
                    writer.WriteLine("Username,FileName,SourcePath,DestPath,DriveLabel,DeviceIdentity,FileSize,TimestampUtc,FileHash");

                    // Satırları yaz
                    foreach (var item in LogsGrid.Items)
                    {
                        if (item is UsbLogModel log)
                        {
                            writer.WriteLine(
                                $"{Safe(log.Username)}," +
                                $"{Safe(log.FileName)}," +
                                $"{Safe(log.SourcePath)}," +
                                $"{Safe(log.DestPath)}," +
                                $"{Safe(log.DriveLabel)}," +
                                $"{Safe(log.DeviceIdentity)}," +
                                $"{log.FileSize}," +
                                $"{log.TimestampUtc}," +
                                $"{Safe(log.FileHash)}");
                        }
                    }
                }

                MessageBox.Show("CSV başarıyla oluşturuldu!", "Başarılı");
            }
        }

        private string Safe(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (LogsGrid.Items.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak kayıt yok!", "Bilgi");
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                FileName = "UsbLogs.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                    using (var package = new ExcelPackage())
                    {
                        var sheet = package.Workbook.Worksheets.Add("Logs");

                        string[] headers =
                        {
                    "Username","FileName","SourcePath","DestPath","DriveLabel",
                    "DeviceIdentity","FileSize","TimestampUtc","FileHash"
                };

                        // Başlıklar
                        for (int i = 0; i < headers.Length; i++)
                        {
                            sheet.Cells[1, i + 1].Value = headers[i];
                            sheet.Cells[1, i + 1].Style.Font.Bold = true;
                            sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        }

                        // Veriler
                        int row = 2;
                        foreach (var item in LogsGrid.Items)
                        {
                            if (item is UsbLogModel log)
                            {
                                sheet.Cells[row, 1].Value = log.Username;
                                sheet.Cells[row, 2].Value = log.FileName;
                                sheet.Cells[row, 3].Value = log.SourcePath;
                                sheet.Cells[row, 4].Value = log.DestPath;
                                sheet.Cells[row, 5].Value = log.DriveLabel;
                                sheet.Cells[row, 6].Value = log.DeviceIdentity;
                                sheet.Cells[row, 7].Value = log.FileSize;
                                sheet.Cells[row, 8].Value = log.TimestampUtc;
                                sheet.Cells[row, 9].Value = log.FileHash;
                                row++;
                            }
                        }

                        // Crash fix ➜ AutoFit sadece sheet doluysa
                        if (sheet.Dimension != null)
                            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                        File.WriteAllBytes(dlg.FileName, package.GetAsByteArray());
                    }

                    MessageBox.Show("Excel başarıyla oluşturuldu!", "Başarılı");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Excel hatası: " + ex.Message, "Hata");
                }
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // ❗ Uygulamayı gerçekten kapatma — gizle
            e.Cancel = true;
            this.Hide();
        }



    }
}
