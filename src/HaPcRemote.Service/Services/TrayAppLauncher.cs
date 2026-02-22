using System.Diagnostics;
using HaPcRemote.Shared.Ipc;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Delegates process launches to the tray app via IPC.
/// Falls back to direct Process.Start if the tray is not connected.
/// </summary>
public sealed class TrayAppLauncher(ILogger<TrayAppLauncher> logger) : IAppLauncher
{
    public async Task LaunchAsync(string exePath, string? arguments = null)
    {
        var client = new IpcClient();
        var response = await client.SendAsync(new IpcRequest
        {
            Type = "launchProcess",
            ExePath = exePath,
            ProcessArguments = arguments
        }, CancellationToken.None);

        if (response is null)
        {
            logger.LogWarning("Tray not connected, launching process directly");
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(arguments))
                startInfo.Arguments = arguments;

            Process.Start(startInfo);
            return;
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(
                response.Error ?? "Failed to launch process via tray");
        }
    }
}
