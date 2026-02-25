# Planned Features

Goal: seamless couch gaming with a desktop PC that power-saves when not in use.
The PC is a multi-function machine — desktop (keyboard + mouse, multiple monitors) and
couch/TV (controller, living room display). Switching between these modes gracefully is
the core use case.

---

## Blockers (fix before v1.0)

These are documented bugs that undermine trust in the integration.

- [x] **Steam: tray 503** — `POST /api/steam/run/{appId}` returns 200 even when the tray
  is not running and no game launches. Fixed in service v0.9.0: `IpcSteamPlatform` throws
  `TrayUnavailableException` → endpoint returns 503. *(service)*

- [x] **Steam: running game not in source list** — `GetRunningGameAsync` falls back to
  `"Unknown ({appId})"` if the games cache isn't warm. Fixed in service v0.9.0:
  `GetRunningGameAsync` warms cache on first call and falls back to direct manifest
  lookup for games outside the top-20. *(service)*

---

## Architecture Refactor — Collapse Service + Tray into Single Process *(done in v0.9.1)*

### Background

The old architecture had a Windows Service (SYSTEM session) and a WinForms tray app (user session) communicating via named pipe IPC. Every meaningful feature required the user session anyway — audio, monitors, Steam, app launch. Collapsed everything into the tray process. Kestrel runs inside the tray. No IPC, no session boundary, no `TrayUnavailableException`. Linux gets a natural headless binary as well.

### Releases

#### ~~0.9.2~~ — Extract `HaPcRemote.Core` library *(shipped in v0.9.1)*

- [x] Create `HaPcRemote.Core` class library
- [x] Move services, interfaces, implementations, endpoints, models into Core
- [x] `HaPcRemote.Tray` references Core
- [x] Update test project references

#### ~~0.9.3~~ — Embed Kestrel in Tray, replace IPC with direct calls *(shipped in v0.9.1)*

- [x] Add ASP.NET Core / Kestrel hosting to `HaPcRemote.Tray`
- [x] Wire all Core services into Tray's DI container
- [x] Replace IPC wrappers with direct calls (`WindowsSteamPlatform`, `CliRunner`, `Process.Start`)
- [x] Migrate config path to `%AppData%\HaPcRemote\`

#### ~~0.9.4~~ — Delete Service project, IPC layer, update installer *(shipped in v0.9.1)*

- [x] Delete `HaPcRemote.Service` project
- [x] Delete IPC layer and wrappers
- [x] Update Inno Setup installer (no service registration, startup via all-users startup folder, config migration)
- [x] Update README

#### 0.9.5 — Linux foundation *(service repo)* *(done)*

Same binary, headless mode, systemd user service.

- [x] Wrap all WinForms/tray code behind `OperatingSystem.IsWindows()` / `[SupportedOSPlatform]`
- [x] Add Linux `IPowerService`: `systemctl suspend` or `loginctl suspend`
- [x] Add Linux `ISteamPlatform`: filesystem path (`~/.steam/steam/`), running game via VDF, launch via `xdg-open steam://run/<id>`
- [x] Add Linux audio (`pactl`-based `LinuxAudioService`)
- [x] Add headless entry point (Linux): plain Kestrel + mDNS, no tray icon, SIGTERM clean exit
- [x] Add systemd user service unit file to release artifacts
- [x] Add Linux build job to GitHub Actions CI
- [x] Document install steps in README

### Key decisions made

- **Why collapse?** Every feature requires the user session. IPC is complexity with no benefit.
- **Config path**: moves to `%AppData%` (user-owned, no elevation needed for reads/writes)
- **Native AOT**: dropped — framework-dependent is fine, .NET 10 auto-install already ships
- **Linux tray**: no system tray on Linux. API key via config file, logs via `journalctl`, updates via package manager — these are Linux-native equivalents, not a degraded experience.
- **Monitor profiles on Linux**: xrandr/Wayland too fragmented — skip initially, document as known gap

---

## v1.0

### 1. PC Mode — `POST /api/system/mode` + `select` entity *(done in v1.0)*

