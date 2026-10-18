using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsMkdirCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsMkdirCli.LfsMkdirCliSettings> {
    public class LfsMkdirCliSettings : CommandSettings {
        [CommandArgument(0, "<path>")]
        [Description("The directory to create.")]
        public string Path { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, LfsMkdirCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var path = LfsPath.Resolve(sessionControl.CurrentPath, settings.Path);
        try {
            deviceInterface.CreateDirectory(path);
            console.MarkupLine($"[green]Created directory '{Markup.Escape(path)}'.[/]");
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to create directory '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        return 0;
    }
}
