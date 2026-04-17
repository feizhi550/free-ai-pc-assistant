using System.Collections.Generic;

namespace AIPCAssistant.Models
{
    public class AppSettings
    {
        public int AutomationLevel { get; set; } = 1;
        public OllamaSettings Ollama { get; set; } = new OllamaSettings();
        public MonitorSettings Monitor { get; set; } = new MonitorSettings();
        public WhitelistSettings Whitelist { get; set; } = new WhitelistSettings();
        public CleanerSettings Cleaner { get; set; } = new CleanerSettings();
    }

    public class OllamaSettings
    {
        public string ApiUrl { get; set; } = "http://localhost:11434/api";
        public string DefaultModel { get; set; } = "llama3";
        public int TimeoutMs { get; set; } = 30000;
        public int RetryCount { get; set; } = 3;
    }

    public class MonitorSettings
    {
        public int IntervalMinutes { get; set; } = 30;
        public float CpuThreshold { get; set; } = 80;
        public float MemoryThreshold { get; set; } = 85;
        public float DiskThreshold { get; set; } = 90;
    }

    public class WhitelistSettings
    {
        public List<string> ProtectedApplications { get; set; } = new List<string>();
        public List<string> ProtectedServices { get; set; } = new List<string>();
        public List<string> ProtectedRegistryKeys { get; set; } = new List<string>();
    }

    public class CleanerSettings
    {
        public int CacheExpirationDays { get; set; } = 7;
        public int IsolationRetentionDays { get; set; } = 7;
        public long MaxCleanSizeMb { get; set; } = 5000;
        public bool ConfirmBeforeClean { get; set; } = true;
    }
}