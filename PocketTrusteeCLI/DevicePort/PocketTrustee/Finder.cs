using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public sealed class DeviceFinder(ILoggerFactory loggerFactory) : IDeviceFinder {
    private readonly ILogger<DeviceFinder> _logger = loggerFactory.CreateLogger<DeviceFinder>();
    
    public IEnumerable<DeviceSearchResult> FindAll() {
        var devices = new List<DeviceSearchResult>();
        foreach (var port in SerialPort.GetPortNames()) {
            try {
                var device = new DeviceInterface(port, loggerFactory);
                var info = device.GetDeviceBaseInfo();
                if (info is null) continue;
                var searchInfo = new DeviceSearchResult() {
                    Name = info.Name,
                    Mversion = info.MVersion,
                    Port = port,
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