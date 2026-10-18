using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsFormatCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command {
    protected override int Execute(CommandContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var confirmed = console.Confirm(
            "Format the device filesystem? All data will be erased.", false);
        if (!confirmed) {
            console.MarkupLine("[grey]Format cancelled.[/]");
            return 0;
        }

        try {
            deviceInterface.FormatFilesystem();
            sessionControl.CurrentPath = "/";
            console.MarkupLine("[green]Formatted the device filesystem.[/]");
        }
        catch (IOException ex) {
            console.MarkupLine($"[red]Failed to format the device filesystem: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        return 0;
    }
}
