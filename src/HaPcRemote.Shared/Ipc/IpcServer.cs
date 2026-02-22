using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Shared.Ipc;

/// <summary>
/// Named pipe server used by the tray app to accept commands from the service.
/// Runs a loop that accepts one connection at a time and dispatches to a handler.
/// </summary>
public sealed class IpcServer
{
    private readonly Func<IpcRequest, CancellationToken, Task<IpcResponse>> _handler;
    private readonly ILogger _logger;

    public IpcServer(
        Func<IpcRequest, CancellationToken, Task<IpcResponse>> handler,
        ILogger logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("IPC server starting on pipe '{PipeName}'", IpcProtocol.PipeName);

        while (!ct.IsCancellationRequested)
        {
            var pipe = CreatePipe();

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                // Fire-and-forget each connection so we can accept the next one immediately
                _ = HandleConnectionAsync(pipe, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await pipe.DisposeAsync();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting IPC connection");
                await pipe.DisposeAsync();
            }
        }

        _logger.LogInformation("IPC server stopped");
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                var request = await IpcProtocol.ReadMessageAsync(
                    pipe, IpcJsonContext.Default.IpcRequest, ct);

                if (request is null)
                    return;

                var response = await _handler(request, ct);
                await IpcProtocol.WriteMessageAsync(
                    pipe, response, IpcJsonContext.Default.IpcResponse, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling IPC request");
            }
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        if (OperatingSystem.IsWindows())
            return CreatePipeWithAcl();

        return new NamedPipeServerStream(
            IpcProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreatePipeWithAcl()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!,
            PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            IpcProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0, 0,
            security);
    }
}
