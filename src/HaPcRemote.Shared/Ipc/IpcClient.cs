using System.IO.Pipes;

namespace HaPcRemote.Shared.Ipc;

/// <summary>
/// Named pipe client used by the service to delegate commands to the tray app.
/// Creates a new connection per request for simplicity.
/// </summary>
public sealed class IpcClient : IDisposable
{
    private readonly int _connectTimeoutMs;

    public IpcClient(int connectTimeoutMs = 3000)
    {
        _connectTimeoutMs = connectTimeoutMs;
    }

    /// <summary>
    /// Send a request to the tray app and return the response.
    /// Returns null if the tray app is not running.
    /// </summary>
    public async Task<IpcResponse?> SendAsync(IpcRequest request, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(
            ".", IpcProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                await pipe.ConnectAsync(_connectTimeoutMs, ct);
            }
            catch (TimeoutException)
            {
                return null;
            }

            await IpcProtocol.WriteMessageAsync(pipe, request, IpcJsonContext.Default.IpcRequest, ct);
            return await IpcProtocol.ReadMessageAsync(pipe, IpcJsonContext.Default.IpcResponse, ct);
        }
    }

    /// <summary>Check if the tray app is running by sending a ping.</summary>
    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcRequest { Type = "ping" }, ct);
        return response is { Success: true };
    }

    public void Dispose()
    {
        // No persistent resources to dispose with per-request connections
    }
}
