namespace UsbMonitorService
{
    public class UsbLogModel
    {
        public string Username { get; set; } = "";
        public string FileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DestPath { get; set; } = "";
        public string DriveLabel { get; set; } = "";
        public string DeviceIdentity { get; set; } = "";
        public long FileSize { get; set; }
        public string TimestampUtc { get; set; } = "";
        public string FileHash { get; set; } = "";
    }
}
