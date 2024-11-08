using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands.Migrations;

[Command("migrations clear", Description = "Clears 'Migrations' folders from EntityFrameworkCore projects.")]
public class ClearMigrationsCommand : MigrationsCommandBase, ICommand
{
    public ClearMigrationsCommand(EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider) : base(entityFrameworkCoreProjectsProvider)
    {
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var projectFiles = await ChooseProjectsAsync();

        if (projectFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No EF Core projects found. No migrations to add.");
            return;
        }

        await AnsiConsole.Status().StartAsync("Clearing migrations...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

            foreach (var project in projectFiles)
            {
                var migrationsFolder = Path.Combine(Path.GetDirectoryName(project.FullName)!, "Migrations");
                if (Directory.Exists(migrationsFolder))
                {
                    Directory.Delete(migrationsFolder, true);
                    AnsiConsole.MarkupLine($"[green]Cleared[/] migrations of [bold]{Path.GetFileNameWithoutExtension(project.Name)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"No migrations found in [bold]{Path.GetFileNameWithoutExtension(project.Name)}[/]");
                }
            }
        });

        await console.Output.WriteLineAsync("Migrations cleared.");
    }
}
