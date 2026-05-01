using System.Diagnostics;

namespace ClipNest.Services;

public sealed class SourceAppService
{
    public string GetSourceApp()
    {
        try
        {
            var processId = NativeMethods.GetForegroundProcessId();
            if (processId == 0)
            {
                return "Unknown";
            }

            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.ProcessName) ? "Unknown" : process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
}
