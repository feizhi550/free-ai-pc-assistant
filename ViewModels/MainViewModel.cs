using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Services;
using AIPCAssistant.Tools;
using AIPCAssistant.Models;

namespace AIPCAssistant.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly OllamaService _ollamaService;
        private readonly AIDecisionEngine _aiDecisionEngine;
        private readonly IEnumerable<ISystemTool> _systemTools;
        private readonly SystemMonitorService _systemMonitorService;
        private Timer _updateTimer;

        private string _userMessage = string.Empty;
        private string _assistantMessage = string.Empty;
        private bool _isProcessing;
        private string _systemHealthStatus = string.Empty;
        private string _selectedNavItem = "Health";
        private string _aiAnalysisStatus = "AI 正在分析系统状态...";
        private float _cpuUsage;
        private float _memoryUsage;
        private float _diskUsage;
        private string _ollamaApiUrl = "http://localhost:11434";
        private string _defaultModel = "qwen2.5:3b";
        private bool _autoSystemAnalysis = true;
        private bool _optimizationNotification = true;

        public AsyncRelayCommand SendCommand { get; }
        public RelayCommand<Recommendation> AcceptSuggestionCommand { get; }
        public RelayCommand<Recommendation> DismissSuggestionCommand { get; }
        public AsyncRelayCommand CleanCacheCommand { get; }
        public AsyncRelayCommand OptimizeSystemCommand { get; }
        public AsyncRelayCommand ShowUninstallCommand { get; }
        public AsyncRelayCommand FreeMemoryCommand { get; }
        public IRelayCommand<string> NavigateCommand { get; }
        public IRelayCommand SaveSettingsCommand { get; }
        public AsyncRelayCommand RefreshModelsCommand { get; }

        public ObservableCollection<ChatMessage> ChatHistory { get; }
        public ObservableCollection<Recommendation> Recommendations { get; }
        public ObservableCollection<OperationHistory> OperationHistory { get; }
        public ObservableCollection<string> AvailableModels { get; }

        public string UserMessage
        {
            get => _userMessage;
            set
            {
                if (SetProperty(ref _userMessage, value))
                {
                    SendCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public string AssistantMessage
        {
            get => _assistantMessage;
            set => SetProperty(ref _assistantMessage, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    SendCommand?.NotifyCanExecuteChanged();
                    AcceptSuggestionCommand?.NotifyCanExecuteChanged();
                    DismissSuggestionCommand?.NotifyCanExecuteChanged();
                    CleanCacheCommand?.NotifyCanExecuteChanged();
                    OptimizeSystemCommand?.NotifyCanExecuteChanged();
                    ShowUninstallCommand?.NotifyCanExecuteChanged();
                    FreeMemoryCommand?.NotifyCanExecuteChanged();
                    RefreshModelsCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public string SystemHealthStatus
        {
            get => _systemHealthStatus;
            set => SetProperty(ref _systemHealthStatus, value);
        }

        public string AIAnalysisStatus
        {
            get => _aiAnalysisStatus;
            set => SetProperty(ref _aiAnalysisStatus, value);
        }

        public float CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        public float MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }

        public float DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }

        public string SelectedNavItem
        {
            get => _selectedNavItem;
            set => SetProperty(ref _selectedNavItem, value);
        }

        public string OllamaApiUrl
        {
            get => _ollamaApiUrl;
            set
            {
                if (SetProperty(ref _ollamaApiUrl, value))
                {
                    // 当API URL变更时，重新获取模型列表
                    _ = LoadAvailableModelsAsync();
                }
            }
        }

        public string DefaultModel
        {
            get => _defaultModel;
            set => SetProperty(ref _defaultModel, value);
        }

        public bool AutoSystemAnalysis
        {
            get => _autoSystemAnalysis;
            set => SetProperty(ref _autoSystemAnalysis, value);
        }

        public bool OptimizationNotification
        {
            get => _optimizationNotification;
            set => SetProperty(ref _optimizationNotification, value);
        }

        public MainViewModel(
            ILogger<MainViewModel> logger,
            OllamaService ollamaService,
            AIDecisionEngine aiDecisionEngine,
            SystemMonitorService systemMonitorService,
            IEnumerable<ISystemTool> systemTools)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _aiDecisionEngine = aiDecisionEngine;
            _systemMonitorService = systemMonitorService;
            _systemTools = systemTools;

            ChatHistory = new ObservableCollection<ChatMessage>();
            Recommendations = new ObservableCollection<Recommendation>();
            OperationHistory = new ObservableCollection<OperationHistory>();
            AvailableModels = new ObservableCollection<string>();

            // 添加一些模拟的操作历史记录
            OperationHistory.Add(new OperationHistory
            {
                Timestamp = DateTime.Now.AddMinutes(-30),
                Operation = "清理系统缓存",
                Status = "成功",
                Details = "释放了1.2GB磁盘空间"
            });

            OperationHistory.Add(new OperationHistory
            {
                Timestamp = DateTime.Now.AddHours(-2),
                Operation = "优化启动项",
                Status = "成功",
                Details = "禁用了3个启动项"
            });

            OperationHistory.Add(new OperationHistory
            {
                Timestamp = DateTime.Now.AddHours(-5),
                Operation = "释放内存",
                Status = "成功",
                Details = "释放了500MB内存"
            });

            OperationHistory.Add(new OperationHistory
            {
                Timestamp = DateTime.Now.AddDays(-1),
                Operation = "卸载软件",
                Status = "成功",
                Details = "卸载了2个软件"
            });

            // 添加一些模拟的AI建议
            Recommendations.Add(new Recommendation
            {
                Action = "清理系统缓存",
                Reason = "清理系统和应用程序缓存，释放磁盘空间",
                Risk = "低风险",
                ExpectedImpact = "释放约1-2GB磁盘空间"
            });

            Recommendations.Add(new Recommendation
            {
                Action = "优化启动项",
                Reason = "禁用不必要的启动项，提升系统启动速度",
                Risk = "中风险",
                ExpectedImpact = "启动速度提升20-30%"
            });

            Recommendations.Add(new Recommendation
            {
                Action = "释放内存",
                Reason = "释放未使用的内存，提升系统响应速度",
                Risk = "低风险",
                ExpectedImpact = "内存使用率降低15-20%"
            });

            SendCommand = new AsyncRelayCommand(SendMessageAsync, () => !IsProcessing && !string.IsNullOrWhiteSpace(UserMessage));
            AcceptSuggestionCommand = new RelayCommand<Recommendation>(AcceptSuggestion, _ => !IsProcessing);
            DismissSuggestionCommand = new RelayCommand<Recommendation>(DismissSuggestion, _ => !IsProcessing);
            CleanCacheCommand = new AsyncRelayCommand(CleanCacheAsync, () => !IsProcessing);
            OptimizeSystemCommand = new AsyncRelayCommand(OptimizeSystemAsync, () => !IsProcessing);
            ShowUninstallCommand = new AsyncRelayCommand(ShowUninstallAsync, () => !IsProcessing);
            FreeMemoryCommand = new AsyncRelayCommand(FreeMemoryAsync, () => !IsProcessing);
            NavigateCommand = new RelayCommand<string>(Navigate);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            RefreshModelsCommand = new AsyncRelayCommand(LoadAvailableModelsAsync, () => !IsProcessing);

            _aiDecisionEngine.SystemStatusChanged += OnSystemStatusChanged;

            SystemHealthStatus = "良好";
            AIAnalysisStatus = "系统状态良好，无需优化";

            // 初始化系统状态数据
            _ = UpdateSystemStatusAsync();

            // 加载可用模型
            _ = LoadAvailableModelsAsync();

            // 设置定时器，每5秒更新一次系统状态
            _updateTimer = new Timer(5000);
            _updateTimer.Elapsed += async (sender, e) => await UpdateSystemStatusAsync();
            _updateTimer.Start();

            _logger.LogInformation("MainViewModel initialized");
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserMessage)) return;

            try
            {
                IsProcessing = true;
                SendCommand.NotifyCanExecuteChanged();
                AcceptSuggestionCommand.NotifyCanExecuteChanged();
                DismissSuggestionCommand.NotifyCanExecuteChanged();

                var userChatMessage = new ChatMessage { Role = "user", Content = UserMessage };
                ChatHistory.Add(userChatMessage);

                var message = UserMessage;
                UserMessage = string.Empty;

                AssistantMessage = string.Empty;
                var progress = new Progress<string>(UpdateAssistantMessage);
                var history = ChatHistory.ToList();
                await _ollamaService.ChatAsync(message, history, progress);

                var assistantChatMessage = new ChatMessage { Role = "assistant", Content = AssistantMessage };
                ChatHistory.Add(assistantChatMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                AssistantMessage = $"Sorry, an error occurred while processing your request: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                SendCommand.NotifyCanExecuteChanged();
                AcceptSuggestionCommand.NotifyCanExecuteChanged();
                DismissSuggestionCommand.NotifyCanExecuteChanged();
            }
        }

        private void UpdateAssistantMessage(string content)
        {
            AssistantMessage += content;
        }

        private async void AcceptSuggestion(Recommendation recommendation)
        {
            if (recommendation == null) return;

            try
            {
                IsProcessing = true;
                SendCommand.NotifyCanExecuteChanged();
                AcceptSuggestionCommand.NotifyCanExecuteChanged();
                DismissSuggestionCommand.NotifyCanExecuteChanged();

                _logger.LogInformation("Accepting AI suggestion: {Action}", recommendation.Action);

                await ExecuteToolBasedOnRecommendation(recommendation);

                Recommendations.Remove(recommendation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing suggestion");
            }
            finally
            {
                IsProcessing = false;
                SendCommand.NotifyCanExecuteChanged();
                AcceptSuggestionCommand.NotifyCanExecuteChanged();
                DismissSuggestionCommand.NotifyCanExecuteChanged();
            }
        }

        private void DismissSuggestion(Recommendation recommendation)
        {
            if (recommendation == null) return;

            _logger.LogInformation("Dismissing AI suggestion: {Action}", recommendation.Action);
            Recommendations.Remove(recommendation);
        }

        private async Task ExecuteToolBasedOnRecommendation(Recommendation recommendation)
        {
            if (recommendation.Action.Contains("clean", StringComparison.OrdinalIgnoreCase))
            {
                var cacheCleaner = _systemTools.FirstOrDefault(t => t.Name == "CacheCleaner");
                if (cacheCleaner != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await cacheCleaner.ExecuteAsync("{\"ThresholdDays\": 7, \"AutoDelete\": true}", progress);
                    _logger.LogInformation("Cache cleaning result: {Success}, {Message}", result.Success, result.Message);
                }
            }
            else if (recommendation.Action.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
            {
                var uninstallTool = _systemTools.FirstOrDefault(t => t.Name == "UninstallTool");
                if (uninstallTool != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await uninstallTool.ExecuteAsync("{\"SoftwareName\": \"unused\", \"ForceUninstall\": true}", progress);
                    _logger.LogInformation("Uninstall result: {Success}, {Message}", result.Success, result.Message);
                }
            }
            else if (recommendation.Action.Contains("optimize", StringComparison.OrdinalIgnoreCase))
            {
                var optimizerTool = _systemTools.FirstOrDefault(t => t.Name == "SystemOptimizer");
                if (optimizerTool != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await optimizerTool.ExecuteAsync("{\"OptimizeStartup\": true, \"FreeMemory\": true}", progress);
                    _logger.LogInformation("System optimization result: {Success}, {Message}", result.Success, result.Message);
                }
            }
        }

        private void OnSystemStatusChanged(object sender, SystemStatusEventArgs e)
        {
            try
            {
                if (e.Issues != null && e.Issues.Count > 0)
                {
                    var criticalIssues = e.Issues.Count(i => i.Severity == "Critical");
                    if (criticalIssues > 0)
                    {
                        SystemHealthStatus = "需要立即优化";
                        AIAnalysisStatus = "发现严重问题，需要立即处理";
                    }
                    else
                    {
                        SystemHealthStatus = "需要优化";
                        AIAnalysisStatus = "发现一些问题，建议进行优化";
                    }

                    if (e.Recommendations != null)
                    {
                        foreach (var recommendation in e.Recommendations)
                        {
                            if (!Recommendations.Any(r => r.Action.Equals(recommendation.Action, StringComparison.OrdinalIgnoreCase)))
                            {
                                Recommendations.Add(recommendation);
                            }
                        }
                    }
                }
                else
                {
                    SystemHealthStatus = "良好";
                    AIAnalysisStatus = "系统状态良好，无需优化";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling system status change");
            }
        }

        private async Task UpdateSystemStatusAsync()
        {
            try
            {
                // 获取系统状态快照
                var snapshot = await _systemMonitorService.GetSystemSnapshot();

                // 更新属性
                CpuUsage = snapshot.CpuUsage;
                MemoryUsage = snapshot.MemoryUsage;
                DiskUsage = snapshot.DiskUsage;

                // 根据系统状态更新健康状态
                UpdateSystemHealthStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system status");
            }
        }

        private void UpdateSystemHealthStatus()
        {
            // 根据CPU、内存和磁盘使用率判断系统健康状态
            if (CpuUsage > 90 || MemoryUsage > 90 || DiskUsage > 95)
            {
                SystemHealthStatus = "需要立即优化";
                AIAnalysisStatus = "发现严重问题，需要立即处理";
            }
            else if (CpuUsage > 70 || MemoryUsage > 70 || DiskUsage > 85)
            {
                SystemHealthStatus = "需要优化";
                AIAnalysisStatus = "发现一些问题，建议进行优化";
            }
            else
            {
                SystemHealthStatus = "良好";
                AIAnalysisStatus = "系统状态良好，无需优化";
            }
        }

        private void AddOperationHistory(string operation, string status, string details)
        {
            OperationHistory.Insert(0, new OperationHistory
            {
                Timestamp = DateTime.Now,
                Operation = operation,
                Status = status,
                Details = details
            });

            // 限制历史记录数量，最多保留100条
            if (OperationHistory.Count > 100)
            {
                OperationHistory.RemoveAt(OperationHistory.Count - 1);
            }
        }

        private async Task CleanCacheAsync()
        {
            try
            {
                IsProcessing = true;
                _logger.LogInformation("Starting cache cleaning");

                var cacheCleaner = _systemTools.FirstOrDefault(t => t.Name == "CacheCleaner");
                if (cacheCleaner != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await cacheCleaner.ExecuteAsync("{\"ThresholdDays\": 7, \"AutoDelete\": true}", progress);
                    _logger.LogInformation("Cache cleaning result: {Success}, {Message}", result.Success, result.Message);
                    SystemHealthStatus = "缓存清理完成: " + result.Message;
                    
                    // 添加操作历史记录
                    AddOperationHistory("清理系统缓存", result.Success ? "成功" : "失败", result.Message);
                }
                else
                {
                    SystemHealthStatus = "缓存清理工具未找到";
                    AddOperationHistory("清理系统缓存", "失败", "缓存清理工具未找到");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning cache");
                SystemHealthStatus = $"缓存清理失败: {ex.Message}";
                AddOperationHistory("清理系统缓存", "失败", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task OptimizeSystemAsync()
        {
            try
            {
                IsProcessing = true;
                _logger.LogInformation("Starting system optimization");

                var optimizerTool = _systemTools.FirstOrDefault(t => t.Name == "SystemOptimizer");
                if (optimizerTool != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await optimizerTool.ExecuteAsync("{\"OptimizeStartup\": true, \"FreeMemory\": true}", progress);
                    _logger.LogInformation("System optimization result: {Success}, {Message}", result.Success, result.Message);
                    SystemHealthStatus = "系统优化完成: " + result.Message;
                    
                    // 添加操作历史记录
                    AddOperationHistory("优化系统", result.Success ? "成功" : "失败", result.Message);
                }
                else
                {
                    SystemHealthStatus = "系统优化工具未找到";
                    AddOperationHistory("优化系统", "失败", "系统优化工具未找到");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing system");
                SystemHealthStatus = $"系统优化失败: {ex.Message}";
                AddOperationHistory("优化系统", "失败", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ShowUninstallAsync()
        {
            try
            {
                IsProcessing = true;
                _logger.LogInformation("Showing uninstall tool");

                var uninstallTool = _systemTools.FirstOrDefault(t => t.Name == "UninstallTool");
                if (uninstallTool != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await uninstallTool.ExecuteAsync("{\"SoftwareName\": \"unused\", \"ForceUninstall\": true}", progress);
                    _logger.LogInformation("Uninstall tool result: {Success}, {Message}", result.Success, result.Message);
                    SystemHealthStatus = "软件卸载完成: " + result.Message;
                    
                    // 添加操作历史记录
                    AddOperationHistory("软件卸载", result.Success ? "成功" : "失败", result.Message);
                }
                else
                {
                    SystemHealthStatus = "软件卸载工具未找到";
                    AddOperationHistory("软件卸载", "失败", "软件卸载工具未找到");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing uninstall tool");
                SystemHealthStatus = $"软件卸载失败: {ex.Message}";
                AddOperationHistory("软件卸载", "失败", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task FreeMemoryAsync()
        {
            try
            {
                IsProcessing = true;
                _logger.LogInformation("Starting memory freeing");

                var optimizerTool = _systemTools.FirstOrDefault(t => t.Name == "SystemOptimizer");
                if (optimizerTool != null)
                {
                    var progress = new Progress<string>(UpdateAssistantMessage);
                    var result = await optimizerTool.ExecuteAsync("{\"FreeMemory\": true}", progress);
                    _logger.LogInformation("Memory freeing result: {Success}, {Message}", result.Success, result.Message);
                    SystemHealthStatus = "内存释放完成: " + result.Message;
                    
                    // 添加操作历史记录
                    AddOperationHistory("释放内存", result.Success ? "成功" : "失败", result.Message);
                }
                else
                {
                    SystemHealthStatus = "内存释放工具未找到";
                    AddOperationHistory("释放内存", "失败", "内存释放工具未找到");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freeing memory");
                SystemHealthStatus = $"内存释放失败: {ex.Message}";
                AddOperationHistory("释放内存", "失败", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void Navigate(string navItem)
        {
            _logger.LogInformation($"Navigating to: {navItem}");
            SelectedNavItem = navItem;
        }

        private void SaveSettings()
        {
            try
            {
                _logger.LogInformation("Saving settings");
                _logger.LogInformation("Ollama API URL: {OllamaApiUrl}", OllamaApiUrl);
                _logger.LogInformation("Default Model: {DefaultModel}", DefaultModel);
                _logger.LogInformation("Auto System Analysis: {AutoSystemAnalysis}", AutoSystemAnalysis);
                _logger.LogInformation("Optimization Notification: {OptimizationNotification}", OptimizationNotification);

                // 这里可以添加将设置保存到文件或数据库的逻辑
                // 目前只是记录日志

                SystemHealthStatus = "设置已保存";
                AddOperationHistory("保存设置", "成功", "设置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                SystemHealthStatus = $"保存设置失败: {ex.Message}";
                AddOperationHistory("保存设置", "失败", ex.Message);
            }
        }

        private async Task LoadAvailableModelsAsync()
        {
            try
            {
                IsProcessing = true;
                _logger.LogInformation("Loading available models from Ollama API");

                var models = await _ollamaService.GetAvailableModelsAsync();
                
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model);
                }

                // 如果没有模型，添加一些默认模型
                if (AvailableModels.Count == 0)
                {
                    AvailableModels.Add("qwen2.5:3b");
                    AvailableModels.Add("llama2");
                    AvailableModels.Add("llama3");
                    AvailableModels.Add("gemma:2b");
                    AvailableModels.Add("mistral");
                    AvailableModels.Add("phi3");
                    AvailableModels.Add("dolphin-llama3");
                    AvailableModels.Add("neural-chat");
                }

                // 确保默认模型在列表中
                if (!string.IsNullOrEmpty(DefaultModel) && !AvailableModels.Contains(DefaultModel))
                {
                    AvailableModels.Add(DefaultModel);
                }

                _logger.LogInformation("Loaded {Count} models", AvailableModels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available models");
                
                // 出错时添加一些默认模型
                AvailableModels.Clear();
                AvailableModels.Add("qwen2.5:3b");
                AvailableModels.Add("llama2");
                AvailableModels.Add("llama3");
                AvailableModels.Add("gemma:2b");
                AvailableModels.Add("mistral");
                AvailableModels.Add("phi3");
                AvailableModels.Add("dolphin-llama3");
                AvailableModels.Add("neural-chat");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}