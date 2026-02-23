using HaPcRemote.Tray;
using HaPcRemote.Tray.Logging;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var mutex = new Mutex(true, @"Local\HaPcRemoteTray", out var isNew);
if (!isNew) return;

var logProvider = new InMemoryLogProvider();
var webCts = new CancellationTokenSource();

var webApp = TrayWebHost.Build(logProvider);
_ = webApp.RunAsync(webCts.Token);

Application.Run(new TrayApplicationContext(webApp.Services, webCts, logProvider));

webCts.Cancel();
