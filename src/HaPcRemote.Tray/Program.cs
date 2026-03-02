using HaPcRemote.Tray;
using HaPcRemote.Tray.Logging;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        using var mutex = new Mutex(false, @"Local\HaPcRemoteTray");
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) return;

        var logProvider = new InMemoryLogProvider();
        var webCts = new CancellationTokenSource();

        var webApp = TrayWebHost.Build(logProvider);

        _ = Task.Run(async () =>
        {
            try
            {
                await webApp.StartAsync(webCts.Token);
                KestrelStatus.SetRunning();
            }
            catch (Exception ex)
            {
                KestrelStatus.SetFailed(ex.InnerException?.Message ?? ex.Message);
            }
        });

        Application.Run(new TrayApplicationContext(webApp.Services, webCts, logProvider));

        webCts.Cancel();
        try { webApp.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); } catch { }
    }
}
