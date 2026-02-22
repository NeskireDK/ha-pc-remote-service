using HaPcRemote.Tray;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var mutex = new Mutex(true, @"Global\HaPcRemoteTray", out var isNew);
if (!isNew)
{
    // Already running
    return;
}

Application.Run(new TrayApplicationContext());
