using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Tools
{
    public class UninstallTool : ISystemTool
    {
        private readonly ILogger<UninstallTool> _logger;

        public string Name => "UninstallTool";
        public string Description => "Uninstall installed software";
        public string RequestType => "uninstall";

        public UninstallTool(ILogger<UninstallTool> logger)
        {
            _logger = logger;
        }

        public async Task<ToolExecutionResult> ExecuteAsync(string jsonArguments, IProgress<string> progress)
        {
            try
            {
                _logger.LogInformation("Starting software uninstall operation");
                progress?.Report("Analyzing uninstall parameters...");

                var options = JsonSerializer.Deserialize<UninstallOptions>(jsonArguments) ?? new UninstallOptions();
                var softwareName = options.SoftwareName;
                var forceUninstall = options.ForceUninstall ?? false;

                if (string.IsNullOrEmpty(softwareName))
                {
                    return ToolExecutionResult.CreateFailure("Software name cannot be empty");
                }

                var softwareInfo = await FindSoftwareAsync(softwareName, progress);
                if (softwareInfo == null)
                {
                    return ToolExecutionResult.CreateFailure($"Software not found: {softwareName}");
                }

                progress?.Report($"Uninstalling software: {softwareInfo.DisplayName}");
                var success = await UninstallSoftwareAsync(softwareInfo, forceUninstall, progress);

                if (success)
                {
                    _logger.LogInformation("Software uninstalled successfully: {SoftwareName}", softwareInfo.DisplayName);
                    return ToolExecutionResult.CreateSuccess($"Software {softwareInfo.DisplayName} uninstalled successfully");
                }
                else
                {
                    _logger.LogError("Software uninstallation failed: {SoftwareName}", softwareInfo.DisplayName);
                    return ToolExecutionResult.CreateFailure($"Software {softwareInfo.DisplayName} uninstallation failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing software uninstall operation");
                return ToolExecutionResult.CreateFailure("Software uninstallation failed", ex);
            }
        }

        private async Task<SoftwareInfo> FindSoftwareAsync(string softwareName, IProgress<string> progress)
        {
            progress?.Report("Searching for software...");

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
                            var uninstallString = subKey.GetValue("UninstallString") as string;
                            var quietUninstallString = subKey.GetValue("QuietUninstallString") as string;

                            if (!string.IsNullOrEmpty(displayName) && (!string.IsNullOrEmpty(uninstallString) || !string.IsNullOrEmpty(quietUninstallString)))
                            {
                                if (displayName.Contains(softwareName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return new SoftwareInfo
                                    {
                                        DisplayName = displayName,
                                        UninstallString = uninstallString,
                                        QuietUninstallString = quietUninstallString,
                                        RegistryPath = $"{path}\\{subKeyName}"
                                    };
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private async Task<bool> UninstallSoftwareAsync(SoftwareInfo softwareInfo, bool forceUninstall, IProgress<string> progress)
        {
            try
            {
                var uninstallCommand = softwareInfo.QuietUninstallString ?? softwareInfo.UninstallString;
                if (string.IsNullOrEmpty(uninstallCommand))
                {
                    progress?.Report("Uninstall command not found");
                    return false;
                }

                string executable;
                string arguments;
                if (uninstallCommand.StartsWith('"'))
                {
                    var quoteIndex = uninstallCommand.IndexOf('"', 1);
                    if (quoteIndex > 0)
                    {
                        executable = uninstallCommand.Substring(1, quoteIndex - 1);
                        arguments = uninstallCommand.Substring(quoteIndex + 1).Trim();
                    }
                    else
                    {
                        executable = uninstallCommand;
                        arguments = string.Empty;
                    }
                }
                else
                {
                    var spaceIndex = uninstallCommand.IndexOf(' ');
                    if (spaceIndex > 0)
                    {
                        executable = uninstallCommand.Substring(0, spaceIndex);
                        arguments = uninstallCommand.Substring(spaceIndex + 1);
                    }
                    else
                    {
                        executable = uninstallCommand;
                        arguments = string.Empty;
                    }
                }

                if (forceUninstall && !arguments.Contains("/quiet", StringComparison.OrdinalIgnoreCase) && !arguments.Contains("/silent", StringComparison.OrdinalIgnoreCase))
                {
                    arguments += " /quiet /norestart";
                }

                progress?.Report($"Executing uninstall command: {executable} {arguments}");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    progress?.Report("Unable to start uninstall process");
                    return false;
                }

                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        progress?.Report(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        progress?.Report($"Error: {e.Data}");
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit(60000));

                if (!process.HasExited)
                {
                    process.Kill();
                    progress?.Report("Uninstall process timed out");
                    return false;
                }

                progress?.Report($"Uninstall process exited with code: {process.ExitCode}");
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing software uninstallation");
                progress?.Report($"Uninstall error: {ex.Message}");
                return false;
            }
        }

        public List<SoftwareInfo> GetInstalledSoftwareList()
        {
            var softwareList = new List<SoftwareInfo>();
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
                            var uninstallString = subKey.GetValue("UninstallString") as string;
                            var quietUninstallString = subKey.GetValue("QuietUninstallString") as string;
                            var installDate = subKey.GetValue("InstallDate") as string;

                            if (!string.IsNullOrEmpty(displayName) && (!string.IsNullOrEmpty(uninstallString) || !string.IsNullOrEmpty(quietUninstallString)))
                            {
                                softwareList.Add(new SoftwareInfo
                                {
                                    DisplayName = displayName,
                                    UninstallString = uninstallString,
                                    QuietUninstallString = quietUninstallString,
                                    InstallDate = installDate,
                                    RegistryPath = $"{path}\\{subKeyName}"
                                });
                            }
                        }
                    }
                }
            }

            return softwareList;
        }
    }

    public class SoftwareInfo
    {
        public string DisplayName { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public string QuietUninstallString { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
        public string RegistryPath { get; set; } = string.Empty;
    }

    public class UninstallOptions
    {
        public string SoftwareName { get; set; } = string.Empty;
        public bool? ForceUninstall { get; set; } = false;
    }
}