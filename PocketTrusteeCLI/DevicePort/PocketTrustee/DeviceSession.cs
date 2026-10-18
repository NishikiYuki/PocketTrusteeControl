using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public sealed class SessionFactory(string comPort, ILoggerFactory loggerFactory, IAnsiConsole console)
    : IDeviceSessionFactory {
    public IDeviceSession Build() {
        return new DeviceSession(comPort, loggerFactory, console);
    }
}

public sealed class SessionControl {
    public bool ExitRequested { get; private set; }
    public string CurrentPath { get; set; } = "/";

    public void RequestExit() {
        ExitRequested = true;
    }
}

public sealed class DisconnectCli(IAnsiConsole console, SessionControl sessionControl) : Command {
    protected override int Execute(CommandContext context, CancellationToken cancellationToken) {
        sessionControl.RequestExit();
        console.MarkupLine("[grey]Disconnected.[/]");
        return 0;
    }
}

public sealed class DeviceSession(string comPort, ILoggerFactory loggerFactory, IAnsiConsole console)
    : IDeviceSession {
    private readonly ILogger<DeviceSession> _logger = loggerFactory.CreateLogger<DeviceSession>();
    private readonly DeviceInterface _deviceInterface = new(comPort, loggerFactory);

    private readonly CancellationTokenSource _cts = new();
    private readonly SessionControl _sessionControl = new();

    public int Run() => RunAsync().GetAwaiter().GetResult();

    private async Task<int> RunAsync() {
        ConsoleCancelEventHandler cancelHandler = (_, e) => {
            e.Cancel = true;
            _cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try {
            return await RunCommandsAsync(_cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) {
            return 0;
        }
        finally {
            Console.CancelKeyPress -= cancelHandler;
            _deviceInterface.Dispose();
            _cts.Dispose();
        }
    }

    private async Task<int> RunCommandsAsync(CancellationToken cancellationToken) {
        var info = _deviceInterface.GetDeviceBaseInfo();
        if (info is null) {
            console.WriteLine("Lose connection with device");
            return -1;
        }

        console.MarkupLine($"[green]Successfully connected to {Markup.Escape(info.Name)}[/]");

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton(console);
        services.AddSingleton(_deviceInterface);
        services.AddSingleton(_sessionControl);
        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config => {
            config.SetApplicationName("PocketTrustee");
            config.AddCommand<BaseInfoCli>("info")
                .WithDescription("Shows info about the device.");
            config.AddCommand<LfsLsCli>("ls")
                .WithDescription("Lists entries of a device directory.");
            config.AddCommand<LfsCdCli>("cd")
                .WithDescription("Changes the current device directory.");
            config.AddCommand<LfsMkdirCli>("mkdir")
                .WithDescription("Creates a directory on the device.");
            config.AddCommand<LfsRmCli>("rm")
                .WithDescription("Removes a file or empty directory from the device.");
            config.AddCommand<LfsMvCli>("mv")
                .WithDescription("Moves or renames a file or directory on the device.");
            config.AddCommand<LfsWriteCli>("write")
                .WithDescription("Uploads a local file to the device.");
            config.AddCommand<LfsReadCli>("read")
                .WithDescription("Downloads a device file to the local filesystem.");
            config.AddCommand<LfsFormatCli>("format")
                .WithDescription("Formats the device filesystem.");
            config.AddCommand<DisconnectCli>("disconnect")
                .WithAlias("exit")
                .WithDescription("Disconnects from the device and exits.");
        });

        while (!cancellationToken.IsCancellationRequested) {
            var input = await console.AskAsync<string>($"({_sessionControl.CurrentPath})>", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string[] args;
            try {
                args = CommandLineParser.Split(input);
            }
            catch (FormatException ex) {
                console.MarkupLine($"[red]Invalid command: {Markup.Escape(ex.Message)}[/]");
                continue;
            }

            if (args.Length == 0) {
                continue;
            }

            await app.RunAsync(args, cancellationToken);
            if (_sessionControl.ExitRequested) {
                break;
            }
        }
        return 0;
    }
}
