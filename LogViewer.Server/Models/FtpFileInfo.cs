using System;

namespace LogViewer.Server.Models
{
    public class FtpFileInfo
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public long Size { get; set; }

        public DateTime Modified { get; set; }

        public bool IsDirectory { get; set; }

        public string? Extension { get; set; }
    }
}
