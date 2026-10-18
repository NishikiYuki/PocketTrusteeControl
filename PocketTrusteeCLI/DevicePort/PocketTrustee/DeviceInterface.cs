using System.IO.Ports;
using System.Formats.Cbor;
using Microsoft.Extensions.Logging;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public sealed class DeviceInterface {
    private const CborConformanceMode CborMode = CborConformanceMode.Lax;

    private readonly SerialPort _port;
    private readonly ILogger<DeviceInterface> _logger;

    public DeviceInterface(string comPort, ILoggerFactory loggerFactory) {
        _port = new SerialPort(comPort);
        _port.Open();
        _logger = loggerFactory.CreateLogger<DeviceInterface>();
    }

    public DeviceInfo? GetDeviceBaseInfo() {
        _port.WriteTimeout = 1000;
        _port.ReadTimeout = 1000;

        var writer = new CborWriter(CborMode);
        writer.WriteStartArray(1);
        writer.WriteTextString("info");
        writer.WriteEndArray();
        var encodes = writer.Encode();

        _port.DiscardInBuffer();
        _port.Write(encodes, 0, encodes.Length);
        var receiveData = _port.ReadToIdle();

        if (receiveData.Count == 0) {
            return null;
        }

        var reader = new CborReader(receiveData.ToArray());
        try {
            reader.ReadStartArray();
            var info = new DeviceInfo() {
                Name = reader.ReadTextString(),
                Description = reader.ReadTextString(),
                MVersion = new Version(reader.ReadTextString()),
            };
            reader.ReadEndArray();
            return info;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing cbor data");
            return null;
        }
    }
}

public record DeviceInfo {
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Version MVersion { get; init; } = new();
}