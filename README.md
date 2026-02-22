# ha-pc-remote-service

A lightweight Windows Service (Native AOT) that exposes a REST API for controlling a Windows PC. Designed for use with the [ha-pc-remote](https://github.com/NeskireDK/ha-pc-remote) Home Assistant integration.

The service advertises itself via mDNS (`_pc-remote._tcp`) for auto-discovery by the HA integration.

## Installation

### Option A: Installer (Recommended)

1. Download `HaPcRemoteService-Setup-x.x.x.exe` from [Releases](https://github.com/NeskireDK/ha-pc-remote-service/releases)
2. Run the installer

The installer handles everything:
- Installs to `C:\Program Files\HA PC Remote Service\`
- Downloads [NirSoft tools](#nirsoft-tools) automatically
- Registers and starts the Windows Service (auto-start on boot)
- Adds a firewall rule for port 5000
- Adds the system tray app to startup (all users)
- Upgrades in-place — stops the existing service, updates files, restarts

To uninstall, use **Add or Remove Programs**. The uninstaller removes the service, firewall rule, and tray startup entry.

### Option B: Portable

1. Download `HaPcRemote-win-x64.zip` (or `win-arm64`) from [Releases](https://github.com/NeskireDK/ha-pc-remote-service/releases)
2. Extract to a directory of your choice (e.g. `C:\HaPcRemote\`)
3. Download [NirSoft tools](#nirsoft-tools) and place them in a `tools/` folder next to the exe
4. Run `HaPcRemote.Service.exe` once — it auto-generates an API key in `%ProgramData%\HaPcRemote\appsettings.json`
5. Register as a Windows Service and open the firewall:
   ```powershell
   sc create HaPcRemoteService binPath="C:\HaPcRemote\HaPcRemote.Service.exe" start=auto
   sc start HaPcRemoteService
   netsh advfirewall firewall add rule name="HA PC Remote Service" dir=in action=allow protocol=TCP localport=5000
   ```
6. Start the tray app (`HaPcRemote.Tray.exe`) and add it to shell startup — required for audio, monitor, and Steam features

To uninstall:
```powershell
sc stop HaPcRemoteService
sc delete HaPcRemoteService
netsh advfirewall firewall delete rule name="HA PC Remote Service"
```

### System Tray App

The tray app runs in the user session and provides:
- **Log viewer** — shows live service logs (right-click → Show Log)
- **API key display** — shows the auto-generated API key (right-click → Show API Key)
- **Service restart** — restarts the Windows Service (requires UAC)
- **Update notifications** — checks GitHub for new releases and can update in-place
- **IPC bridge** — the service delegates CLI tool execution and Steam operations to the tray app so tools run in the user session (required for monitor, audio, and Steam features)

The tray app is required for monitor, audio, and Steam features to work correctly. Portable installs must add `HaPcRemote.Tray.exe` to shell startup (e.g. the Windows Startup folder).

## API Endpoints

All endpoints except `/api/health` require the `X-Api-Key` header.

### Health

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/health` | Health check (no auth) |

### System

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/system/sleep` | Suspend the PC |

### Audio

Requires `SoundVolumeView.exe` in `ToolsPath`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/audio/devices` | List audio output devices |
| `GET` | `/api/audio/current` | Current default output device |
| `POST` | `/api/audio/set/{deviceName}` | Switch default output device |
| `POST` | `/api/audio/volume/{level}` | Set master volume (0-100) |

> **TODO:** Filter `GET /api/audio/devices` to return only hardware sound card output devices. Currently, virtual audio devices created by applications (e.g. communication apps) appear in the list alongside real output devices.

### Monitors — Direct Control

Requires `MultiMonitorTool.exe` in `ToolsPath`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/monitor/list` | List connected monitors |
| `POST` | `/api/monitor/solo/{id}` | Enable only this monitor, disable all others |
| `POST` | `/api/monitor/enable/{id}` | Enable a monitor |
| `POST` | `/api/monitor/disable/{id}` | Disable a monitor |
| `POST` | `/api/monitor/primary/{id}` | Set a monitor as primary |

> **TODO:** Monitor switching is unreliable across repeated toggling. Known symptoms: (1) first switch — both monitors stay on; (2) switching back — correctly disables monitor 2; (3) re-enabling monitor 2 as primary — does nothing. Root cause likely a race or state issue in MultiMonitorTool when called in rapid succession, or incorrect sequencing of enable/primary/disable operations. Also, `solo/{id}` should set the selected monitor as primary; selecting a monitor should mean: enable + set as primary + disable all others.

### Monitors — Profiles

Profile `.cfg` files are saved in `ProfilesPath` (created with MultiMonitorTool).

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/monitor/profiles` | List saved monitor profiles |
| `POST` | `/api/monitor/set/{profile}` | Apply a monitor profile |

### Apps

Apps are defined in `appsettings.json` under `PcRemote.Apps`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/app/status` | Status of all configured apps |
| `GET` | `/api/app/status/{appKey}` | Status of a specific app |
| `POST` | `/api/app/launch/{appKey}` | Launch a configured app |
| `POST` | `/api/app/kill/{appKey}` | Kill a configured app |

### Steam

Requires Steam to be installed. Returns the top 20 most recently played games.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/steam/games` | List installed games (sorted by last played, top 20) |
| `GET` | `/api/steam/running` | Currently running game, or `null` if none |
| `POST` | `/api/steam/run/{appId}` | Launch a game by Steam app ID |
| `POST` | `/api/steam/stop` | Stop the currently running game |

## Configuration

Runtime-generated settings (API key) are stored in `%ProgramData%\HaPcRemote\appsettings.json` so the service works correctly when installed in read-only locations like `C:\Program Files`. Static configuration is read from `appsettings.json` next to the executable; the ProgramData file overrides matching values.

Full example:

```json
{
  "PcRemote": {
    "Port": 5000,
    "Auth": {
      "Enabled": true,
      "ApiKey": "<auto-generated>"
    },
    "ToolsPath": "./tools",
    "ProfilesPath": "./monitor-profiles",
    "Apps": {
      "steam-bigpicture": {
        "DisplayName": "Steam Big Picture",
        "ExePath": "C:\\Program Files (x86)\\Steam\\steam.exe",
        "Arguments": "steam://open/bigpicture",
        "ProcessName": "steam"
      }
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Port` | `5000` | HTTP listen port |
| `Auth.Enabled` | `true` | Require `X-Api-Key` header |
| `Auth.ApiKey` | (generated) | API key, auto-generated on first run |
| `ToolsPath` | `./tools` | Directory containing NirSoft executables |
| `ProfilesPath` | `./monitor-profiles` | Directory containing monitor profile `.cfg` files |
| `Apps` | `{}` | Map of app key to app definition |

### App Definition

| Key | Required | Description |
|-----|----------|-------------|
| `DisplayName` | Yes | Friendly name shown in HA |
| `ExePath` | Yes | Full path to the executable |
| `Arguments` | No | Command-line arguments |
| `ProcessName` | Yes | Process name used to detect if the app is running |

## NirSoft Tools

Two free NirSoft tools are required for audio and monitor features. The installer downloads them automatically. For portable installs, place them in the `ToolsPath` directory (default: `./tools`).

- **[SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html)** — audio device listing, switching, and volume control
- **[MultiMonitorTool](https://www.nirsoft.net/utils/multi_monitor_tool.html)** — monitor listing, enable/disable, and profile management

## Building

```bash
dotnet build HaPcRemote.sln
dotnet test HaPcRemote.sln
dotnet publish src/HaPcRemote.Service -c Release -r win-x64 /p:PublishAot=true
```

## License

MIT
