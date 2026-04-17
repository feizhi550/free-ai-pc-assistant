using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Tools
{
    public class CacheCleanerTool : ISystemTool
    {
        private readonly ILogger<CacheCleanerTool> _logger;
        private readonly List<string> _cacheDirectories = new List<string>();

        public string Name => "CacheCleaner";
        public string Description => "Deep scan and clean system cache files";
        public string RequestType => "cache_cleaner";

        public CacheCleanerTool(ILogger<CacheCleanerTool> logger)
        {
            _logger = logger;
            InitializeCacheDirectories();
        }

        private void InitializeCacheDirectories()
        {
            try
            {
                _cacheDirectories.Add(Path.GetTempPath());

                var userTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp");
                if (Directory.Exists(userTemp))
                    _cacheDirectories.Add(userTemp);

                var chromeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cache");
                if (Directory.Exists(chromeCache))
                    _cacheDirectories.Add(chromeCache);

                var edgeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Microsoft", "Edge", "User Data", "Default", "Cache");
                if (Directory.Exists(edgeCache))
                    _cacheDirectories.Add(edgeCache);

                var vscodeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "Code", "CachedData");
                if (Directory.Exists(vscodeCache))
                    _cacheDirectories.Add(vscodeCache);

                var jetbrainsCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "JetBrains");
                if (Directory.Exists(jetbrainsCache))
                    _cacheDirectories.Add(jetbrainsCache);

                var windowsUpdateCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                if (Directory.Exists(windowsUpdateCache))
                    _cacheDirectories.Add(windowsUpdateCache);

                _logger.LogInformation("Initialized {Count} cache directories", _cacheDirectories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing cache directories");
            }
        }

        public async Task<ToolExecutionResult> ExecuteAsync(string jsonArguments, IProgress<string> progress)
        {
            try
            {
                _logger.LogInformation("Starting cache cleaning operation");
                progress?.Report("Analyzing cache cleaning parameters...");

                var options = JsonSerializer.Deserialize<CacheCleanerOptions>(jsonArguments) ?? new CacheCleanerOptions();
                var thresholdDays = options.ThresholdDays ?? 7;
                var autoDelete = options.AutoDelete ?? false;

                var totalCleanedSize = 0L;
                var totalFilesDeleted = 0;

                foreach (var directory in _cacheDirectories)
                {
                    try
                    {
                        progress?.Report($"Scanning directory: {directory}");
                        var (cleanedSize, filesDeleted) = await ScanAndCleanDirectoryAsync(directory, thresholdDays, autoDelete, progress);
                        totalCleanedSize += cleanedSize;
                        totalFilesDeleted += filesDeleted;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error scanning directory {Directory}", directory);
                        progress?.Report($"Error scanning directory {directory}: {ex.Message}");
                    }
                }

                var message = $"Cache cleaning completed. Deleted {totalFilesDeleted} files, freed {FormatSize(totalCleanedSize)} of space";

                _logger.LogInformation("Cache cleaning completed. Deleted {Files} files, freed {Size}", totalFilesDeleted, FormatSize(totalCleanedSize));

                return ToolExecutionResult.CreateSuccess(
                    message,
                    $"Cleaned {_cacheDirectories.Count} cache directories",
                    totalCleanedSize
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing cache cleaning operation");
                return ToolExecutionResult.CreateFailure("Cache cleaning failed", ex);
            }
        }

        private async Task<(long, int)> ScanAndCleanDirectoryAsync(string directoryPath, int thresholdDays, bool autoDelete, IProgress<string> progress)
        {
            var cleanedSize = 0L;
            var filesDeleted = 0;
            var thresholdDate = DateTime.Now.AddDays(-thresholdDays);

            try
            {
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Where(file => File.GetLastAccessTime(file) < thresholdDate)
                    .ToList();

                progress?.Report($"Found {files.Count} expired files in {directoryPath}");

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        cleanedSize += fileInfo.Length;

                        if (autoDelete)
                        {
                            File.Delete(file);
                            filesDeleted++;
                            if (filesDeleted % 100 == 0)
                            {
                                progress?.Report($"Deleted {filesDeleted} files, freed {FormatSize(cleanedSize)} space");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting file {File}", file);
                    }

                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory {Directory}", directoryPath);
            }

            return (cleanedSize, filesDeleted);
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024):F2} MB";
            else
                return $"{bytes / (1024 * 1024 * 1024):F2} GB";
        }
    }

    public class CacheCleanerOptions
    {
        public int? ThresholdDays { get; set; }
        public bool? AutoDelete { get; set; }
    }
}