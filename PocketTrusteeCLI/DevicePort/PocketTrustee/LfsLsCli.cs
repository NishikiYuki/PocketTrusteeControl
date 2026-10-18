using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsLsCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsLsCli.LfsLsCliSettings> {
    public class LfsLsCliSettings : CommandSettings {
        [CommandArgument(0, "[path]")]
        [Description("The directory path.")]
        public string? Path { get; init; }
    }

    protected override int Execute(CommandContext context, LfsLsCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var path = string.IsNullOrWhiteSpace(settings.Path)
            ? sessionControl.CurrentPath
            : LfsPath.Resolve(sessionControl.CurrentPath, settings.Path);

        try {
            var entries = deviceInterface.ListEntries(path).ToList();
            foreach (var entry in entries) {
                var name = Markup.Escape(entry.Name);
                console.MarkupLine(entry.IsDirectory
                    ? $"[blue]d[/] [grey]{entry.Size,10}[/] [blue]{name}[/]"
                    : $"[grey]f[/] [grey]{entry.Size,10}[/] {name}");
            }
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to list '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        return 0;
    }
}
