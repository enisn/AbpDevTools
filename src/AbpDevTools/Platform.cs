using System.Diagnostics;
using System.Runtime.InteropServices;
using AbpDevTools.Configuration;

namespace AbpDevTools;

[RegisterTransient]
public class Platform
{
    private readonly ToolsConfiguration _toolsConfiguration;

    public Platform(ToolsConfiguration toolsConfiguration)
    {
        _toolsConfiguration = toolsConfiguration;
    }

    public virtual void Open(string filePath)
    {
        OpenProcess(filePath).WaitForExit();
    }

    public virtual Task OpenAsync(string filePath)
    {
        return OpenProcess(filePath).WaitForExitAsync();
    }

    private Process OpenProcess(string filePath)
    {
        var openTool = GetOpenTool();

        var process = Process.Start(new ProcessStartInfo(openTool, filePath));

        if (process == null)
        {
            throw new InvalidOperationException($"Failed to open file: {filePath}");
        }

        return process;
    }

    protected virtual string GetOpenTool()
    {
        var tools = _toolsConfiguration.GetOptions();
        if (tools.TryGetValue("open", out var tool))
        {
            return tool;
        }
        
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "explorer" : "open";
    }
}
