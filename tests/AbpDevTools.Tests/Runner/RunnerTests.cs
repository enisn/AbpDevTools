using System.Diagnostics;
using AbpDevTools.Runner;
using Shouldly;
using Xunit;

namespace AbpDevTools.Tests.Runner;

public sealed class RunnerTests
{
    [Fact]
    public void ContextIdentity_IsStableForEquivalentPaths_AndIncludesConfiguration()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var configurationPath = Path.Combine(root, "abpdev.yml");
            File.WriteAllText(configurationPath, "run:\n  skip-migrate: true\n");

            var first = RunnerContextIdentity.Create(root + Path.DirectorySeparatorChar, configurationPath);
            var equivalent = RunnerContextIdentity.Create(Path.Combine(root, "."), configurationPath);
            var withoutConfiguration = RunnerContextIdentity.Create(root, null);

            equivalent.ContextKey.ShouldBe(first.ContextKey);
            equivalent.ConfigurationHash.ShouldBe(first.ConfigurationHash);
            first.ConfigurationHash.ShouldNotBeNullOrWhiteSpace();
            withoutConfiguration.ContextKey.ShouldNotBe(first.ContextKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplicationIdentity_AndMatcher_IncludeNpmScriptAndStableId()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var packagePath = Path.Combine(root, "package.json");
            var devId = RunnerContextIdentity.CreateApplicationId(packagePath, "dev");
            var testId = RunnerContextIdentity.CreateApplicationId(packagePath, "test");
            var snapshot = new RunnerApplicationSnapshot
            {
                Id = devId,
                Name = "frontend",
                DisplayName = "ui:dev",
                TargetPath = packagePath,
                WorkingDirectory = root,
                Script = "dev"
            };

            devId.ShouldNotBe(testId);
            RunnerProjectMatcher.Matches(snapshot, devId).ShouldBeTrue();
            RunnerProjectMatcher.Matches(snapshot, "ui:dev").ShouldBeTrue();
            RunnerProjectMatcher.Matches(snapshot, "missing").ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplicationSpec_RoundTripsTheCompleteProcessEnvironment()
    {
        var startInfo = new ProcessStartInfo("tool", "--flag")
        {
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false,
            RedirectStandardInput = true
        };
        startInfo.Environment.Clear();
        startInfo.Environment["ABPDEV_RUNNER_TEST"] = "value";

        var spec = RunnerApplicationSpec.FromProcessStartInfo(
            "id",
            "name",
            "display",
            RunnerApplicationType.Other,
            Path.Combine(Path.GetTempPath(), "target"),
            null,
            startInfo,
            retry: true,
            verbose: true);
        var restored = spec.CreateProcessStartInfo();

        restored.FileName.ShouldBe("tool");
        restored.Arguments.ShouldBe("--flag");
        restored.Environment.Count.ShouldBe(1);
        restored.Environment["ABPDEV_RUNNER_TEST"].ShouldBe("value");
        restored.RedirectStandardInput.ShouldBeTrue();
        spec.Retry.ShouldBeTrue();
        spec.Verbose.ShouldBeTrue();

        var changedRetryPolicy = RunnerApplicationSpec.FromProcessStartInfo(
            "id",
            "name",
            "display",
            RunnerApplicationType.Other,
            Path.Combine(Path.GetTempPath(), "target"),
            null,
            startInfo,
            retry: false,
            verbose: true);
        changedRetryPolicy.Fingerprint.ShouldNotBe(spec.Fingerprint);
    }

    [Fact]
    public async Task Registry_ReconcilesDuplicateStarts_CapturesLogs_AndStopsByProjectFilter()
    {
        var root = CreateTemporaryDirectory();
        var logsDirectory = Path.Combine(root, "logs");
        using var registry = new RunnerRegistry(logsDirectory);
        try
        {
            var descriptor = RunnerContextIdentity.Create(root, null);
            var spec = CreateLongRunningSpec(root);
            var request = new RunnerStartContextRequest
            {
                ContextKey = descriptor.ContextKey,
                DisplayName = descriptor.DisplayName,
                WorkingDirectory = descriptor.WorkingDirectory,
                Applications = new[] { spec }
            };

            var firstStart = registry.Start(request);
            var duplicateStart = registry.Start(request);

            firstStart.AffectedCount.ShouldBe(1);
            duplicateStart.AffectedCount.ShouldBe(0);
            duplicateStart.Messages.ShouldContain(x => x.Contains("already running", StringComparison.OrdinalIgnoreCase));
            registry.HasActiveApplications.ShouldBeTrue();

            await WaitUntilAsync(
                () => registry.GetLogs(descriptor.ContextKey, spec.Id, 0, 100)
                    .Logs.Any(x => x.Message.Contains("Now listening on", StringComparison.Ordinal)),
                TimeSpan.FromSeconds(5));

            var logs = registry.GetLogs(descriptor.ContextKey, spec.Id, 0, 100);
            logs.Logs.ShouldContain(x => x.Message.Contains("Now listening on", StringComparison.Ordinal));

            var unmatchedStop = await registry.StopAsync(
                descriptor.ContextKey,
                new[] { "another-project" },
                CancellationToken.None);
            unmatchedStop.AffectedCount.ShouldBe(0);

            var stop = await registry.StopAsync(
                descriptor.ContextKey,
                new[] { "Sample.Web" },
                CancellationToken.None);
            stop.AffectedCount.ShouldBe(1);
            registry.HasActiveApplications.ShouldBeFalse();
        }
        finally
        {
            await registry.StopAllAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ServerAndClient_ExchangeAuthenticatedRequestsOverTheNamedPipe()
    {
        var root = CreateTemporaryDirectory();
        var pipeName = "abpdev-runner-tests-" + Guid.NewGuid().ToString("N");
        var token = Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        var server = new RunnerServer(
            pipeName,
            token,
            idleTimeout: TimeSpan.FromSeconds(30),
            instanceLockPath: Path.Combine(root, "runner.lock"),
            logsDirectory: Path.Combine(root, "logs"));
        var serverTask = server.RunAsync(cancellation.Token);
        var client = new RunnerClient(pipeName, token);

        try
        {
            await WaitUntilAsync(
                () => client.IsAvailableAsync().GetAwaiter().GetResult(),
                TimeSpan.FromSeconds(5));

            var response = await client.ListAsync(includeInactive: true);

            response.ShouldNotBeNull();
            response.Success.ShouldBeTrue();
            response.Contexts.ShouldBeEmpty();
        }
        finally
        {
            cancellation.Cancel();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            Directory.Delete(root, recursive: true);
        }
    }

    private static RunnerApplicationSpec CreateLongRunningSpec(string workingDirectory)
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(
                Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                "/d /s /c \"echo Now listening on: http://127.0.0.1:43210 & ping 127.0.0.1 -n 30 > nul\"");
        }
        else
        {
            startInfo = new ProcessStartInfo(
                "/bin/sh",
                "-c \"echo 'Now listening on: http://127.0.0.1:43210'; sleep 30\"");
        }

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        var targetPath = Path.Combine(workingDirectory, "Sample.Web.csproj");
        return RunnerApplicationSpec.FromProcessStartInfo(
            RunnerContextIdentity.CreateApplicationId(targetPath),
            "Sample.Web.csproj",
            "Sample.Web.csproj",
            RunnerApplicationType.DotNet,
            targetPath,
            null,
            startInfo,
            retry: false,
            verbose: false);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var startedAt = Stopwatch.StartNew();
        while (!condition())
        {
            if (startedAt.Elapsed >= timeout)
            {
                throw new TimeoutException("The expected runner state was not reached before the timeout.");
            }

            await Task.Delay(25);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AbpDevTools_Runner_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
