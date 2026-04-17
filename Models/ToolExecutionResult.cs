using System;

namespace AIPCAssistant.Models
{
    public class ToolExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public Exception Exception { get; set; }
        public long AffectedDataSize { get; set; }
        public long ExecutionTimeMs { get; set; }

        public static ToolExecutionResult CreateSuccess(string message, string details = null, long affectedDataSize = 0, long executionTimeMs = 0)
        {
            return new ToolExecutionResult
            {
                Success = true,
                Message = message,
                Details = details,
                AffectedDataSize = affectedDataSize,
                ExecutionTimeMs = executionTimeMs
            };
        }

        public static ToolExecutionResult CreateFailure(string message, Exception exception = null, string details = null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Message = message,
                Exception = exception,
                Details = details
            };
        }
    }
}