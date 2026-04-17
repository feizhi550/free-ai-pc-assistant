using System;
using System.Threading.Tasks;

namespace AIPCAssistant.Models
{
    public interface ISystemTool
    {
        string Name { get; }
        string Description { get; }
        string RequestType { get; }
        Task<ToolExecutionResult> ExecuteAsync(string jsonArguments, IProgress<string> progress);
    }
}