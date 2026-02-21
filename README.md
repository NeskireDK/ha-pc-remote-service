# ha-windows-remote-service

A lightweight Windows Service (Native AOT) that exposes a REST API for controlling audio, monitors, apps, and system power on a Windows PC. Designed for use with the [ha-windows-remote](https://github.com/YOUR_USERNAME/ha-windows-remote) Home Assistant integration.

## Quick Start

1. Download the latest release
2. Place NirSoft tools in `./tools/` (optional, for audio/monitor control)
3. Run the exe once to generate `appsettings.json` with your API key
4. Install as a Windows Service:
   ```powershell
   sc create HaWindowsRemote binPath="C:\path\to\HaWindowsRemote.Service.exe"
   sc start HaWindowsRemote
   ```

## API

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/health` | Health check |
| `POST` | `/api/system/sleep` | Suspend the PC |

## License

MIT
