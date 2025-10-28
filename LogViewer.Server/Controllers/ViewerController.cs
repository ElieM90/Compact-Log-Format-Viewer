using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LogViewer.Server.Extensions;
using LogViewer.Server.Models;
using LogViewer.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogViewer.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViewerController : ControllerBase
    {
        private readonly ILogger<ViewerController> _logger;
        private readonly ILogParser _logParser;
        private readonly IFtpService _ftpService;
        private readonly IConnectionStorageService _connectionStorage;

        public ViewerController(ILogger<ViewerController> logger, ILogParser logParser, IFtpService ftpService, IConnectionStorageService connectionStorage)
        {
            _logger = logger;
            _logParser = logParser;
            _ftpService = ftpService;
            _connectionStorage = connectionStorage;
        }


        [HttpGet("open")]
        public ActionResult<string> Open(string filePath)
        {

            //Check for valid filepath
            if (System.IO.File.Exists(filePath) == false)
            {
                var message = $"No file exists on disk at {filePath}";
                return NotFound(message);
            }

            //Lets check file is valid JSON & not a text document on your upcoming novel
            string? firstLine;
            using (var s = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(s))
            { firstLine = sr.ReadLine(); }

            if (firstLine.IsValidJson() == false)
            {
                var message = $"The file {filePath} does not contain valid JSON on line one";
                return BadRequest(message);
            }

            //We will skip over/ignore invalid/malformed log lines
            try
            {
                var logs = _logParser.ReadLogs(filePath);
                return $"Log contains {logs.Count}";
            }
            catch (InvalidDataException ex)
            {
                // Can be InvalidDataExcpetion or JsonFormatterException
                // Such as 'The data on line 1 does not include the required @t field'
                return BadRequest($"There was a problem reading the JSON. {ex.Message}");
            }
        }

        [HttpGet("reload")]
        public ActionResult<string> Reload()
        {
            //Ensure _logFilePath not null
            if (string.IsNullOrEmpty(_logParser.LogFilePath) == false)
            {
                //Call Open again with the stored path
                return Open(_logParser.LogFilePath);
            }

            return Ok();
        }

        [HttpGet("totals")]
        public ActionResult<LogLevelCounts> TotalCounts()
        {
            if (_logParser.LogIsOpen == false)
                return BadRequest("No logfile has been opened yet");

            return _logParser.TotalCounts();
        }

        [HttpGet("errors")]
        public ActionResult<int> TotalErrors()
        {
            if (_logParser.LogIsOpen == false) return BadRequest("No logfile has been opened yet");
            return _logParser.TotalErrors();
        }

        [HttpGet("export")]
        public ActionResult Export(string messageTemplate, string newFileName)
        {
            if (_logParser.LogIsOpen == false) return BadRequest("No logfile has been opened yet");

            if (string.IsNullOrEmpty(newFileName)) return BadRequest("Missing filename to export the JSON log file");

            _logParser.ExportTextFile(messageTemplate, newFileName);
            return Ok();
        }

        [HttpGet("search")]
        public ActionResult<LogResults> Search(int pageNumber = 1, int pageSize = 100, string? filterExpression = null, SortOrder sort = SortOrder.Descending)
        {
            if (_logParser.LogIsOpen == false)
                return BadRequest("No logfile has been opened yet");

            return _logParser.Search(pageNumber, pageSize, filterExpression, sort);
        }

        // FTP Endpoints

        [HttpPost("ftp/test")]
        public async Task<ActionResult<bool>> TestFtpConnection([FromBody] FtpConnectionRequest request)
        {
            try
            {
                var result = await _ftpService.TestConnectionAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing FTP connection");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("ftp/list")]
        public async Task<ActionResult<FtpListResponse>> ListFtpDirectory([FromBody] FtpConnectionRequest request)
        {
            try
            {
                var remotePath = request.RemotePath ?? "/";
                var result = await _ftpService.ListDirectoryAsync(request, remotePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing FTP directory");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("ftp/open")]
        public async Task<ActionResult<string>> OpenFtpFile([FromBody] FtpConnectionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RemotePath))
                {
                    return BadRequest("Remote file path is required");
                }

                // Create temp directory for downloaded FTP files
                var tempDir = Path.Combine(Path.GetTempPath(), "CompactLogViewer", "ftp-downloads");

                // Download the file
                var localFilePath = await _ftpService.DownloadFileAsync(request, request.RemotePath, tempDir);

                // Save connection if requested
                if (request.SaveConnection ?? false)
                {
                    var settings = new FtpConnectionSettings
                    {
                        Host = request.Host,
                        Port = request.Port,
                        Username = request.Username,
                        EncryptedPassword = request.Password,
                        LastRemotePath = Path.GetDirectoryName(request.RemotePath)
                    };
                    await _connectionStorage.SaveConnectionAsync(settings);
                }

                // Open the downloaded file using the existing Open method
                return Open(localFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening FTP file");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("ftp/connections")]
        public async Task<ActionResult<List<FtpConnectionSettings>>> GetAllConnections()
        {
            try
            {
                var connections = await _connectionStorage.GetAllConnectionsAsync();
                return Ok(connections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved connections");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("ftp/connections/{name}")]
        public async Task<ActionResult<FtpConnectionSettings>> GetConnection(string name)
        {
            try
            {
                var connection = await _connectionStorage.GetConnectionAsync(name);
                if (connection == null)
                {
                    return NotFound($"Connection '{name}' not found");
                }
                return Ok(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading connection '{name}'");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("ftp/connections")]
        public async Task<ActionResult> SaveConnection([FromBody] FtpConnectionSettings settings)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.Name))
                {
                    return BadRequest("Connection name is required");
                }

                await _connectionStorage.SaveConnectionAsync(settings);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving connection");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("ftp/connections/{name}")]
        public async Task<ActionResult> DeleteConnection(string name)
        {
            try
            {
                await _connectionStorage.DeleteConnectionAsync(name);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting connection '{name}'");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("ftp/connections")]
        public async Task<ActionResult> ClearAllConnections()
        {
            try
            {
                await _connectionStorage.ClearAllConnectionsAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all connections");
                return BadRequest(ex.Message);
            }
        }

    }
}
