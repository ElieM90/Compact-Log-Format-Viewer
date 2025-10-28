using System;
using System.IO;
using System.Threading.Tasks;
using FluentFTP;
using LogViewer.Server.Models;
using Microsoft.Extensions.Logging;

namespace LogViewer.Server.Services
{
    public class FtpService : IFtpService
    {
        private readonly ILogger<FtpService> _logger;
        private FtpClient? _currentClient;
        private readonly object _lock = new object();

        public FtpService(ILogger<FtpService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> TestConnectionAsync(FtpConnectionRequest request)
        {
            return await Task.Run(() =>
            {
                FtpClient? client = null;
                try
                {
                    client = CreateClient(request);
                    client.AutoConnect();
                    _logger.LogInformation($"Successfully connected to FTP server {request.Host}");
                    return client.IsConnected;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to connect to FTP server {request.Host}");
                    return false;
                }
                finally
                {
                    if (client != null && client.IsConnected)
                    {
                        client.Disconnect();
                    }
                    client?.Dispose();
                }
            });
        }

        public async Task<FtpListResponse> ListDirectoryAsync(FtpConnectionRequest request, string remotePath)
        {
            return await Task.Run(() =>
            {
                var response = new FtpListResponse
                {
                    CurrentPath = remotePath
                };

                FtpClient? client = null;
                try
                {
                    client = CreateClient(request);
                    client.AutoConnect();

                    if (!client.IsConnected)
                    {
                        response.Success = false;
                        response.ErrorMessage = "Failed to connect to FTP server";
                        return response;
                    }

                    // List directory contents
                    var items = client.GetListing(remotePath);

                    foreach (var item in items)
                    {
                        // Only include directories and log files (.txt, .json, .clef)
                        if (item.Type == FtpObjectType.Directory ||
                            IsLogFile(item.Name))
                        {
                            response.Files.Add(new FtpFileInfo
                            {
                                Name = item.Name,
                                FullPath = item.FullName,
                                Size = item.Size,
                                Modified = item.Modified,
                                IsDirectory = item.Type == FtpObjectType.Directory,
                                Extension = Path.GetExtension(item.Name)
                            });
                        }
                    }

                    response.Success = true;
                    _logger.LogInformation($"Listed {response.Files.Count} items in {remotePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to list directory {remotePath}");
                    response.Success = false;
                    response.ErrorMessage = ex.Message;
                }
                finally
                {
                    if (client != null && client.IsConnected)
                    {
                        client.Disconnect();
                    }
                    client?.Dispose();
                }

                return response;
            });
        }

        public async Task<string> DownloadFileAsync(FtpConnectionRequest request, string remoteFilePath, string localDirectory)
        {
            return await Task.Run(() =>
            {
                FtpClient? client = null;
                try
                {
                    client = CreateClient(request);
                    client.AutoConnect();

                    if (!client.IsConnected)
                    {
                        throw new Exception("Failed to connect to FTP server");
                    }

                    // Create local directory if it doesn't exist
                    if (!Directory.Exists(localDirectory))
                    {
                        Directory.CreateDirectory(localDirectory);
                    }

                    // Generate local file path
                    var fileName = Path.GetFileName(remoteFilePath);
                    var localFilePath = Path.Combine(localDirectory, fileName);

                    // Download the file
                    var result = client.DownloadFile(localFilePath, remoteFilePath, FtpLocalExists.Overwrite);

                    if (result == FtpStatus.Success)
                    {
                        _logger.LogInformation($"Successfully downloaded {remoteFilePath} to {localFilePath}");
                        return localFilePath;
                    }
                    else
                    {
                        throw new Exception($"Failed to download file. Status: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to download file {remoteFilePath}");
                    throw;
                }
                finally
                {
                    if (client != null && client.IsConnected)
                    {
                        client.Disconnect();
                    }
                    client?.Dispose();
                }
            });
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                if (_currentClient != null)
                {
                    if (_currentClient.IsConnected)
                    {
                        _currentClient.Disconnect();
                    }
                    _currentClient.Dispose();
                    _currentClient = null;
                }
            }
        }

        private FtpClient CreateClient(FtpConnectionRequest request)
        {
            // Parse IIS authentication format: website|hsc\username
            // The username format is: website|domain\username or just username
            var username = request.Username;

            var client = new FtpClient(request.Host, username, request.Password, request.Port);

            // Configure for standard FTP
            client.Config.EncryptionMode = FtpEncryptionMode.None;
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;

            _logger.LogInformation($"Created FTP client for {request.Host}:{request.Port} with username: {username}");

            return client;
        }

        private bool IsLogFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".txt" || extension == ".json" || extension == ".clef";
        }
    }
}
