using System.Collections.Generic;
using System.Threading.Tasks;
using LogViewer.Server.Models;

namespace LogViewer.Server.Services
{
    public interface IConnectionStorageService
    {
        Task SaveConnectionAsync(FtpConnectionSettings settings);

        Task<List<FtpConnectionSettings>> GetAllConnectionsAsync();

        Task<FtpConnectionSettings?> GetConnectionAsync(string name);

        Task DeleteConnectionAsync(string name);

        Task ClearAllConnectionsAsync();

        bool HasSavedConnections();
    }
}
