using AbpDevTools.Commands.Migrations;
using CliFx.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands.Migrations;

public class MigrationsCommandBaseTests
{
    [Fact]
    public async Task ChooseProjectsAsync_WithSingleProject_SkipsPrompt()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = new FileInfo(Path.Combine(workingDirectory, "MyApp.EntityFrameworkCore.csproj"));
        var command = new TestMigrationsCommand(new[] { project })
        {
            WorkingDirectory = workingDirectory
        };

        var selectedProjects = await command.InvokeChooseProjectsAsync();

        selectedProjects.Should().ContainSingle().Which.FullName.Should().Be(project.FullName);
        command.PromptCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ChooseProjectsAsync_WithMultipleProjects_UsesPromptSelection()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var firstProject = new FileInfo(Path.Combine(workingDirectory, "First.EntityFrameworkCore.csproj"));
        var secondProject = new FileInfo(Path.Combine(workingDirectory, "Second.EntityFrameworkCore.csproj"));
        var command = new TestMigrationsCommand(new[] { firstProject, secondProject }, projects => new[] { projects[1] })
        {
            WorkingDirectory = workingDirectory
        };

        var selectedProjects = await command.InvokeChooseProjectsAsync();

        selectedProjects.Should().ContainSingle().Which.FullName.Should().Be(secondProject.FullName);
        command.PromptCallCount.Should().Be(1);
    }

    [Fact]
    public void GetProjectSelectionLabel_WithProjectInWorkingDirectory_ReturnsProjectFileName()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = new FileInfo(Path.Combine(workingDirectory, "MyApp.EntityFrameworkCore.csproj"));
        var command = new TestMigrationsCommand(Array.Empty<FileInfo>())
        {
            WorkingDirectory = workingDirectory
        };

        var label = command.InvokeGetProjectSelectionLabel(project);

        label.Should().Be("MyApp.EntityFrameworkCore.csproj");
    }

    [Fact]
    public void GetProjectSelectionLabel_WithNestedProject_ReturnsRelativeProjectPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = new FileInfo(Path.Combine(workingDirectory, "src", "MyApp.EntityFrameworkCore", "MyApp.EntityFrameworkCore.csproj"));
        var command = new TestMigrationsCommand(Array.Empty<FileInfo>())
        {
            WorkingDirectory = workingDirectory
        };

        var label = command.InvokeGetProjectSelectionLabel(project);

        label.Should().Be(Path.Combine("src", "MyApp.EntityFrameworkCore", "MyApp.EntityFrameworkCore.csproj"));
    }

    [CliFx.Attributes.Command("test-migrations-command")]
    private sealed class TestMigrationsCommand : MigrationsCommandBase
    {
        private readonly FileInfo[] projectFiles;
        private readonly Func<FileInfo[], FileInfo[]> promptSelection;

        public TestMigrationsCommand(FileInfo[] projectFiles, Func<FileInfo[], FileInfo[]>? promptSelection = null)
            : base(null!)
        {
            this.projectFiles = projectFiles;
            this.promptSelection = promptSelection ?? (projects => projects);
        }

        public int PromptCallCount { get; private set; }

        public Task<FileInfo[]> InvokeChooseProjectsAsync()
        {
            return ChooseProjectsAsync();
        }

        public string InvokeGetProjectSelectionLabel(FileInfo projectFile)
        {
            return GetProjectSelectionLabel(projectFile);
        }

        public override ValueTask ExecuteAsync(IConsole console)
        {
            return default;
        }

        protected override Task<FileInfo[]> GetEfCoreProjectsAsync()
        {
            return Task.FromResult(projectFiles);
        }

        protected override FileInfo[] PromptForProjectSelection(FileInfo[] projectFiles)
        {
            PromptCallCount++;
            return promptSelection(projectFiles);
        }
    }
}
