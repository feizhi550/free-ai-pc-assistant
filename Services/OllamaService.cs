using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Services
{
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private readonly string _ollamaApiUrl;
        private readonly string _defaultModel;
        private readonly int _timeoutMs;
        private readonly int _retryCount;

        public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger, AppSettings appSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _ollamaApiUrl = appSettings.Ollama.ApiUrl;
            _defaultModel = appSettings.Ollama.DefaultModel;
            _timeoutMs = appSettings.Ollama.TimeoutMs;
            _retryCount = appSettings.Ollama.RetryCount;

            _httpClient.BaseAddress = new Uri(_ollamaApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);
        }

        public async Task<string> ChatAsync(string userPrompt, List<ChatMessage> history, IProgress<string> progress = null)
        {
            int retryCount = 0;
            while (retryCount <= _retryCount)
            {
                try
                {
                    _logger.LogInformation("Starting chat with Ollama (Attempt {RetryCount}/{MaxRetry})", retryCount, _retryCount);

                    var messages = new List<ChatMessage>(history);
                    messages.Add(new ChatMessage { Role = "user", Content = userPrompt });

                    var requestBody = new
                    {
                        model = _defaultModel,
                        messages = messages,
                        stream = true
                    };

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("/chat", content);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new System.IO.StreamReader(stream);

                    var fullResponse = new StringBuilder();
                    string line;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            var responseObj = JsonSerializer.Deserialize<JsonElement>(line);
                            if (responseObj.TryGetProperty("message", out var messageObj) &&
                                messageObj.TryGetProperty("content", out var contentObj))
                            {
                                var contentValue = contentObj.GetString();
                                if (!string.IsNullOrEmpty(contentValue))
                                {
                                    fullResponse.Append(contentValue);
                                    progress?.Report(contentValue);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error parsing Ollama response");
                        }
                    }

                    var result = fullResponse.ToString();
                    _logger.LogInformation("Chat completed, response length: {Length}", result.Length);
                    return result;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    _logger.LogError(ex, "Ollama service is not running");
                    retryCount++;
                    if (retryCount > _retryCount)
                    {
                        return GetLocalRuleResponse(userPrompt);
                    }
                    await Task.Delay(1000 * retryCount);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Error communicating with Ollama");
                    retryCount++;
                    if (retryCount > _retryCount)
                    {
                        return GetLocalRuleResponse(userPrompt);
                    }
                    await Task.Delay(1000 * retryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during chat");
                    retryCount++;
                    if (retryCount > _retryCount)
                    {
                        return GetLocalRuleResponse(userPrompt);
                    }
                    await Task.Delay(1000 * retryCount);
                }
            }

            return GetLocalRuleResponse(userPrompt);
        }

        private string GetLocalRuleResponse(string userPrompt)
        {
            _logger.LogInformation("Ollama service unavailable, using local rule mode");

            if (userPrompt.Contains("clean", StringComparison.OrdinalIgnoreCase))
            {
                return "I see you need to clean your system. Since AI service is temporarily unavailable, I will use local rule mode to help you. You can view cleanable items in the Optimization Suggestions page.";
            }
            else if (userPrompt.Contains("optim", StringComparison.OrdinalIgnoreCase))
            {
                return "I see you need to optimize your system. Since AI service is temporarily unavailable, I will use local rule mode to help you. You can view optimization suggestions in the System Health page.";
            }
            else if (userPrompt.Contains("status", StringComparison.OrdinalIgnoreCase))
            {
                return "I see you want to check system status. Since AI service is temporarily unavailable, you can view current system status in the System Health page.";
            }
            else
            {
                return "Sorry, AI service is temporarily unavailable. I have switched to local rule mode. You can still use basic system maintenance functions. Please try again later.";
            }
        }

        public async Task<string> AnalyzeSystemStateAsync(SystemStatusSnapshot snapshot)
        {
            try
            {
                _logger.LogInformation("Starting system state analysis");

                var prompt = $@"As a system optimization expert, please analyze the following system status snapshot and identify potential issues with optimization suggestions:

CPU Usage: {snapshot.CpuUsage}%
Memory Usage: {snapshot.MemoryUsage}%
Disk Usage: {snapshot.DiskUsage}%
Temp Files Count: {snapshot.TempFilesCount}
Temp Files Size: {snapshot.TempFilesSize / (1024 * 1024)}MB
Recently Installed Software: {string.Join(", ", snapshot.RecentlyInstalledSoftware)}
Running Processes Count: {snapshot.RunningProcesses.Count}
Startup Items Count: {snapshot.StartupItemsCount}

Please provide specific optimization suggestions including:
1. Identified issues
2. Recommended solutions
3. Expected optimization effects
4. Risk level (Low/Medium/High)

Please return analysis results in JSON format with the following fields:
{{
  ""issues"": [{{""description"": ""Issue description"", ""severity"": ""Severity"", ""risk"": ""Risk level""}}],
  ""recommendations"": [{{""action"": ""Recommended action"", ""reason"": ""Reason"", ""risk"": ""Risk level"", ""expected_impact"": ""Expected impact""}}]
}}";

                var history = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = "You are a professional system optimization expert, skilled at analyzing system status and providing specific optimization suggestions. Please return analysis results in JSON format." }
                };

                var result = await ChatAsync(prompt, history);
                _logger.LogInformation("System state analysis completed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing system state");
                throw new Exception("System state analysis failed", ex);
            }
        }

        public async Task<string> GenerateFunctionCallAsync(string userPrompt)
        {
            try
            {
                _logger.LogInformation("Starting function call generation");

                var prompt = $@"Based on user request, generate appropriate function call instructions. User request: {userPrompt}

Please return function call instructions in JSON format with the following fields:
{{
  ""tool_name"": ""Tool name"",
  ""parameters"": {{
    ""parameter_name"": ""parameter_value""
  }}
}}";

                var history = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = "You are a function call generator. Based on user requests, generate appropriate function call instructions. Please strictly return results in JSON format without any other text." }
                };

                var result = await ChatAsync(prompt, history);
                _logger.LogInformation("Function call generation completed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating function call");
                throw new Exception("Function call generation failed", ex);
            }
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                _logger.LogInformation("Getting available models from Ollama API");

                var response = await _httpClient.GetAsync("/api/tags");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get models: {StatusCode}", response.StatusCode);
                    return new List<string>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);

                var models = new List<string>();
                if (jsonResponse.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameElement))
                        {
                            var modelName = nameElement.GetString();
                            if (!string.IsNullOrEmpty(modelName))
                            {
                                models.Add(modelName);
                            }
                        }
                    }
                }

                _logger.LogInformation("Retrieved {Count} models from Ollama API", models.Count);
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<string>();
            }
        }
    }
}