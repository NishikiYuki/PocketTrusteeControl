using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class BaseInfoCli(DeviceInterface deviceInterface, IAnsiConsole console) : Command {

    protected override int Execute(CommandContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var info = deviceInterface.GetDeviceBaseInfo();
        if (info is null) {
            console.MarkupLine($"[red]Failed to read info.[/]");
            return 1;
        }

        var table = new Table();
        table.AddColumn("Property");
        table.AddColumn("Value");
        table.AddRow("Name", Markup.Escape(info.Name));
        table.AddRow("Description", Markup.Escape(info.Description));
        table.AddRow("Version", info.MVersion.ToString());
        console.Write(table);
        return 0;
    }
}
