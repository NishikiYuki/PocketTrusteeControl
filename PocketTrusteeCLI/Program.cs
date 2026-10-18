using Microsoft.Extensions.DependencyInjection;
using PocketTrusteeCLI.DevicePort;
using PocketTrusteeCLI.DevicePort.PocketTrustee;
using Spectre.Console;

namespace PocketTrusteeCLI;

public static class Program {
    private static readonly Type[] DevicePortFinder = [
        typeof(DeviceFinder)
    ];

    public static int Main(string[] args) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(AnsiConsole.Console);

        var provider = services.BuildServiceProvider();

        AnsiConsole.WriteLine(Logo);

        var devices = AnsiConsole
            .Status()
            .Start("Searching for devices...",
                _ => {
                    var deviceInfos = new List<DeviceSearchResult>();
                    foreach (var type in DevicePortFinder) {
                        if (ActivatorUtilities.CreateInstance(provider, type) is not IDeviceFinder finder) {
                            continue;
                        }

                        deviceInfos.AddRange(finder.FindAll());
                    }

                    return deviceInfos;
                }
            );

        if (devices.Count == 0) {
            AnsiConsole.MarkupLine("[bold red]× No devices found.[/]");
            return 0;
        }

        DeviceSearchResult selectedDevice;
        if (devices.Count > 1) {
            var prompt = new SelectionPrompt<DeviceSearchResult>()
                .Title("[green]Multiple devices were found,[/] you have to pick up one:")
                .UseConverter(device => $"{device.Name} at {device.Port}")
                .AddChoices(devices);
            selectedDevice = AnsiConsole.Prompt(prompt);
        }
        else {
            selectedDevice = devices[0];
            AnsiConsole.MarkupLine($"[green]Found {selectedDevice.Name}[/]");
        }

        if (selectedDevice.SessionFactory is null) {
            AnsiConsole.MarkupLine($"[bold red]No interface for this device is available.[/]");
            return 0;
        }

        return selectedDevice.SessionFactory.Build().Run();
    }

    private const string Logo =
        """
         ____               _          _   _____                   _              
        |  _ \  ___    ___ | | __ ___ | |_|_   _|_ __  _   _  ___ | |_  ___   ___ 
        | |_) |/ _ \  / __|| |/ // _ \| __| | | | '__|| | | |/ __|| __|/ _ \ / _ \
        |  __/| (_) || (__ |   <|  __/| |_  | | | |   | |_| |\__ \| |_|  __/|  __/
        |_|    \___/  \___||_|\_\\___| \__| |_| |_|    \__,_||___/ \__|\___| \___|

                                       C O M M A N D   L I N E   I N T E R F A C E
        """;
}