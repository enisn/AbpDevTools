using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for PrepareCommand dependency detection logic.
/// Tests detection of EF Core, Redis, MongoDB dependencies and environment app mapping.
/// </summary>
public class PrepareCommand_DependencyDetectionTests : CommandTestBase
{
    private readonly string _testRootPath;

    public PrepareCommand_DependencyDetectionTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
    }

    public override void Dispose()
    {
        base.Dispose();

        // Clean up test directory
        try
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region EF Core Dependency Detection Tests

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithEfCoreSqlServerReference_ReturnsSqlServerMapping()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult("Volo.Abp.EntityFrameworkCore.SqlServer", true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.SqlServer"), mockDependencyChecker);

        // Assert
        result.Should().NotBeEmpty("project has EF Core SQL Server dependency");
        result.Should().ContainSingle(
            m => m.AppName == EnvironmentAppConfiguration.SqlServerEdge,
            "should detect SQL Server dependency");
    }

    [Theory]
    [InlineData("Volo.Abp.EntityFrameworkCore.SqlServer", EnvironmentAppConfiguration.SqlServerEdge)]
    [InlineData("Volo.Abp.EntityFrameworkCore.MySQL", EnvironmentAppConfiguration.MySql)]
    [InlineData("Volo.Abp.EntityFrameworkCore.PostgreSql", EnvironmentAppConfiguration.PostgreSql)]
    public async Task CheckEnvironmentAppsAsync_WithEfCoreProvider_ReturnsCorrectEnvironmentApp(
        string packageName,
        string expectedAppName)
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult(packageName, true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject($"TestProject.{Guid.NewGuid()}"), mockDependencyChecker);

        // Assert
        result.Should().ContainSingle(m => m.AppName == expectedAppName,
            $"should map {packageName} to {expectedAppName}");
    }

    #endregion

    #region Redis Dependency Detection Tests

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithRedisReference_ReturnsRedisMapping()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult("Volo.Abp.Caching.StackExchangeRedis", true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.Redis"), mockDependencyChecker);

        // Assert
        result.Should().NotBeEmpty("project has Redis dependency");
        result.Should().ContainSingle(
            m => m.AppName == EnvironmentAppConfiguration.Redis,
            "should detect Redis dependency");
    }

    #endregion

    #region MongoDB Dependency Detection Tests

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithMongoDbReference_ReturnsMongoDbMapping()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult("Volo.Abp.MongoDB", true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.MongoDb"), mockDependencyChecker);

        // Assert
        result.Should().NotBeEmpty("project has MongoDB dependency");
        result.Should().ContainSingle(
            m => m.AppName == EnvironmentAppConfiguration.MongoDb,
            "should detect MongoDB dependency");
    }

    #endregion

    #region HasAnyDependency Tests

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithDependencies_ReturnsNonEmptyList()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetAllDependenciesTo(true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.WithDeps"), mockDependencyChecker);

        // Assert
        result.Should().NotBeEmpty("project has dependencies");
        result.Count.Should().BeGreaterThan(0, "should have at least one dependency");
    }

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithNoDependencies_ReturnsEmptyList()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetAllDependenciesTo(false);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.NoDeps"), mockDependencyChecker);

        // Assert
        result.Should().BeEmpty("project has no dependencies");
    }

    #endregion

    #region GetRequiredEnvironmentApps Tests

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithMultipleDeps_ReturnsAllEnvironmentApps()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult("Volo.Abp.EntityFrameworkCore.SqlServer", true);
        mockDependencyChecker.SetDependencyResult("Volo.Abp.Caching.StackExchangeRedis", true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.Multiple"), mockDependencyChecker);

        // Assert
        result.Count.Should().BeGreaterThanOrEqualTo(2, "should have multiple environment apps");
        result.Should().Contain(m => m.AppName == EnvironmentAppConfiguration.SqlServerEdge,
            "should include SQL Server");
        result.Should().Contain(m => m.AppName == EnvironmentAppConfiguration.Redis,
            "should include Redis");
    }

    [Fact]
    public async Task CheckEnvironmentAppsAsync_WithRabbitMq_ReturnsRabbitMqMapping()
    {
        // Arrange
        var mockDependencyChecker = new MockDependencyChecker();
        mockDependencyChecker.SetDependencyResult("Volo.Abp.EventBus.RabbitMQ", true);

        // Act
        var result = await CheckEnvironmentAppsAsync(CreateTestProject("TestProject.RabbitMq"), mockDependencyChecker);

        // Assert
        result.Should().NotBeEmpty("project has RabbitMQ dependency");
        result.Should().ContainSingle(
            m => m.AppName == EnvironmentAppConfiguration.RabbitMq,
            "should detect RabbitMQ dependency");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Simulates the CheckEnvironmentAppsAsync logic from PrepareCommand.
    /// </summary>
    private async Task<List<AppEnvironmentMapping>> CheckEnvironmentAppsAsync(
        string projectPath,
        MockDependencyChecker dependencyChecker)
    {
        var results = new List<AppEnvironmentMapping>();
        var appEnvironmentMapping = AppEnvironmentMapping.Default;
        var tasks = appEnvironmentMapping.Keys.Select(async package =>
        {
            try
            {
                bool hasDependency = await dependencyChecker.CheckSingleDependencyAsync(
                    projectPath,
                    package,
                    CancellationToken.None);

                if (hasDependency && appEnvironmentMapping.TryGetValue(package, out var mapping))
                {
                    lock (results)
                    {
                        results.Add(mapping);
                    }
                }
            }
            catch
            {
                // Swallow exceptions for testing purposes
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Creates a test project file with the given name.
    /// </summary>
    private string CreateTestProject(string projectName)
    {
        var projectDir = Path.Combine(_testRootPath, projectName);
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, content);

        return projectPath;
    }

    #endregion

    #region Mock Dependency Checker

    /// <summary>
    /// Mock implementation for dependency checking in tests.
    /// </summary>
    private class MockDependencyChecker
    {
        private readonly Dictionary<string, bool> _dependencyResults = new();

        public void SetDependencyResult(string package, bool hasDependency)
        {
            _dependencyResults[package] = hasDependency;
        }

        public void SetAllDependenciesTo(bool value)
        {
            foreach (var key in AppEnvironmentMapping.Default.Keys)
            {
                _dependencyResults[key] = value;
            }
        }

        public Task<bool> CheckSingleDependencyAsync(string projectPath, string assemblyName, CancellationToken cancellationToken)
        {
            return Task.FromResult(_dependencyResults.TryGetValue(assemblyName, out var result) ? result : false);
        }
    }

    #endregion
}
