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
        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345);

        startInfo.UseShellExecute.Should().BeFalse();
    }

    [Fact]
    public void CreateUpdateProcessStartInfo_Should_Wait_For_Current_Process_Before_Update()
    {
        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345);

        startInfo.Arguments.Should().Contain("12345");
        startInfo.Arguments.Should().Contain("dotnet tool update -g AbpDevTools");
    }

    [Fact]
    public void CreateUpdateProcessStartInfo_On_Windows_Should_Start_PowerShell_Directly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var startInfo = UpdateCheckCommand.CreateUpdateProcessStartInfo(12345);

        startInfo.FileName.Should().Be("powershell.exe");
        startInfo.Arguments.Should().Contain("Wait-Process -Id 12345");
    }
}
