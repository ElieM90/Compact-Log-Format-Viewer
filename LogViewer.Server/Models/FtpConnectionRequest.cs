namespace LogViewer.Server.Models;

public class FtpConnectionRequest
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 21;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? RemotePath { get; set; }

    public bool? SaveConnection { get; set; }
}
