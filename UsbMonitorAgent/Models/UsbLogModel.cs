namespace UsbMonitorAgent
{
    public class UsbLogModel
    {
        public string Username { get; set; } = "";
        public string? FileName { get; set; }
        public string? SourcePath { get; set; }
        public string? DestPath { get; set; }
        public string? DriveLabel { get; set; }
        public string? DeviceIdentity { get; set; }
        public long FileSize { get; set; }
        public string TimestampUtc { get; set; } = "";
        public string? FileHash { get; set; }

        public string FileSizeFormatted =>
            FileSize switch
            {
                < 1024 => $"{FileSize} B",
                < 1024 * 1024 => $"{FileSize / 1024.0:F2} KB",
                _ => $"{FileSize / 1024.0 / 1024.0:F2} MB"
            };
    }
}
