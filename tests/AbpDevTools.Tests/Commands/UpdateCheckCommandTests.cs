using System.Runtime.InteropServices;
using AbpDevTools.Commands;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

public class UpdateCheckCommandTests
{
    [Fact]
    public void CreateUpdateProcessStartInfo_Should_Not_Use_Shell_Execute()
    {
        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345, "custom-pwsh", "custom-dotnet");

        startInfo.UseShellExecute.Should().BeFalse();
    }

    [Fact]
    public void CreateUpdateProcessStartInfo_Should_Wait_For_Current_Process_Before_Update()
    {
        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345, "custom-pwsh", "custom-dotnet");
        var arguments = GetArguments(startInfo);

        arguments.Should().Contain("12345");
        arguments.Should().Contain("custom-dotnet");
        arguments.Should().Contain("tool update -g AbpDevTools");
    }

    [Fact]
    public void CreateUpdateProcessStartInfo_On_Windows_Should_Use_Configured_PowerShell()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345, "custom-pwsh", "custom-dotnet");
        var arguments = GetArguments(startInfo);

        startInfo.FileName.Should().Be("custom-pwsh");
        arguments.Should().Contain("Wait-Process -Id 12345");
        arguments.Should().NotContain("ExecutionPolicy");
    }

    private static string GetArguments(System.Diagnostics.ProcessStartInfo startInfo)
    {
        return startInfo.ArgumentList.Count > 0
            ? string.Join(" ", startInfo.ArgumentList)
            : startInfo.Arguments;
    }
}
