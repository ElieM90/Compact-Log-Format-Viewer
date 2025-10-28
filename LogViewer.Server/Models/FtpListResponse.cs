using System.Collections.Generic;

namespace LogViewer.Server.Models
{
    public class FtpListResponse
    {
        public string CurrentPath { get; set; } = string.Empty;

        public List<FtpFileInfo> Files { get; set; } = new List<FtpFileInfo>();

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
