using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Tools
{
    public class SystemOptimizerTool : ISystemTool
    {
        private readonly ILogger<SystemOptimizerTool> _logger;

        public string Name => "SystemOptimizer";
        public string Description => "Optimize system performance";
        public string RequestType => "system_optimizer";

        public SystemOptimizerTool(ILogger<SystemOptimizerTool> logger)
        {
            _logger = logger;
        }

        public async Task<ToolExecutionResult> ExecuteAsync(string jsonArguments, IProgress<string> progress)
        {
            try
            {
                _logger.LogInformation("Starting system optimization operation");
                progress?.Report("Analyzing optimization parameters...");

                var options = JsonSerializer.Deserialize<OptimizerOptions>(jsonArguments) ?? new OptimizerOptions();
                var optimizeStartup = options.OptimizeStartup ?? false;
                var freeMemory = options.FreeMemory ?? false;
                var optimizeServices = options.OptimizeServices ?? false;

                var totalOptimizations = 0;

                if (optimizeStartup)
                {
                    progress?.Report("Optimizing startup items...");
                    totalOptimizations += await OptimizeStartupItemsAsync(progress);
                }

                if (freeMemory)
                {
                    progress?.Report("Freeing memory...");
                    totalOptimizations += await FreeMemoryAsync(progress);
                }

                if (optimizeServices)
                {
                    progress?.Report("Optimizing services...");
                    totalOptimizations += await OptimizeServicesAsync(progress);
                }

                var message = $"System optimization completed. Applied {totalOptimizations} optimizations";

                _logger.LogInformation("System optimization completed. Applied {Count} optimizations", totalOptimizations);

                return ToolExecutionResult.CreateSuccess(
                    message,
                    $"Applied {totalOptimizations} optimizations",
                    totalOptimizations
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing system optimization operation");
                return ToolExecutionResult.CreateFailure("System optimization failed", ex);
            }
        }

        private async Task<int> OptimizeStartupItemsAsync(IProgress<string> progress)
        {
            var optimized = 0;
            try
            {
                var paths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
                };

                foreach (var path in paths)
                {
                    using var currentUserKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path, true);
                    if (currentUserKey != null)
                    {
                        var values = currentUserKey.GetValueNames();
                        foreach (var valueName in values)
                        {
                            var value = currentUserKey.GetValue(valueName) as string;
                            if (value != null && IsNonEssentialStartup(value))
                            {
                                currentUserKey.DeleteValue(valueName, false);
                                optimized++;
                                progress?.Report($"Removed startup item: {valueName}");
                                await Task.Delay(100);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing startup items");
            }
            return optimized;
        }

        private async Task<int> FreeMemoryAsync(IProgress<string> progress)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "rundll32.exe";
                process.StartInfo.Arguments = "advapi32.dll,ProcessIdleTasks";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                await process.WaitForExitAsync();

                progress?.Report("Memory freed successfully");
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freeing memory");
                return 0;
            }
        }

        private async Task<int> OptimizeServicesAsync(IProgress<string> progress)
        {
            var optimized = 0;
            try
            {
                var servicesToOptimize = new[]
                {
                    "SysMain", // Superfetch
                    "WSearch", // Windows Search
                    "TrkWks"   // Distributed Link Tracking Client
                };

                foreach (var serviceName in servicesToOptimize)
                {
                    try
                    {
                        using var service = new System.ServiceProcess.ServiceController(serviceName);
                        if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            await Task.Delay(2000);
                            progress?.Report($"Optimized service: {serviceName}");
                            optimized++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error optimizing service: {ServiceName}", serviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing services");
            }
            return optimized;
        }

        private bool IsNonEssentialStartup(string startupPath)
        {
            var essentialProcesses = new[]
            {
                "explorer.exe",
                "ctfmon.exe",
                "taskhostw.exe",
                "dwm.exe",
                "svchost.exe"
            };

            foreach (var process in essentialProcesses)
            {
                if (startupPath.Contains(process, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class OptimizerOptions
    {
        public bool? OptimizeStartup { get; set; }
        public bool? FreeMemory { get; set; }
        public bool? OptimizeServices { get; set; }
    }
}