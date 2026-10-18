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

public sealed class DeviceSession(string comPort, ILoggerFactory loggerFactory, IAnsiConsole console)
    : IDeviceSession {
    private readonly ILogger<DeviceSession> _logger = loggerFactory.CreateLogger<DeviceSession>();
    private readonly DeviceInterface _deviceInterface = new(comPort, loggerFactory);

    public int Run() {
        var info = _deviceInterface.GetDeviceBaseInfo();
        if (info is null) {
            console.WriteLine("Lose connection with device");
            return -1;
        }

        console.MarkupLine($"[green]Successfully connected to {Markup.Escape(info.Name)}[/]");
        return 0;
    }
}