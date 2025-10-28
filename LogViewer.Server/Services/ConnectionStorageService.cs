using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LogViewer.Server.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LogViewer.Server.Services
{
    public class ConnectionStorageService : IConnectionStorageService
    {
        private readonly IDataProtector _protector;
        private readonly ILogger<ConnectionStorageService> _logger;
        private readonly string _settingsPath;

        public ConnectionStorageService(IDataProtectionProvider dataProtectionProvider, ILogger<ConnectionStorageService> logger)
        {
            _protector = dataProtectionProvider.CreateProtector("FtpConnectionSettings");
            _logger = logger;

            // Store settings in user's local app data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "CompactLogViewer");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _settingsPath = Path.Combine(appFolder, "ftp-connections.json");
        }

        public async Task SaveConnectionAsync(FtpConnectionSettings settings)
        {
            try
            {
                var connections = await GetAllConnectionsAsync();

                // Remove existing connection with same name (update scenario)
                connections.RemoveAll(c => c.Name.Equals(settings.Name, StringComparison.OrdinalIgnoreCase));

                // Encrypt the password before saving
                var settingsToSave = new FtpConnectionSettings
                {
                    Name = settings.Name,
                    Host = settings.Host,
                    Port = settings.Port,
                    Username = settings.Username,
                    EncryptedPassword = !string.IsNullOrEmpty(settings.EncryptedPassword)
                        ? _protector.Protect(settings.EncryptedPassword)
                        : string.Empty,
                    LastRemotePath = settings.LastRemotePath
                };

                connections.Add(settingsToSave);

                var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                _logger.LogInformation($"FTP connection '{settings.Name}' saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save FTP connection settings");
                throw;
            }
        }

        public async Task<List<FtpConnectionSettings>> GetAllConnectionsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new List<FtpConnectionSettings>();
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                var connections = JsonSerializer.Deserialize<List<FtpConnectionSettings>>(json) ?? new List<FtpConnectionSettings>();

                // Decrypt passwords
                foreach (var connection in connections)
                {
                    if (!string.IsNullOrEmpty(connection.EncryptedPassword))
                    {
                        try
                        {
                            connection.EncryptedPassword = _protector.Unprotect(connection.EncryptedPassword);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to decrypt password for connection '{connection.Name}'");
                            connection.EncryptedPassword = string.Empty;
                        }
                    }
                }

                return connections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load FTP connections");
                return new List<FtpConnectionSettings>();
            }
        }

        public async Task<FtpConnectionSettings?> GetConnectionAsync(string name)
        {
            var connections = await GetAllConnectionsAsync();
            return connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task DeleteConnectionAsync(string name)
        {
            try
            {
                var connections = await GetAllConnectionsAsync();
                connections.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                _logger.LogInformation($"FTP connection '{name}' deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete FTP connection '{name}'");
                throw;
            }
        }

        public async Task ClearAllConnectionsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                    _logger.LogInformation("All FTP connections cleared");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear FTP connections");
                throw;
            }

            await Task.CompletedTask;
        }

        public bool HasSavedConnections()
        {
            return File.Exists(_settingsPath) && new FileInfo(_settingsPath).Length > 0;
        }
    }
}
