using System.IO.Ports;
using System.Formats.Cbor;
using Microsoft.Extensions.Logging;

namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

public sealed class DeviceInterface : IDisposable {
    private const CborConformanceMode CborMode = CborConformanceMode.Lax;
    private const int TransferChunkSize = 128;

    private readonly SerialPort _port;
    private readonly ILogger<DeviceInterface> _logger;
    private readonly Queue<TransferResponse> _pendingResponses = new();

    public DeviceInterface(string comPort, ILoggerFactory loggerFactory) {
        _port = new SerialPort(comPort);
        _port.WriteTimeout = 1000;
        _port.ReadTimeout = 1000;
        _port.Open();
        _logger = loggerFactory.CreateLogger<DeviceInterface>();
    }

    public DeviceInfo? GetDeviceBaseInfo() {
        ResetResponseState();
        SendRequest("info");
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

    public IEnumerable<LfsItemEntryInfo> ListEntries(string path) {
        try {
            ResetResponseState();
            SendRequest("ls", path);

            var entries = new List<LfsItemEntryInfo>();
            while (true) {
                var receiveData = _port.ReadToIdle();
                if (receiveData.Count == 0) {
                    throw new IOException($"No response from device while listing '{path}'.");
                }

                var reader = new CborReader(receiveData.ToArray(), CborMode);
                if (reader.PeekState() == CborReaderState.TextString) {
                    var message = reader.ReadTextString();
                    if (message != "done") {
                        throw new IOException($"Unexpected response '{message}' while listing '{path}'.");
                    }

                    EnsureMessageConsumed(reader, $"while listing '{path}'");
                    return entries;
                }

                if (reader.PeekState() != CborReaderState.StartArray) {
                    throw new IOException($"Unexpected CBOR response while listing '{path}'.");
                }

                var count = reader.ReadStartArray();
                var name = reader.ReadTextString();
                if (count is not null && count is not 2 and not 3) {
                    throw new IOException($"Unexpected array length while listing '{path}'.");
                }

                if (name == "error" &&
                    reader.PeekState() is CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger) {
                    var error = reader.ReadInt32();
                    reader.ReadEndArray();
                    EnsureMessageConsumed(reader, $"while listing '{path}'");
                    _logger.LogError("Device failed to list '{Path}' with LFS error {Error}.", path, error);
                    throw new IOException($"Device failed to list '{path}' (LFS error {error}).");
                }

                var type = reader.ReadTextString();
                var size = reader.ReadUInt32();
                reader.ReadEndArray();
                EnsureMessageConsumed(reader, $"while listing '{path}'");

                entries.Add(new LfsItemEntryInfo {
                    Name = name,
                    Type = type,
                    Size = size,
                });
                SendAck();
            }
        }
        catch (IOException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error listing '{Path}' on device", path);
            throw new IOException($"Failed to list '{path}' on device.", ex);
        }
    }

    public void Write(string path, ReadOnlySpan<byte> data, Action<float>? progressCallback = null) {
        const string operation = "write";
        try {
            ResetResponseState();
            SendRequest(operation, path);

            var response = ReadTransferResponse($"write '{path}'");
            ExpectText(response, "ready", $"write '{path}'");
            progressCallback?.Invoke(0f);

            if (data.IsEmpty) {
                SendChunk(false, ReadOnlySpan<byte>.Empty);
                response = ReadTransferResponse($"write '{path}'");
                ExpectText(response, "ok", $"write '{path}'");
            }
            else {
                var offset = 0;
                while (offset < data.Length) {
                    var size = Math.Min(TransferChunkSize, data.Length - offset);
                    var more = offset + size < data.Length;
                    SendChunk(more, data.Slice(offset, size));

                    response = ReadTransferResponse($"write '{path}'");
                    ExpectText(response, "ok", $"write '{path}'");

                    offset += size;
                    progressCallback?.Invoke(Math.Clamp((float)offset / data.Length, 0f, 1f));
                }
            }

            response = ReadTransferResponse($"write '{path}'");
            ExpectText(response, "done", $"write '{path}'");
            progressCallback?.Invoke(1f);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (IOException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error writing '{Path}' to device", path);
            throw new IOException($"Failed to write '{path}' to device.", ex);
        }
    }

    public byte[] Read(string path, Action<float>? progressCallback = null) {
        try {
            progressCallback?.Invoke(0f);
            var fileSize = GetFileSize(path);
            if (fileSize > int.MaxValue) {
                throw new IOException($"File '{path}' is too large to read into memory.");
            }

            ResetResponseState();
            SendRequest("read", path);

            var response = ReadTransferResponse($"read '{path}'");
            ExpectText(response, "ready", $"read '{path}'");
            SendAck();

            var data = new List<byte>((int)fileSize);
            while (true) {
                response = ReadTransferResponse($"read '{path}'");
                if (response is not ChunkResponse chunk) {
                    throw new IOException($"Unexpected CBOR response while trying to read '{path}'.");
                }

                data.AddRange(chunk.Data);
                SendAck();

                if (fileSize != 0) {
                    progressCallback?.Invoke(
                        Math.Clamp((float)data.Count / fileSize, 0f, 1f));
                }

                if (!chunk.More) {
                    break;
                }
            }

            response = ReadTransferResponse($"read '{path}'");
            ExpectText(response, "done", $"read '{path}'");

            if (data.Count != fileSize) {
                throw new IOException(
                    $"Device returned {data.Count} bytes for '{path}', but {fileSize} bytes were expected.");
            }

            progressCallback?.Invoke(1f);
            return data.ToArray();
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (IOException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading '{Path}' from device", path);
            throw new IOException($"Failed to read '{path}' from device.", ex);
        }
    }

    public void CreateDirectory(string path) {
        ExecuteCommand("mkdir", $"create directory '{path}'", path);
    }

    public void Remove(string path) {
        ExecuteCommand("rm", $"remove '{path}'", path);
    }

    public void Move(string origin, string destination) {
        ExecuteCommand("mv", $"move '{origin}' to '{destination}'", origin, destination);
    }

    public void FormatFilesystem() {
        ExecuteCommand("format", "format the device filesystem");
    }

    private void ExecuteCommand(string command, string operation, params string[] arguments) {
        try {
            ResetResponseState();
            SendRequest(command, arguments);
            ReadCommandResult(operation);
        }
        catch (IOException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error trying to {Operation} on device", operation);
            throw new IOException($"Failed to {operation} on device.", ex);
        }
    }

    private void ReadCommandResult(string operation) {
        var response = ReadTransferResponse(operation);
        ExpectText(response, "success", operation);
    }

    private uint GetFileSize(string path) {
        var (parent, name) = LfsPath.GetParentAndName(path);
        var entry = ListEntries(parent)
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        if (entry is null) {
            throw new IOException($"File '{path}' does not exist on device.");
        }

        if (entry.IsDirectory) {
            throw new IOException($"Device entry '{path}' is a directory.");
        }

        return entry.Size;
    }

    private TransferResponse ReadTransferResponse(string operation) {
        while (_pendingResponses.Count == 0) {
            var receiveData = _port.ReadToIdle();
            if (receiveData.Count == 0) {
                throw new IOException($"No response from device while trying to {operation}.");
            }

            ParseTransferResponses(receiveData, operation);
        }

        var response = _pendingResponses.Dequeue();
        if (response is ErrorResponse error) {
            _logger.LogError(
                "Device failed to {Operation} with LFS error {Error}.", operation, error.Error);
            throw new IOException(
                $"Device failed to {operation} (LFS error {error.Error}: {DescribeLfsError(error.Error)}).");
        }

        return response;
    }

    private void ParseTransferResponses(List<byte> receiveData, string operation) {
        var data = receiveData.ToArray();
        var offset = 0;
        while (offset < data.Length) {
            var remaining = data.AsMemory(offset);
            var reader = new CborReader(remaining, CborMode, allowMultipleRootLevelValues: true);

            var state = reader.PeekState();
            if (state == CborReaderState.TextString) {
                _pendingResponses.Enqueue(new TextResponse(reader.ReadTextString()));
            }
            else if (state == CborReaderState.StartArray) {
                var count = reader.ReadStartArray();
                if (count is not null && count != 2) {
                    throw new IOException($"Unexpected array length while trying to {operation}.");
                }

                var elementState = reader.PeekState();
                if (elementState == CborReaderState.TextString) {
                    var name = reader.ReadTextString();
                    if (name != "error" ||
                        reader.PeekState() is not CborReaderState.UnsignedInteger and not
                            CborReaderState.NegativeInteger) {
                        throw new IOException($"Unexpected CBOR response while trying to {operation}.");
                    }

                    var error = reader.ReadInt32();
                    reader.ReadEndArray();
                    _pendingResponses.Enqueue(new ErrorResponse(error));
                }
                else if (elementState == CborReaderState.Boolean) {
                    var more = reader.ReadBoolean();
                    var chunk = reader.ReadByteString();
                    reader.ReadEndArray();
                    _pendingResponses.Enqueue(new ChunkResponse(more, chunk));
                }
                else {
                    throw new IOException($"Unexpected CBOR response while trying to {operation}.");
                }
            }
            else {
                throw new IOException($"Unexpected CBOR response while trying to {operation}.");
            }

            var consumed = remaining.Length - reader.BytesRemaining;
            if (consumed <= 0) {
                throw new IOException($"Invalid CBOR response while trying to {operation}.");
            }

            offset += consumed;
        }
    }

    private static void ExpectText(TransferResponse response, string expected, string operation) {
        if (response is TextResponse text && text.Text == expected) {
            return;
        }

        var actual = response switch {
            TextResponse textResponse => $"'{textResponse.Text}'",
            ChunkResponse => "a data chunk",
            _ => "an unexpected response",
        };
        throw new IOException(
            $"Unexpected response {actual} while trying to {operation}; expected '{expected}'.");
    }

    private void SendRequest(string command, params string[] arguments) {
        var writer = new CborWriter(CborMode);
        writer.WriteStartArray(arguments.Length + 1);
        writer.WriteTextString(command);
        foreach (var argument in arguments) {
            writer.WriteTextString(argument);
        }

        writer.WriteEndArray();
        var request = writer.Encode();
        _port.Write(request, 0, request.Length);
    }

    private void SendChunk(bool more, ReadOnlySpan<byte> data) {
        var writer = new CborWriter(CborMode);
        writer.WriteStartArray(2);
        writer.WriteBoolean(more);
        writer.WriteByteString(data);
        writer.WriteEndArray();
        var chunk = writer.Encode();
        _port.Write(chunk, 0, chunk.Length);
    }

    private void ResetResponseState() {
        _port.DiscardInBuffer();
        _pendingResponses.Clear();
    }

    private static string DescribeLfsError(int error) {
        return error switch {
            -5 => "I/O error",
            -84 => "corrupted filesystem",
            -2 => "entry not found",
            -17 => "entry already exists",
            -20 => "not a directory",
            -21 => "entry is a directory",
            -39 => "directory not empty",
            -9 => "bad file number",
            -27 => "file too large",
            -22 => "invalid parameter",
            -28 => "no space left on device",
            -12 => "out of memory",
            -61 => "attribute not found",
            -36 => "name too long",
            _ => "unknown error",
        };
    }

    private void SendAck() {
        var writer = new CborWriter(CborMode);
        writer.WriteTextString("ok");
        var ack = writer.Encode();
        _port.Write(ack, 0, ack.Length);
    }

    private static void EnsureMessageConsumed(CborReader reader, string description) {
        if (reader.BytesRemaining != 0) {
            throw new IOException($"Unexpected trailing CBOR data {description}.");
        }
    }

    private abstract record TransferResponse;

    private sealed record TextResponse(string Text) : TransferResponse;

    private sealed record ErrorResponse(int Error) : TransferResponse;

    private sealed record ChunkResponse(bool More, byte[] Data) : TransferResponse;

    public void Dispose() {
        _port.Close();
        _port.Dispose();
    }
}

public record DeviceInfo {
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Version MVersion { get; init; } = new();
}

public record LfsItemEntryInfo {
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public uint Size { get; init; }
    public bool IsDirectory => string.Equals(Type, "dir", StringComparison.Ordinal);
}
