# ha-pc-remote-service

A Windows system tray application (and Linux headless daemon) that embeds a Kestrel REST API for controlling a PC remotely. Designed for use with the [ha-pc-remote](https://github.com/NeskireDK/ha-pc-remote) Home Assistant integration.

The app advertises itself via mDNS (`_pc-remote._tcp`) for auto-discovery by the HA integration.

## Installation

### Windows — Installer (Recommended)

1. Download `HaPcRemoteService-Setup-x.x.x.exe` from [Releases](https://github.com/NeskireDK/ha-pc-remote-service/releases)
2. Run the installer — choose your install directory (default: `C:\Program Files\HA PC Remote\`)

The installer handles everything:
- Downloads [NirSoft tools](#nirsoft-tools) automatically
- Adds the tray app to startup
- Adds a firewall rule for port 5000
- Installs .NET 10 Desktop Runtime if missing
- Upgrades in-place — stops the running tray, updates files, restarts
- Config and monitor profiles are stored in `%AppData%\HaPcRemote\`

To uninstall, use **Add or Remove Programs**. The uninstaller removes the tray startup entry and firewall rule.

### Windows — Portable

1. Download `HaPcRemote-win-x64.zip` from [Releases](https://github.com/NeskireDK/ha-pc-remote-service/releases)
2. Extract to a directory of your choice (e.g. `C:\HaPcRemote\`)
3. Download [NirSoft tools](#nirsoft-tools) and place them in a `tools/` folder next to the exe
4. Run `HaPcRemote.Tray.exe` — it auto-generates an API key in `%AppData%\HaPcRemote\appsettings.json`
5. Add `HaPcRemote.Tray.exe` to your shell startup (e.g. the Windows Startup folder or Task Scheduler)
6. Open the firewall:
   ```powershell
   netsh advfirewall firewall add rule name="HA PC Remote" dir=in action=allow protocol=TCP localport=5000 program="C:\HaPcRemote\HaPcRemote.Tray.exe" enable=yes
   ```

To uninstall:
```powershell
netsh advfirewall firewall delete rule name="HA PC Remote"
```

### Linux — Headless

1. Download `HaPcRemote-linux-x64.tar.gz` (or `arm64`) from [Releases](https://github.com/NeskireDK/ha-pc-remote-service/releases)
2. Extract and place the binary at `~/.local/bin/ha-pc-remote`
3. Install the systemd user service:
   ```bash
   cp installer/ha-pc-remote.service ~/.config/systemd/user/
   systemctl --user enable --now ha-pc-remote
   ```

Config is stored in `~/.config/HaPcRemote/appsettings.json`.

### System Tray App (Windows)

The tray app is the main process — it hosts the HTTP server (Kestrel) directly. It must be running for any API feature to work.

Features:
- **Log viewer** — shows live service logs (right-click → Show Log)
- **API key display** — shows the auto-generated API key (right-click → Show API Key)
- **Log level** — configurable: Error / Warning / Info / Verbose (right-click → Logging)
- **Auto-update** — checks GitHub for new releases, downloads and runs the installer

## API Endpoints

All endpoints except `/api/health` require the `X-Api-Key` header.

### Health

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/health` | Health check (no auth) |

### System

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/system/state` | All state in one call (audio, monitors, Steam, modes, idle) |
| `GET` | `/api/system/modes` | List available PC mode names |
| `GET` | `/api/system/idle` | Seconds since last keyboard/mouse input |
| `POST` | `/api/system/mode/{name}` | Apply a named PC mode |
| `POST` | `/api/system/sleep` | Suspend the PC |

### Audio

Requires `SoundVolumeView.exe` in `ToolsPath`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/audio/devices` | List audio output devices |
| `GET` | `/api/audio/current` | Current default output device |
| `POST` | `/api/audio/set/{deviceName}` | Switch default output device |
| `POST` | `/api/audio/volume/{level}` | Set master volume (0-100) |

### Monitors — Direct Control

Requires `MultiMonitorTool.exe` in `ToolsPath`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/monitor/list` | List connected monitors |
| `POST` | `/api/monitor/solo/{id}` | Enable this monitor, set as primary, disable all others |
| `POST` | `/api/monitor/enable/{id}` | Enable a monitor |
| `POST` | `/api/monitor/disable/{id}` | Disable a monitor |
| `POST` | `/api/monitor/primary/{id}` | Set a monitor as primary |

### Monitors — Profiles

Profile `.cfg` files are stored in `ProfilesPath` (`%AppData%\HaPcRemote\monitor-profiles` by default), exported from MultiMonitorTool.

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

Settings are stored in `%AppData%\HaPcRemote\appsettings.json`. The file is auto-generated on first run. Static config next to the exe overrides matching values.

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
    "ProfilesPath": "%AppData%\\HaPcRemote\\monitor-profiles",
    "Apps": {
      "steam-bigpicture": {
        "DisplayName": "Steam Big Picture",
        "ExePath": "C:\\Program Files (x86)\\Steam\\steam.exe",
        "Arguments": "steam://open/bigpicture",
        "ProcessName": "steam"
      }
    },
    "Modes": {
      "couch": {
        "AudioDevice": "HDMI Output",
        "MonitorProfile": "tv-only",
        "Volume": 40,
        "LaunchApp": "steam-bigpicture"
      },
      "desktop": {
        "AudioDevice": "Speakers",
        "MonitorProfile": "desk-full",
        "Volume": 25,
        "KillApp": "steam-bigpicture"
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
| `ProfilesPath` | `%AppData%\HaPcRemote\monitor-profiles` | Directory containing monitor profile `.cfg` files |
| `Apps` | `{}` | Map of app key to app definition |
| `Modes` | `{}` | Map of mode name to mode config |

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
# Tests (WSL-safe — skips Windows-only Tray project)
dotnet test tests/HaPcRemote.Service.Tests

# Windows release
dotnet publish src/HaPcRemote.Tray -c Release -r win-x64

# Linux release
dotnet publish src/HaPcRemote.Headless -c Release -r linux-x64
```

## Known Issues
- ~Browsers hitting the service URL directly trigger API key warnings for `/favicon.ico` requests~ — **Resolved** in v1.0.5: auth middleware now skips non-API paths.
- ~Top 20 not working correctly, showing games not played since 2022~ — **Resolved** in v1.0.5: Steam sorting now uses actual `LastPlayed` timestamps instead of `LastUpdated` (install/update time).
- ~Game images varying size/format~ — **Resolved**: switched from `header.jpg` (460x215 landscape) to `library_600x900.jpg` (600x900 portrait poster) matching HA media browser expectations.
- Non-steam games arent compatible and cannot so far be launched - theres also the issue of how to get posters for these games, could they be extracted from steam or another free fallback solution?
- When user manually checks for update and quickly clicks the green update button, the tray will show a console error of the file is already in use (this is due to auto update triggered right after and colliding with user update, a low priority to solve)
- Update button shouldnt be green, just regular colors
- ~Entity turn on and off broken~ — **Resolved**: the error `extra keys not allowed @ data['input']` was from a user-defined script using `!input` syntax (a blueprint-only feature) in a regular script. The entity `async_turn_on`/`async_turn_off` methods work correctly. If you see this error, check your scripts for `!input` usage and replace with literal entity IDs or use `target:` instead of `data:`.
## Roadmap

- [ ] Introduce config panel with multiple tabs *(in progress — `feature/v1.1-settings-panel`)*
- [ ] User configurable for adding and removing PC modes, for each new PC modes have dropdowns for available monitor profiles, monitors, audio devices and an option for dont change.
- [ ] Show log in the new config panel
- [ ] General settings for setting log level, auto update.
- [ ] Settings tab for default power settings, sleep when disconnect etc values. 
- [ ] When PC modes are stable, consider implementing a config panel to bind PC modes to games, so they will trigger the mode followed by launch of game. User configable delay for each game. 
- [ ] Verify Linux headless daemon + systemd user service *(v0.9.5)*
- [x] Brand icons submitted to [home-assistant/brands](https://github.com/home-assistant/brands) *(awaiting approval)*

## License

MIT
