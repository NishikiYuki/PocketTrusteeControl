using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsCdCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsCdCli.LfsCdCliSettings> {
    public class LfsCdCliSettings : CommandSettings {
        [CommandArgument(0, "[path]")]
        [Description("The directory to change to.")]
        public string? Path { get; init; }
    }

    protected override int Execute(CommandContext context, LfsCdCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(settings.Path)) {
            return 0;
        }

        var path = LfsPath.Resolve(sessionControl.CurrentPath, settings.Path);
        if (path == "/") {
            sessionControl.CurrentPath = path;
            return 0;
        }

        var (parent, name) = LfsPath.GetParentAndName(path);
        try {
            var entry = deviceInterface.ListEntries(parent)
                .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (entry is null) {
                console.MarkupLine($"[red]Directory '{Markup.Escape(path)}' does not exist.[/]");
                return 1;
            }

            if (!entry.IsDirectory) {
                console.MarkupLine($"[red]'{Markup.Escape(path)}' is not a directory.[/]");
                return 1;
            }
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to change directory to '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        sessionControl.CurrentPath = path;
        return 0;
    }
}
