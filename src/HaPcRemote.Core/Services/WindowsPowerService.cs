using System.Runtime.InteropServices;

namespace HaPcRemote.Service.Services;

public sealed partial class WindowsPowerService : IPowerService
{
    /// <summary>
    /// Suspends the system (sleep).
    /// hibernate: false = sleep, true = hibernate
    /// forceCritical: false = allow apps to cancel
    /// disableWakeEvent: false = allow wake events
    /// </summary>
    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool forceCritical,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

    public Task SleepAsync()
    {
        if (!SetSuspendState(false, false, false))
        {
            var error = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"SetSuspendState failed with error code {error}");
        }

        return Task.CompletedTask;
    }
}
