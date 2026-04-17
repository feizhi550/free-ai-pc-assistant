using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Services
{
    public class AIDecisionEngine : BackgroundService
    {
        private readonly ILogger<AIDecisionEngine> _logger;
        private readonly OllamaService _ollamaService;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public event EventHandler<SystemStatusEventArgs> SystemStatusChanged;

        public AIDecisionEngine(ILogger<AIDecisionEngine> logger, OllamaService ollamaService, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AI Decision Engine started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting system status collection");
                    var snapshot = await CollectSystemStatusAsync();

                    _logger.LogInformation("Starting system state analysis");
                    var analysisResult = await _ollamaService.AnalyzeSystemStateAsync(snapshot);

                    _logger.LogInformation("Processing analysis result");
                    await ProcessAnalysisResultAsync(analysisResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during AI Decision Engine execution");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("AI Decision Engine stopped");
        }

        private async Task<SystemStatusSnapshot> CollectSystemStatusAsync()
        {
            var snapshot = new SystemStatusSnapshot
            {
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                DiskUsage = GetDiskUsage(),
                TempFilesCount = GetTempFilesCount(),
                TempFilesSize = GetTempFilesSize(),
                RecentlyInstalledSoftware = GetRecentlyInstalledSoftware(),
                RunningProcesses = GetRunningProcesses(),
                StartupItemsCount = GetStartupItemsCount()
            };

            return await Task.FromResult(snapshot);
        }

        private float GetCpuUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_PerfFormattedData_PerfOS_Processor where Name='_Total'");
                var collection = searcher.Get();
                var obj = collection.Cast<ManagementObject>().FirstOrDefault();
                if (obj != null)
                {
                    return float.Parse(obj["PercentProcessorTime"].ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU usage");
            }
            return 0;
        }

        private float GetMemoryUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                var collection = searcher.Get();
                var obj = collection.Cast<ManagementObject>().FirstOrDefault();
                if (obj != null)
                {
                    var totalMemory = ulong.Parse(obj["TotalVisibleMemorySize"].ToString());
                    var freeMemory = ulong.Parse(obj["FreePhysicalMemory"].ToString());
                    return ((float)(totalMemory - freeMemory) / totalMemory) * 100;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage");
            }
            return 0;
        }

        private float GetDiskUsage()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                return ((float)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting disk usage");
            }
            return 0;
        }

        private long GetTempFilesCount()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                if (Directory.Exists(tempPath))
                {
                    return Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories).Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting temp files count");
            }
            return 0;
        }

        private long GetTempFilesSize()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                if (Directory.Exists(tempPath))
                {
                    return Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories)
                        .Select(file => new FileInfo(file).Length)
                        .Sum();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting temp files size");
            }
            return 0;
        }

        private List<string> GetRecentlyInstalledSoftware()
        {
            var softwareList = new List<string>();
            try
            {
                var paths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var path in paths)
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey != null)
                            {
                                var displayName = subKey.GetValue("DisplayName") as string;
                                var installDate = subKey.GetValue("InstallDate") as string;

                                if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(installDate))
                                {
                                    try
                                    {
                                        if (DateTime.TryParseExact(installDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                                        {
                                            if ((DateTime.Now - date).TotalDays <= 30)
                                            {
                                                softwareList.Add(displayName);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recently installed software");
            }
            return softwareList;
        }

        private List<string> GetRunningProcesses()
        {
            try
            {
                return Process.GetProcesses()
                    .Select(p => p.ProcessName)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting running processes");
                return new List<string>();
            }
        }

        private int GetStartupItemsCount()
        {
            try
            {
                var count = 0;
                var paths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
                };

                foreach (var path in paths)
                {
                    using var currentUserKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path);
                    if (currentUserKey != null)
                    {
                        count += currentUserKey.GetValueNames().Length;
                    }

                    using var localMachineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                    if (localMachineKey != null)
                    {
                        count += localMachineKey.GetValueNames().Length;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting startup items count");
                return 0;
            }
        }

        private async Task ProcessAnalysisResultAsync(string analysisResult)
        {
            try
            {
                var result = JsonSerializer.Deserialize<AnalysisResult>(analysisResult);
                if (result != null)
                {
                    SystemStatusChanged?.Invoke(this, new SystemStatusEventArgs
                    {
                        Issues = result.Issues,
                        Recommendations = result.Recommendations
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analysis result");
            }
        }
    }
}