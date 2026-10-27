using System.Diagnostics;
using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Commands;

// Spectre.Console status displays are process-wide; keep these command executions
// separate from other tests that use the static console.
[CollectionDefinition("Database drop console", DisableParallelization = true)]
public class DatabaseDropConsoleCollection { }

[Collection("Database drop console")]
public class DatabaseDropCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "drop-guard-" + Guid.NewGuid());
    private readonly IProcessEnvironmentManager environment = Substitute.For<IProcessEnvironmentManager>();
    private readonly DesignTimeEnvironmentGuard guard = Substitute.For<DesignTimeEnvironmentGuard>();
    private readonly TestCommand command;

    public DatabaseDropCommandTests()
    {
        Directory.CreateDirectory(root);
        var tools = Substitute.For<ToolsConfiguration>(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>());
        tools.GetOptions().Returns(new ToolOption { ["dotnet"] = "dotnet" });
        var resolver = new DotnetDependencyResolver(tools);
        command = new TestCommand(tools, resolver, environment, guard) { WorkingDirectory = root };
    }

    private void Project(string name, bool package = true)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, name));
        File.WriteAllText(Path.Combine(directory.FullName, name + ".csproj"),
            "<Project><ItemGroup><PackageReference Include=\"Microsoft.EntityFrameworkCore.Tools\" Version=\"10.0.0\" />" +
            (package ? "<PackageReference Include=\"Selected\" Version=\"1.0.0\" />" : "") + "</ItemGroup></Project>");
    }

    [Fact]
    public async Task NoEnvironmentSkipsGuard()
    {
        Project("A");
        await command.ExecuteAsync(new FakeInMemoryConsole());
        Assert.Single(command.Starts);
        guard.DidNotReceive().Inspect(Arg.Any<FileInfo>());
        environment.DidNotReceive().SetEnvironmentForProcess(Arg.Any<string>(), Arg.Any<ProcessStartInfo>());
    }

    [Theory]
    [InlineData(EnvironmentSupport.Supported)]
    [InlineData(EnvironmentSupport.Unresolved)]
    public async Task SupportedAndUnresolvedContinueWithForceAndPackage(EnvironmentSupport support)
    {
        Project("A"); Project("Excluded", false);
        command.EnvironmentName = "dev"; command.Force = true; command.PackageFilter = "Selected";
        guard.Inspect(Arg.Any<FileInfo>()).Returns(new EnvironmentSupportResult(support, "Factory.cs / Factory"));
        var console = new FakeInMemoryConsole();
        await command.ExecuteAsync(console);
        Assert.Single(command.Starts);
        Assert.Equal("ef database drop --force", command.Starts[0].Arguments);
        Assert.Equal(support == EnvironmentSupport.Unresolved, console.ReadErrorString().Contains("!!! WARNING"));
        if (support == EnvironmentSupport.Unresolved)
        {
            var stderr = console.ReadErrorString();
            Assert.Contains("could not verify AddEnvironmentVariables()", stderr);
            Assert.Contains("The selected virtual environment may not affect the effective EF Core connection string", stderr);
        }
    }

    [Fact]
    public async Task AllProjectsPreflightBeforeAnyDropEvenWhenOneIsMissing()
    {
        Project("A"); Project("B"); Project("C");
        command.EnvironmentName = "dev";
        guard.Inspect(Arg.Any<FileInfo>()).Returns(c => new EnvironmentSupportResult(
            ((FileInfo)c[0]).Name == "B.csproj" ? EnvironmentSupport.Missing : EnvironmentSupport.Unresolved, "Factory.cs"));
        var console = new FakeInMemoryConsole();
        var error = await Assert.ThrowsAsync<CommandException>(async () => await command.ExecuteAsync(console));
        Assert.Contains("AddEnvironmentVariables()", error.Message);
        Assert.Contains("ConnectionStrings__Default", error.Message);
        Assert.Contains("No databases were dropped", error.Message);
        Assert.Empty(command.Starts);
        Assert.Empty(console.ReadErrorString());
        guard.Received(3).Inspect(Arg.Any<FileInfo>());
    }

    [Fact]
    public async Task AllChecksFinishBeforeFirstLaunch()
    {
        Project("A"); Project("B"); command.EnvironmentName = "dev";
        var checkedCount = 0;
        guard.Inspect(Arg.Any<FileInfo>()).Returns(_ => { checkedCount++; return new(EnvironmentSupport.Supported, "Factory.cs"); });
        command.BeforeDrop = () => Assert.Equal(2, checkedCount);
        await command.ExecuteAsync(new FakeInMemoryConsole());
        Assert.Equal(2, command.Starts.Count);
    }

    [Fact]
    public async Task UnknownEnvironmentKeepsExistingErrorAndLaunchesNothing()
    {
        Project("A"); command.EnvironmentName = "unknown";
        environment.When(e => e.SetEnvironmentForProcess("unknown", Arg.Any<ProcessStartInfo>()))
            .Do(_ => throw new CommandException("Environment not found!"));
        var error = await Assert.ThrowsAsync<CommandException>(async () => await command.ExecuteAsync(new FakeInMemoryConsole()));
        Assert.Equal("Environment not found!", error.Message);
        Assert.Empty(command.Starts);
        guard.DidNotReceive().Inspect(Arg.Any<FileInfo>());
    }

    public void Dispose() => Directory.Delete(root, true);

    [CliFx.Attributes.Command("test-drop")]
    private class TestCommand : DatabaseDropCommand
    {
        public List<ProcessStartInfo> Starts { get; } = new();
        public Action? BeforeDrop { get; set; }
        public TestCommand(ToolsConfiguration tools, DotnetDependencyResolver resolver, IProcessEnvironmentManager env, DesignTimeEnvironmentGuard guard)
            : base(Substitute.For<INotificationManager>(), tools, new EntityFrameworkCoreProjectsProvider(resolver), resolver, env, guard) { }
        protected override Task DropDatabaseAsync(ProcessStartInfo start, IConsole console, CancellationToken token)
        { BeforeDrop?.Invoke(); Starts.Add(start); return Task.CompletedTask; }
    }
}
