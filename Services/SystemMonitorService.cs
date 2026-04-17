using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Services
{
    public class SystemMonitorService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memoryCounter;

        public SystemMonitorService(ILogger<SystemMonitorService> logger)
        {
            _logger = logger;
            InitializeCounters();
        }

        private void InitializeCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();

                _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                _memoryCounter.NextValue();

                _logger.LogInformation("Performance counters initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing performance counters");
            }
        }

        public async Task<SystemStatusSnapshot> GetSystemSnapshot()
        {
            try
            {
                _logger.LogInformation("Starting to get system snapshot");

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

                _logger.LogInformation("System snapshot obtained successfully");
                return await Task.FromResult(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system snapshot");
                return new SystemStatusSnapshot();
            }
        }

        public float GetCpuUsage()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    return _cpuCounter.NextValue();
                }

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

        public float GetMemoryUsage()
        {
            try
            {
                if (_memoryCounter != null)
                {
                    return _memoryCounter.NextValue();
                }

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

        public float GetDiskUsage()
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

        public long GetTempFilesCount()
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

        public long GetTempFilesSize()
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

        public List<string> GetRecentlyInstalledSoftware()
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

        public List<string> GetRunningProcesses()
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

        public int GetStartupItemsCount()
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

        public async Task<string> GetSystemSnapshotJson()
        {
            try
            {
                var snapshot = await GetSystemSnapshot();
                return JsonSerializer.Serialize(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system snapshot JSON");
                return "{}";
            }
        }
    }
}