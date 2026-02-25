using HaPcRemote.Tray;
using HaPcRemote.Tray.Logging;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var mutex = new Mutex(false, @"Local\HaPcRemoteTray");
if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
    return; // Another instance still running

var logProvider = new InMemoryLogProvider();
var webCts = new CancellationTokenSource();

var webApp = TrayWebHost.Build(logProvider);

_ = Task.Run(async () =>
{
    try
    {
        await webApp.StartAsync(webCts.Token);
        KestrelStatus.IsRunning = true;
    }
    catch (Exception ex)
    {
        KestrelStatus.IsRunning = false;
        KestrelStatus.Error = ex.InnerException?.Message ?? ex.Message;
    }
});

Application.Run(new TrayApplicationContext(webApp.Services, webCts, logProvider));

webCts.Cancel();
try { await webApp.StopAsync(TimeSpan.FromSeconds(5)); } catch { }
