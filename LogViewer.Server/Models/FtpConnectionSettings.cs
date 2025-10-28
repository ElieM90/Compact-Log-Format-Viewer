namespace LogViewer.Server.Models
{
    public class FtpConnectionSettings
    {
        public string Name { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 21;

        public string Username { get; set; } = string.Empty;

        // Encrypted password will be stored
        public string EncryptedPassword { get; set; } = string.Empty;

        public string? LastRemotePath { get; set; }
    }
}
