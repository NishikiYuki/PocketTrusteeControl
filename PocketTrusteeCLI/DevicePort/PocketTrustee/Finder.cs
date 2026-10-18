using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public sealed class DeviceFinder(ILoggerFactory loggerFactory, IAnsiConsole console) : IDeviceFinder {
    private readonly ILogger<DeviceFinder> _logger = loggerFactory.CreateLogger<DeviceFinder>();
    
    public IEnumerable<DeviceSearchResult> FindAll() {
        var devices = new List<DeviceSearchResult>();
        foreach (var port in SerialPort.GetPortNames()) {
            try {
                using var device = new DeviceInterface(port, loggerFactory);
                var info = device.GetDeviceBaseInfo();
                if (info is null) continue;
                var searchInfo = new DeviceSearchResult() {
                    Name = info.Name,
                    Mversion = info.MVersion,
                    Port = port,
                    SessionFactory = new SessionFactory(port, loggerFactory, console)
                };
                devices.Add(searchInfo);
            }
            catch (Exception e) {
                _logger.LogError(e, "Error occured while trying to find device");
            }
        }

        return devices;
    }
}