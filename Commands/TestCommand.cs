using CliFx.Infrastructure;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands;

[Command("test")]
public class TestCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        // Ask for the user's favorite fruits
        var fruits = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("What are your [green]favorite fruits[/]?")
                .NotRequired() // Not required to have a favorite fruit
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more fruits)[/]")
                .InstructionsText(
                    "[grey](Press [blue]<space>[/] to toggle a fruit, " +
                    "[green]<enter>[/] to accept)[/]")
                .AddChoices(new[] {
            "Apple", "Apricot", "Avocado",
            "Banana", "Blackcurrant", "Blueberry",
            "Cherry", "Cloudberry", "Cocunut",
                }));

        // Write the selected fruits to the terminal
        foreach (string fruit in fruits)
        {
            AnsiConsole.WriteLine(fruit);
        }
    }
}
