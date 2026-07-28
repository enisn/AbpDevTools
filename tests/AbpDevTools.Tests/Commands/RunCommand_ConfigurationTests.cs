using AbpDevTools.Commands;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Exceptions;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Commands;

public class RunCommand_ConfigurationTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly TestRunCommand _command;

    public RunCommand_ConfigurationTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_RunConfig_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        var environmentManager = Substitute.For<IProcessEnvironmentManager>();
        var fileExplorer = new FileExplorer();
        var localConfigurationManager = new LocalConfigurationManager(
            new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build(),
            new SerializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build(),
            fileExplorer,
            environmentManager);

        _command = new TestRunCommand(localConfigurationManager, environmentManager, fileExplorer);
    }

    [Fact]
    public void TryLoadRootConfiguration_WithoutYmlOption_LoadsNearestAncestor()
    {
        var workspacePath = Path.Combine(_testRootPath, "workspace");
        var workingDirectory = Path.Combine(workspacePath, "src", "service");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(_testRootPath, "abpdev.yml"), "run:\n  configuration: Debug\n");
        var nearestConfigurationPath = Path.Combine(workspacePath, "abpdev.yml");
        File.WriteAllText(nearestConfigurationPath, "run:\n  configuration: Release\n");

        _command.WorkingDirectory = workingDirectory;

        var result = _command.InvokeTryLoadRootConfiguration(out var configuration, out var loadedPath);

        result.Should().BeTrue();
        configuration!.Run!.Configuration.Should().Be("Release");
        loadedPath.Should().Be(Path.GetFullPath(nearestConfigurationPath));
        _command.YmlPath.Should().Be(loadedPath);
    }

    [Fact]
    public void TryLoadRootConfiguration_WithYmlOption_LoadsExactFile()
    {
        var workingDirectory = Path.Combine(_testRootPath, "workspace", "src");
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllText(Path.Combine(workingDirectory, "abpdev.yml"), "run:\n  configuration: Debug\n");

        var explicitConfigurationPath = Path.Combine(_testRootPath, "profiles", "release.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(explicitConfigurationPath)!);
        File.WriteAllText(explicitConfigurationPath, "run:\n  configuration: Release\n");

        _command.WorkingDirectory = workingDirectory;
        _command.YmlPath = explicitConfigurationPath;

        var result = _command.InvokeTryLoadRootConfiguration(out var configuration, out var loadedPath);

        result.Should().BeTrue();
        configuration!.Run!.Configuration.Should().Be("Release");
        loadedPath.Should().Be(Path.GetFullPath(explicitConfigurationPath));
    }

    [Fact]
    public void TryLoadRootConfiguration_WithMissingYmlOption_DoesNotSearchParents()
    {
        var workingDirectory = Path.Combine(_testRootPath, "workspace", "src");
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllText(Path.Combine(_testRootPath, "custom.yml"), "run:\n  configuration: Release\n");

        _command.WorkingDirectory = workingDirectory;
        _command.YmlPath = Path.Combine(workingDirectory, "custom.yml");

        var act = () => _command.InvokeTryLoadRootConfiguration(out _, out _);

        act.Should().Throw<CommandException>()
            .WithMessage("*custom.yml*was not found*");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    [CliFx.Attributes.Command("test-run-configuration-command")]
    private sealed class TestRunCommand : RunCommand
    {
        public TestRunCommand(
            LocalConfigurationManager localConfigurationManager,
            IProcessEnvironmentManager environmentManager,
            FileExplorer fileExplorer)
            : base(
                Substitute.For<INotificationManager>(),
                null!,
                environmentManager,
                null!,
                null!,
                null!,
                fileExplorer,
                localConfigurationManager,
                Substitute.For<IKeyInputManager>())
        {
        }

        public bool InvokeTryLoadRootConfiguration(
            out LocalConfiguration? localConfiguration,
            out string? loadedPath)
        {
            return TryLoadRootConfiguration(out localConfiguration, out loadedPath);
        }
    }
}