Single endpoint that atomically sequences audio output, monitor profile, volume, and
app launch/kill from a named config block.

```json
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
```

HA exposes a `select` entity "PC Mode" with options from `GET /api/system/modes`.
Selecting a mode calls the endpoint. The service handles sequencing and waits between
steps — no fragile automation chains.

- [x] Service: add `Modes` config section and `POST /api/system/mode` endpoint *(service)*
- [x] Service: add `GET /api/system/modes` to list available mode names *(service)*
- [x] Integration: `PcRemoteModeSelect` entity in `select.py` *(integration)*
- [x] Integration: `set_mode()` in `api.py` *(integration)*

### 2. Couch Gaming Automation Blueprint *(done in v1.0)*

Blueprint with selector inputs — no hard-coded entity names.

- [x] `blueprints/automation/pc_remote/couch_gaming.yaml` *(integration)*

### 3. Aggregated State Endpoint — `GET /api/system/state` *(done in v1.0)*

Single endpoint replaces the 6+ individual coordinator calls per poll cycle.

- [x] Service: add `GET /api/system/state` endpoint *(service)*
- [x] Integration: refactor `_async_update_data` to use single call *(integration)*

---

## v1.1

### 4. Post-Session Sleep Blueprint *(done in v1.0)*

When the Steam media player transitions `playing → idle`, wait N minutes, confirm
still idle, then sleep the PC. Closes the power-saving loop without manual action.

- [x] `blueprints/automation/pc_remote/post_session_sleep.yaml` *(integration)*

### 5. Media Browser for Steam Games + Apps *(done in v1.0.2)*

`select_source` works via developer tools / service calls but the dropdown only shows
in the entity detail dialog — not on dashboard cards. Add `browse_media` + `play_media`
support so Steam games (and later apps) appear in the HA media browser with thumbnails
and hierarchical navigation.

- [x] Integration: implement `async_browse_media()` returning `BrowseMedia` tree *(integration)*
- [x] Integration: implement `async_play_media()` to launch games/apps *(integration)*
- [x] Integration: add `BROWSE_MEDIA` + `PLAY_MEDIA` feature flags *(integration)*
- [x] Integration: add tests for browse/play media *(integration)*

### 6. User Idle Time Sensor *(done in v1.0.2)*

`GetLastInputInfo` Win32 API → seconds since last keyboard/mouse input.
Guards the sleep blueprint against sleeping a PC that someone is actively using at the desk.

- [x] Service: expose via `GET /api/system/idle` *(service)*
- [x] Integration: `sensor` entity "Idle Time" (device class `duration`) *(integration)*

### Settings Panel *(done in v1.1)*

Tabbed settings UI in the tray app: Modes, General, and log viewer.

- [x] Service: config panel with multiple tabs *(service)*
- [x] Service: PC mode config UI with dropdowns for monitor profiles, audio devices *(service)*
- [x] Service: general settings for log level *(service)*
- [x] Service: log viewer in settings panel *(service)*

---

## v1.2

### Bugs

- [ ] **Kestrel status stuck on "Starting..."** — The label next to the port field in the
  General tab never transitions to green "Online". It stays on "Starting..." indefinitely
  even after the server is listening. Red "Offline" untested. *(service — `KestrelStatus` / `GeneralTab`)*

- [ ] **Update race condition** — When user manually checks for update and quickly clicks the
  green update button, the tray shows a console error that the file is already in use. Caused
  by auto-update triggering right after and colliding with the manual update. Low priority.
  *(service)*

- [ ] **Update button color** — Update button shouldn't be green, should use regular colors.
  *(service)*

### 7. Rename Duration Sensor to Idle Duration

The "Idle Time" sensor entity name doesn't clearly convey what it measures. Rename to
"Idle Duration" so `idle` appears in both the entity name and the underlying identifiers.

- [ ] Service: rename endpoint/field from idle time → idle duration *(service)*
- [ ] Integration: rename sensor entity to "Idle Duration" (`idle_duration` in entity ID) *(integration)*

### 8. Non-Steam Game Discovery

