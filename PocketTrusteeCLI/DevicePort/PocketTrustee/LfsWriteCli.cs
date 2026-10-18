using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public class LfsWriteCli(DeviceInterface deviceInterface, IAnsiConsole console, SessionControl sessionControl)
    : Command<LfsWriteCli.LfsWriteCliSettings> {
    public class LfsWriteCliSettings : CommandSettings {
        [CommandArgument(0, "<local-path>")]
        [Description("The local file to upload.")]
        public string LocalPath { get; init; } = string.Empty;

        [CommandArgument(1, "<device-path>")]
        [Description("The destination path on the device.")]
        public string DevicePath { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, LfsWriteCliSettings settings,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var devicePath = LfsPath.Resolve(sessionControl.CurrentPath, settings.DevicePath);
        if (Directory.Exists(settings.LocalPath)) {
            console.MarkupLine(
                $"[red]Local path '{Markup.Escape(settings.LocalPath)}' is a directory.[/]");
            return 1;
        }

        byte[] data;
        try {
            data = File.ReadAllBytes(settings.LocalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       NotSupportedException or ArgumentException) {
            console.MarkupLine(
                $"[red]Failed to read local file '{Markup.Escape(settings.LocalPath)}': " +
                $"{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        try {
            console.Progress().Start(progressContext => {
                var task = progressContext.AddTask(
                    $"Uploading {Markup.Escape(devicePath)}", maxValue: 1);
                deviceInterface.Write(devicePath, data, progress => {
                    cancellationToken.ThrowIfCancellationRequested();
                    task.Value = Math.Clamp(progress, 0f, 1f);
                });
                task.Value = 1;
            });
        }
        catch (IOException ex) {
            console.MarkupLine(
                $"[red]Failed to upload '{Markup.Escape(settings.LocalPath)}' to " +
                $"'{Markup.Escape(devicePath)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        console.MarkupLine(
            $"[green]Uploaded '{Markup.Escape(settings.LocalPath)}' to " +
            $"'{Markup.Escape(devicePath)}'.[/]");
        return 0;
    }
}
