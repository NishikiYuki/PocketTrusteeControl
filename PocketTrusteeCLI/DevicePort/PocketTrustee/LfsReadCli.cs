using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsReadCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsReadCli.LfsReadCliSettings> {
    public class LfsReadCliSettings : CommandSettings {
        [CommandArgument(0, "<device-path>")]
        [Description("The source file on the device.")]
        public string DevicePath { get; init; } = string.Empty;

        [CommandArgument(1, "<local-path>")]
        [Description("The local destination file.")]
        public string LocalPath { get; init; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Overwrite the local file if it already exists.")]
        public bool Force { get; init; }
    }

    protected override int Execute(CommandContext context, LfsReadCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var devicePath = LfsPath.Resolve(sessionControl.CurrentPath, settings.DevicePath);
        if (Directory.Exists(settings.LocalPath)) {
            console.MarkupLine(
                $"[red]Local path '{Markup.Escape(settings.LocalPath)}' is a directory.[/]");
            return 1;
        }

        if (File.Exists(settings.LocalPath) && !settings.Force) {
            console.MarkupLine(
                $"[red]Local file '{Markup.Escape(settings.LocalPath)}' already exists. " +
                "Use --force to overwrite it.[/]");
            return 1;
        }

        byte[] data;
        try {
            data = console.Progress().Start(progressContext => {
                var task = progressContext.AddTask(
                    $"Downloading {Markup.Escape(devicePath)}", maxValue: 1);
                var result = deviceInterface.Read(devicePath, progress => {
                    cancellationToken.ThrowIfCancellationRequested();
                    task.Value = Math.Clamp(progress, 0f, 1f);
                });
                task.Value = 1;
                return result;
            });
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to download '{Markup.Escape(devicePath)}': " +
                $"{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        try {
            File.WriteAllBytes(settings.LocalPath, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       NotSupportedException or ArgumentException) {
            console.MarkupLine(
                $"[red]Failed to write local file '{Markup.Escape(settings.LocalPath)}': " +
                $"{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        console.MarkupLine(
            $"[green]Downloaded '{Markup.Escape(devicePath)}' to " +
            $"'{Markup.Escape(settings.LocalPath)}'.[/]");
        return 0;
    }
}