Discover non-Steam game shortcuts added by the user and expose them alongside regular
Steam games. Shortcuts are stored in `userdata/{steamid}/config/shortcuts.vdf` (binary VDF).

The existing ValveKeyValue library already supports binary VDF parsing
(`KVSerializationFormat.KeyValues1Binary`), used by `LinuxSteamPlatform` for `registry.vdf`.

- [ ] Service: parse `shortcuts.vdf` to extract non-Steam shortcut entries *(service)*
- [ ] Service: merge shortcuts into the game list with a distinguishing flag *(service)*
- [ ] Service: launch shortcuts via `steam://rungameid/{(appid << 32) | 0x02000000}` — preserves user args, overlay, and Steam Input *(service)*
- [ ] Integration: display non-Steam games in media browser alongside Steam games *(integration)*

### 9. Steam Artwork / Poster Serving

Serve game artwork (posters, heroes, logos) from Steam's local cache so the HA media
browser can display thumbnails for both Steam and non-Steam games.

Steam stores artwork in two locations (custom grid takes priority):
- `userdata/{steamid}/config/grid/` — user-set custom art (`{appid}p.png` for capsule)
- `appcache/librarycache/` — Steam CDN cache (`{appid}_library_600x900.jpg`)

- [ ] Service: add `GET /api/steam/artwork/{appId}` endpoint that resolves artwork from grid → librarycache fallback *(service)*
- [ ] Integration: use artwork endpoint as thumbnail URL in media browser `BrowseMedia` entries *(integration)*

### 10. API Debug Page in Tray App

Tray context menu item "API Explorer" opens the default browser to a self-hosted debug page
served by the service. Lists all endpoints with descriptions, methods, and example parameters.
Each endpoint has a "Try it" button that fires the request and displays the result inline.

**Auth solution:** The HTML page is served by the service itself at `GET /debug` (excluded
from API key middleware). The page contains a server-rendered `<meta>` tag or JS variable
with the API key, and all test requests use `fetch()` with the `X-Api-Key` header — so
authentication stays intact for the real API, but the debug page handles it transparently.
The `/debug` endpoint only responds to `127.0.0.1`/`::1` requests for safety.

- [ ] Service: add `/debug` endpoint serving a static HTML page, localhost-only, excluded from auth middleware *(service)*
- [ ] Service: inject API key into the page so JS `fetch()` calls include `X-Api-Key` header automatically *(service)*
- [ ] Service: auto-discover or manually list all API endpoints with method, path, and example params *(service)*
- [ ] Tray: add "API Explorer" context menu item that opens `http://localhost:{port}/debug` in default browser *(service)*

### 11. Game-to-PC-Mode Binding

Bind games (Steam and non-Steam) to a PC Mode so the mode switches automatically before
launch. Config adds a `DefaultPcMode` (one of the defined modes or `none`) and optional
per-game overrides in `GamePcModeBindings`.

```json
"Steam": {
  "DefaultPcMode": "couch",
  "GamePcModeBindings": {
    "730": "desktop",
    "1245620": "couch"
  }
}
```

When a game launch is requested: if the game has a binding use that PC mode, otherwise fall
back to `DefaultPcMode`. If the resolved mode is `none` or missing, launch without
switching. The mode switch completes before the game launch begins. No automatic revert
on session end — that's handled by sleep or manual mode change.

- [ ] Service: add `DefaultPcMode` and `GamePcModeBindings` to config *(service)*
- [ ] Service: resolve PC mode on game launch — per-game override → default → none *(service)*
- [ ] Service: execute mode switch before `steam://` launch in `SteamService` *(service)*
- [ ] Service: expose bindings via API for settings UI and integration *(service)*
- [ ] Tray: add "Games" settings tab — lists top 20 games, dropdown per game to bind a PC mode, default PC mode selector at top *(service)*
- [ ] Integration: show current game PC mode binding state in attributes *(integration)*

---

## Backlog

- [ ] Power settings tab — default power behavior (sleep on disconnect, etc.) *(service)*
- [ ] Verify Linux headless daemon + systemd user service end-to-end *(service)*
