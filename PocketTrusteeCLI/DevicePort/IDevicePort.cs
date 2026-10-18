using System.IO.Ports;

namespace PocketTrusteeCLI.DevicePort;

public static class DeviceSerialPortExtension {
    public static List<byte> ReadToIdle(this SerialPort port) {
        const int idleTimeout = 20;
        const uint cacheSize = 256;
        var oldTimeout = port.ReadTimeout;
        var received = new List<byte>();
        var buffer = new byte[cacheSize];
        try {
            while (true) {
                try {
                    var r = port.Read(buffer, 0, buffer.Length);
                    received.AddRange(buffer.AsSpan(0, r));
                    port.ReadTimeout = idleTimeout;
                }
                catch (TimeoutException) {
                    break;
                }
            }
        }
        finally {
            port.ReadTimeout = oldTimeout;
        }

        return received;
    }
}

public interface IDeviceFinder {
    IEnumerable<DeviceSearchResult> FindAll();
}

public interface IDeviceSessionFactory {
    IDeviceSession Build();
}

public interface IDeviceSession {
    int Run();
}

public record DeviceSearchResult {
    public string Name { get; init; } = string.Empty;
    public Version Mversion { get; init; } = new();
    public string Port { get; init; } = string.Empty;
    public IDeviceSessionFactory? SessionFactory { get; init; }
}
