using System.Diagnostics;
using Spectre.Console;

namespace AbpDevTools.Services;

[RegisterTransient]
public class GitService
{
    public async Task<bool> CloneRepositoryAsync(string remoteUrl, string localPath, string? branch = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure the parent directory exists
            var parentDir = Directory.GetParent(localPath)?.FullName;
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // If the directory exists and is not empty, remove it first
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }

            // Try to determine the best branch to use
            var branchToUse = await DetermineBestBranchAsync(remoteUrl, branch);

            var gitArgs = $"clone {remoteUrl} \"{localPath}\"";
            if (!string.IsNullOrEmpty(branchToUse))
            {
                gitArgs += $" --branch {branchToUse}";
            }
            
            AnsiConsole.MarkupLine($"[dim]Executing: git {gitArgs}[/]");

            return await AnsiConsole.Status()
                .StartAsync($"[green]Cloning repository[/] {(!string.IsNullOrEmpty(branchToUse) ? $"(branch: {branchToUse})" : "")}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = gitArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    
                    // Register cancellation to kill the process
                    using var cancellationRegistration = cancellationToken.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Cancellation requested. Terminating git process...[/]");
                                process.Kill(true); // Kill entire process tree
                            }
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Error killing git process: {ex.Message}[/]");
                        }
                    });

                    process.Start();

                    // Start reading output and error streams
                    var outputTask = Task.Run(async () =>
                    {
                        try
                        {
                            return await process.StandardOutput.ReadToEndAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            return string.Empty; // Process was killed
                        }
                    }, cancellationToken);

                    var errorTask = Task.Run(async () =>
                    {
                        try
                        {
                            return await process.StandardError.ReadToEndAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            return string.Empty; // Process was killed
                        }
                    }, cancellationToken);

                    // Wait for the process to complete or cancellation
                    try
                    {
                        await process.WaitForExitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Git clone operation was cancelled.[/]");
                        return false;
                    }
                    
                    // Wait for all tasks to complete (with a short timeout in case of cancellation)
                    try
                    {
                        await Task.WhenAll(outputTask, errorTask).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                    catch (TimeoutException)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Timeout waiting for git output streams to complete.[/]");
                    }

                    var output = string.Empty;
                    var error = string.Empty;
                    
                    try
                    {
                        output = await outputTask;
                        error = await errorTask;
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        AnsiConsole.MarkupLine($"[red]Git clone failed (exit code: {process.ExitCode}):[/]");
                        if (!string.IsNullOrEmpty(error))
                        {
                            AnsiConsole.MarkupLine($"[red]Error output:[/] {error}");
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            AnsiConsole.MarkupLine($"[red]Standard output:[/] {output}");
                        }
                        return false;
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        AnsiConsole.MarkupLine($"[dim]Git output: {output}[/]");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        AnsiConsole.MarkupLine($"[dim]Git stderr: {error}[/]");
                    }

                    // Verify that files were actually cloned (not just .git directory)
                    if (Directory.Exists(localPath))
                    {
                        var files = Directory.GetFileSystemEntries(localPath, "*", SearchOption.TopDirectoryOnly)
                            .Where(entry => !Path.GetFileName(entry).Equals(".git", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        
                        if (files.Length == 0)
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning: Clone completed but no files found in working directory. Attempting to checkout files...[/]");
                            
                            // Try to manually checkout the HEAD to ensure files are present
                            var checkoutSuccess = await CheckoutFilesAsync(localPath, branchToUse, cancellationToken);
                            if (!checkoutSuccess)
                            {
                                AnsiConsole.MarkupLine($"[red]Failed to checkout files. This might be an empty repository or the branch might not contain any files.[/]");
                            }
                        }
                    }

                    return true;
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during git clone:[/] {ex.Message}");
            return false;
        }
    }

    private async Task<string?> DetermineBestBranchAsync(string remoteUrl, string? preferredBranch)
    {
        // If a specific branch is requested, use it
        if (!string.IsNullOrEmpty(preferredBranch))
        {
            return preferredBranch;
        }

        // Try to get remote branches and find main/master
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"ls-remote --heads {remoteUrl}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var branches = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('\t').LastOrDefault()?.Replace("refs/heads/", ""))
                    .Where(branch => !string.IsNullOrEmpty(branch))
                    .ToList();

                // Prefer main over master (more common nowadays)
                if (branches.Contains("main"))
                    return "main";
                if (branches.Contains("master"))
                    return "master";
            }
        }
        catch
        {
            // If remote branch detection fails, just proceed without specifying branch
        }

        return null;
    }

    public bool IsGitInstalled()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool IsDirectoryEmpty(string path)
    {
        if (!Directory.Exists(path))
            return true;

        return !Directory.EnumerateFileSystemEntries(path).Any();
    }

    private async Task<bool> CheckoutFilesAsync(string localPath, string? branch, CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "checkout .",
                WorkingDirectory = localPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            // Register cancellation to kill the process
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Kill entire process tree
                    }
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            });
            
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[dim]Checkout failed: {error}[/]");
                return false;
            }

            AnsiConsole.MarkupLine($"[green]Successfully checked out files.[/]");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[dim]Checkout error: {ex.Message}[/]");
            return false;
        }
    }
} 