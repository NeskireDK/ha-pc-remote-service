namespace HaPcRemote.Tray;

/// <summary>
/// Singleton registered in the web app's DI container.
/// Program.cs sets <see cref="RestartAsync"/> after the first WebApplication is built.
/// GeneralTab resolves this service and calls RestartAsync to trigger an in-process Kestrel restart.
/// </summary>
internal sealed class KestrelRestartService
{
    /// <summary>
    /// Set by Program.cs after the initial WebApplication is constructed.
    /// Accepts the new port and performs stop + rebuild + start in-process.
    /// </summary>
    public Func<int, Task>? RestartAsync { get; set; }
}
