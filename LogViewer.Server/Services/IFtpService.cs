using System.Threading.Tasks;
using LogViewer.Server.Models;

namespace LogViewer.Server.Services
{
    public interface IFtpService
    {
        Task<bool> TestConnectionAsync(FtpConnectionRequest request);

        Task<FtpListResponse> ListDirectoryAsync(FtpConnectionRequest request, string remotePath);

        Task<string> DownloadFileAsync(FtpConnectionRequest request, string remoteFilePath, string localDirectory);

        void Disconnect();
    }
}
