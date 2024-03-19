using Cocona;
using Spectre.Console;

AnsiConsole.WriteLine("Starting up");

var app = CoconaApp.Create();

app.AddCommand("extractFeatures", async (string inputDir) =>
{
    if (!Directory.Exists(inputDir))
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }

    return 0;
});