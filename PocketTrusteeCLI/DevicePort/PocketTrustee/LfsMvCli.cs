using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsMvCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsMvCli.LfsMvCliSettings> {
    public class LfsMvCliSettings : CommandSettings {
        [CommandArgument(0, "<origin>")]
        [Description("The file or directory to move.")]
        public string Origin { get; init; } = string.Empty;

        [CommandArgument(1, "<destination>")]
        [Description("The new path.")]
        public string Destination { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, LfsMvCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var origin = LfsPath.Resolve(sessionControl.CurrentPath, settings.Origin);
        var destination = LfsPath.Resolve(sessionControl.CurrentPath, settings.Destination);
        try {
            deviceInterface.Move(origin, destination);
            if (LfsPath.IsSelfOrDescendant(sessionControl.CurrentPath, origin)) {
                sessionControl.CurrentPath = LfsPath.Rebase(sessionControl.CurrentPath, origin, destination);
            }

            console.MarkupLine($"[green]Moved '{Markup.Escape(origin)}' to '{Markup.Escape(destination)}'.[/]");
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to move '{Markup.Escape(origin)}' to '{Markup.Escape(destination)}': " +
                $"{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        return 0;
    }
}
