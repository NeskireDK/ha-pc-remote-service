using HaPcRemote.Shared.Ipc;

namespace HaPcRemote.Service.Services;

/// <summary>
/// ICliRunner that delegates execution to the tray app via named pipe IPC.
/// Falls back to direct execution if the tray is not running (dev mode).
/// </summary>
public sealed class TrayCliRunner(ILogger<TrayCliRunner> logger) : ICliRunner
{
    private readonly CliRunner _fallback = new();

    public async Task<string> RunAsync(string exePath, IEnumerable<string> arguments, int timeoutMs = 10000)
    {
        var args = arguments as string[] ?? arguments.ToArray();

        var client = new IpcClient();
        var response = await client.SendAsync(new IpcRequest
        {
            Type = "runCli",
            ExePath = exePath,
            Arguments = args,
            TimeoutMs = timeoutMs
        }, CancellationToken.None);

        if (response is null)
        {
            logger.LogWarning("Tray not connected, falling back to direct execution");
            return await _fallback.RunAsync(exePath, args, timeoutMs);
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(
                response.Error ?? "Tray IPC call failed with no error message");
        }

        return response.Stdout ?? string.Empty;
    }
}
