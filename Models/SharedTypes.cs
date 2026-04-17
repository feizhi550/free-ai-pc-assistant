using System;
using System.Collections.Generic;

namespace AIPCAssistant.Models
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class SystemStatusSnapshot
    {
        public float CpuUsage { get; set; }
        public float MemoryUsage { get; set; }
        public float DiskUsage { get; set; }
        public long TempFilesCount { get; set; }
        public long TempFilesSize { get; set; }
        public List<string> RecentlyInstalledSoftware { get; set; }
        public List<string> RunningProcesses { get; set; }
        public int StartupItemsCount { get; set; }
    }

    public class SystemStatusEventArgs : EventArgs
    {
        public List<Issue> Issues { get; set; }
        public List<Recommendation> Recommendations { get; set; }
    }

    public class AnalysisResult
    {
        public List<Issue> Issues { get; set; }
        public List<Recommendation> Recommendations { get; set; }
    }

    public class Issue
    {
        public string Description { get; set; }
        public string Severity { get; set; }
        public string Risk { get; set; }
    }

    public class Recommendation
    {
        public string Action { get; set; }
        public string Reason { get; set; }
        public string Risk { get; set; }
        public string ExpectedImpact { get; set; }
    }
}