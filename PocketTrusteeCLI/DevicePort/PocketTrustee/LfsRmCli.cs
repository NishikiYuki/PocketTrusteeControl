using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsRmCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsRmCli.LfsRmCliSettings> {
    public class LfsRmCliSettings : CommandSettings {
        [CommandArgument(0, "<path>")]
        [Description("The file or empty directory to remove.")]
        public string Path { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, LfsRmCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var path = LfsPath.Resolve(sessionControl.CurrentPath, settings.Path);
        try {
            deviceInterface.Remove(path);
            if (LfsPath.IsSelfOrDescendant(sessionControl.CurrentPath, path)) {
                sessionControl.CurrentPath = "/";
            }

            console.MarkupLine($"[green]Removed '{Markup.Escape(path)}'.[/]");
        }
        catch (IOException ex) {
            console.MarkupLine($"[red]Failed to remove '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        return 0;
    }
}
