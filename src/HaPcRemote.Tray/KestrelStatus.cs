namespace HaPcRemote.Tray;

internal static class KestrelStatus
{
    private static readonly TaskCompletionSource _started = new();

    public static bool IsRunning { get; private set; }
    public static string? Error { get; private set; }
    public static Task Started => _started.Task;

    public static void SetRunning()
    {
        IsRunning = true;
        _started.TrySetResult();
    }

    public static void SetFailed(string error)
    {
        Error = error;
        _started.TrySetResult();
    }
}
