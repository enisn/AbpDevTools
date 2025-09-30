using System;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands.Migrations;

[Command("migrations", Description = "Manages EntityFrameworkCore migrations in multiple projects.")]
public class MigrationsCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Specify a subcommand.Available subcommands:\n\n");

        await console.Output.WriteLineAsync("\tabpdev add");
        await console.Output.WriteLineAsync("\tabpdev clear");
        await console.Output.WriteLineAsync("\tabpdev recreate");

        await console.Output.WriteLineAsync("\n\nExample:");
        await console.Output.WriteLineAsync("\tabpdev migrations add --name Initial");
        await console.Output.WriteLineAsync("\tabpdev migrations clear");
        await console.Output.WriteLineAsync("\tabpdev migrations recreate --drop-database");

        await console.Output.WriteLineAsync("\n\nGet Help:");
        await console.Output.WriteLineAsync("\tabpdev migrations --help");
        await console.Output.WriteLineAsync("\tabpdev migrations add --help");
        await console.Output.WriteLineAsync("\tabpdev migrations recreate --help");

        return;
        
    }
}
