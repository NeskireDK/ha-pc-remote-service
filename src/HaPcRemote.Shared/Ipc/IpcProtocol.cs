using System.Buffers;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace HaPcRemote.Shared.Ipc;

public static class IpcProtocol
{
    public const string PipeName = "HaPcRemote_Ipc";

    /// <summary>
    /// Write a length-prefixed JSON message to a pipe stream.
    /// Format: [4 bytes little-endian length][UTF-8 JSON payload]
    /// </summary>
    public static async Task WriteMessageAsync<T>(
        PipeStream pipe, T message, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, typeInfo);
        var lengthBytes = BitConverter.GetBytes(json.Length);

        await pipe.WriteAsync(lengthBytes, ct);
        await pipe.WriteAsync(json, ct);
        await pipe.FlushAsync(ct);
    }

    /// <summary>
    /// Read a length-prefixed JSON message from a pipe stream.
    /// </summary>
    public static async Task<T?> ReadMessageAsync<T>(
        PipeStream pipe, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        var lengthBytes = new byte[4];
        var bytesRead = await ReadExactAsync(pipe, lengthBytes, ct);
        if (bytesRead < 4)
            return default;

        var length = BitConverter.ToInt32(lengthBytes);
        if (length <= 0 || length > 1024 * 1024) // 1 MB max
            throw new InvalidOperationException($"Invalid IPC message length: {length}");

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            bytesRead = await ReadExactAsync(pipe, buffer.AsMemory(0, length), ct);
            if (bytesRead < length)
                return default;

            return JsonSerializer.Deserialize(buffer.AsSpan(0, length), typeInfo);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<int> ReadExactAsync(
        PipeStream pipe, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await pipe.ReadAsync(buffer[totalRead..], ct);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }
}
