using HaPcRemote.Tray;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var mutex = new Mutex(true, @"Local\HaPcRemoteTray", out var isNew);
if (!isNew)
{
    // Already running
    return;
}

Application.Run(new TrayApplicationContext());
