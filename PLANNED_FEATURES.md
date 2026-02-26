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

- [x] **Kestrel status stuck on "Starting..."** — Fixed: synchronous fast path in
  `GeneralTab.UpdatePortStatus()` when `KestrelStatus.Started` is already completed. *(service)*

- [x] **Update race condition** — Fixed: `SemaphoreSlim` guard in `HandleDownloadAsync`
  prevents concurrent manual + auto-update downloads. *(service)*

- [x] **Update button color** — Fixed: removed stale `BackColor` reset. *(service)*

### 7. Rename Duration Sensor to Idle Duration *(done in v1.2)*

- [x] Service: renamed log message from "idle time" → "idle duration" *(service)*
- [x] Integration: renamed sensor to "Idle Duration" (`idle_duration` entity ID, translations, strings) *(integration)*

### 8. Non-Steam Game Discovery *(done in v1.2)*

Parses `shortcuts.vdf` (binary VDF via ValveKeyValue) to discover non-Steam game shortcuts.
Shortcuts merge into the game list with `IsShortcut` flag and launch via shifted
`steam://rungameid/` URI. CRC32-based appid generation matches Steam's algorithm.

- [x] Service: parse `shortcuts.vdf`, merge into game list, launch via shifted appid *(service)*
- [x] Integration: non-Steam games appear in media browser automatically (no changes needed — data flows through existing game list) *(integration)*

### 9. Steam Artwork / Poster Serving *(done in v1.2)*

`GET /api/steam/artwork/{appId}` serves game artwork from Steam's local cache.
Resolution order: custom grid art (`userdata/{steamid}/config/grid/{appId}p.*`) →
library cache (`appcache/librarycache/{appId}_library_600x900.*`). `SteamUserIdResolver`
discovers the active user via `loginusers.vdf`.

- [x] Service: artwork endpoint with grid → librarycache fallback *(service)*
- [x] Integration: media browser thumbnails use local artwork endpoint instead of Steam CDN *(integration)*

### 10. API Debug Page *(done in v1.2)*

Self-hosted HTML page at `GET /debug` (localhost-only, excluded from auth). Lists all
endpoints with method, path, description, and "Try it" buttons. API key auto-injected
via `<meta>` tag. Dark theme, no external dependencies.

- [x] Service: `/debug` endpoint, localhost-only, API key injection, endpoint catalog *(service)*
- [x] Tray: "API Explorer" context menu item *(service)*

### 11. Game-to-PC-Mode Binding *(done in v1.2)*

Per-game and default PC mode bindings. Mode switch executes before game launch.
Config in `Steam.DefaultPcMode` + `Steam.GamePcModeBindings`. Games settings tab
in tray with per-game dropdown.

```json
"Steam": {
  "DefaultPcMode": "couch",
  "GamePcModeBindings": {
    "730": "desktop",
    "1245620": "couch"
  }
}
```

- [x] Service: config, resolution logic (per-game → default → none), mode switch before launch *(service)*
- [x] Service: `GET/PUT /api/steam/bindings` endpoints *(service)*
- [x] Tray: "Games" settings tab with per-game mode dropdown *(service)*
- [x] Integration: `game_pc_mode_binding` attribute on media player entity *(integration)*

---

## v1.2.2

### 14. Steam Cold-Start Support

`steam_ready` in system state signals when Steam is up and ready. Prevents game launch
commands silently failing when Steam isn't running. Integration auto-launches Steam via
the existing app system and waits for readiness before sending the game command.

Auto-detects Steam path from registry on startup and writes a default `"steam"` app
config entry if missing — no manual setup required.

**Service:**
- [x] Add `SteamReady: bool` to `GET /api/system/state` response *(service)*
- [x] Add `UseShellExecute: bool` to `AppDefinitionOptions` (default `false`) *(service)*
- [x] Pass `UseShellExecute` through `DirectAppLauncher.LaunchAsync` *(service)*
- [x] On startup: detect Steam exe from registry, write default `"steam"` app entry (`-bigpicture` args) if not already configured *(service)*

**Integration:**
- [x] Read `steam_ready` from system state in coordinator *(integration)*
- [x] Cold path: replace fixed 15 s sleep with poll-until-`steam_ready` (max 2 min) *(integration)*
- [x] Warm path: check `steam_ready`; if false → launch `"steam"` app, wait for `steam_ready`, then launch game *(integration)*

---

## v1.3

### Bugs

- [ ] **Game artwork returns 401** — `media_image_url` and browse_media thumbnails are plain URLs that HA fetches without headers. `ApiKeyMiddleware` blocks all `/api/` paths not in `ExemptPaths`, so `/api/steam/artwork/{appId}` returns 401. Fix: add `/api/steam/artwork` prefix to `ExemptPaths` in `ApiKeyMiddleware`. Artwork is read-only local data — same rationale as `/debug`. *(service)*

- [ ] **Non-Steam games show idle when launched via integration** — When a non-Steam game is launched manually from Steam, the overlay and friends activity both show correctly — Steam tracks and reports the game as running. When launched via the integration (`steam://rungameid/{shiftedId}`), the integration always shows idle. The launch path through the integration is bypassing Steam's game tracking. Needs investigation: determine why the same `steam://` URI behaves differently when invoked by the service vs. a user clicking in Steam. Possible causes: process ancestry (Steam may only track games it directly spawns from its own UI), timing (game registers before Steam is ready), or the shifted appid not matching Steam's tracking lookup. *(service)*

- [x] **turn_off connection error** — HA throws `CannotConnectError` on `switch/turn_off` and `media_player/turn_off` after sleep. PC suspends mid-request so `_TIMEOUT` fires → `TimeoutError` → `CannotConnectError` bubbles uncaught. Fixed: wrap `sleep()` in both `switch.py` and `media_player.py` to catch `CannotConnectError` and treat it as success. *(integration)*

- [ ] **Tray "Log" menu item opens Power tab instead of Log tab** — The right-click context menu "Log" item navigates to the wrong tab when opening the settings window. Fix: ensure the `Log` menu handler sets the tab control's `SelectedTab` to the Log tab, not the Power tab. Likely a copy-paste error in the tab index or tab reference. *(service)*

- [ ] **Steam Big Picture not auto-registered as app entry** — The startup bootstrapper auto-writes a `"steam"` app entry but does not write a separate `"steam-bigpicture"` entry (Steam exe + `-bigpicture` argument). PC mode dropdowns therefore don't include `steam-bigpicture` as a selectable launch app without manual config. Additionally, the bootstrapper currently only runs on first install — migrated installations that already have a `"steam"` entry will never receive `"steam-bigpicture"`. Fix: run the bootstrapper on every startup and write any missing well-known entries (`"steam"`, `"steam-bigpicture"`) unconditionally if absent, so upgrades self-heal. *(service)*

- [ ] **HACS install instructions missing repository step** — The README skips the "Add custom repository" step before searching. The user must first add the repo URL in HACS before the integration appears in search results. Fix: update the HACS section to (1) add the repo URL as a custom repository with a fenced code block for easy copy, then (2) search and install. *(integration)*

- [ ] **Info tooltip suppressed on click** — Clicking a `ⓘ` label in the tray settings UI dismisses the tooltip instead of showing it. Hovering shows the tooltip correctly after 1–2 s. Cause: a click focuses the label (or its parent), which triggers tooltip hide logic. Fix: intercept the `Click` event on the info label and explicitly call `ToolTip.Show()` with a fixed duration, or use `MouseClick` to force-show it. *(service)*

- [ ] **Games tab PC mode dropdown throws `ThreadStateException`** — Opening or interacting with the PC mode `ComboBox` column in the games `DataGridView` throws: `System.Threading.ThreadStateException: Current thread must be set to single thread apartment (STA) mode before OLE calls can be made` inside `ComboBox.set_AutoCompleteSource` → `DataGridViewComboBoxCell.InitializeEditingControl`. Root cause: `AutoCompleteSource` is being set on a `DataGridViewComboBoxCell` which triggers OLE on a non-STA thread, or the cell is configured with an `AutoCompleteSource` that isn't compatible with in-grid combo editing. Fix: either remove `AutoCompleteSource` from the in-grid combo column (autocomplete isn't needed there — the dropdown is already constrained to mode names), or handle the `DataGridView.DataError` event to suppress the dialog until the root cause is resolved. *(service)*

- [ ] **Update check fails with "no such host: api.github.com"** — The auto-update check throws a DNS resolution error on some networks/configurations instead of silently failing. Should catch `HttpRequestException` (and `SocketException`) in the update check path and treat it as "no update available" — same as a 404 or timeout. Log at debug level, not error. *(service)*

- [ ] **Game launch buffers for ~3 minutes** — Launching a game via the media player does trigger the cold-start flow, but the initial buffering state lasts up to 3 minutes. Additionally, the wake-on-LAN retry logic used in the power-on path (WoL packets sent for 20 s straight) is not applied here — game launch uses a single attempt or short wait instead of the sustained retry loop. Should reuse the same WoL retry mechanism to ensure the machine receives the packet before waiting for `steam_ready`. *(integration)*

- [ ] **Stop game: be optimistic for 30 s** — When stopping a game (`media_player.media_stop` or equivalent), the command is sent but the media player immediately reflects the failure/idle state if the PC doesn't respond instantly. Be optimistic: assume the stop succeeded and hold the previous state for up to 30 seconds before reflecting the actual polled state. Mirrors the WoL optimism pattern used for power-on. *(integration)*

---

## Backlog

### 12. Auto-Sleep on Inactivity

Auto-sleep the PC when it's been idle for a configurable duration. Conditions:
no game running, no mouse/keyboard/gamepad input for X minutes → sleep.

**Service-side:**
- Config: `Power.AutoSleepAfterMinutes` in `appsettings.json` (0 = disabled)
- Monitor loop: checks game state via `ISteamService` + input idle time via
  `GetLastInputInfo` / `loginctl` — if both exceed threshold, trigger sleep
- Power settings tab in tray with timeout slider/input
- `GET/PUT /api/system/power` endpoints to read/write config remotely

**HA integration:**
- Number entity "Auto-Sleep Timeout" to adjust minutes from dashboard
- Complements the post-session sleep blueprint (F4) which handles the
  "game just ended" case — this covers the broader "PC sitting idle" scenario

**Investigation — gamepad input detection:**
The current plan mentions gamepad integration as a prerequisite for detecting user activity. Investigate whether we can instead listen to gamepad connect/disconnect events (e.g. Windows `RawInput`/`XInput` device arrival, or Linux `udev` events / `/dev/input` monitoring) to infer activity without full gamepad integration. If connect/disconnect events are reliable enough (gamepad turns off when user is done), this could replace or supplement the `GetLastInputInfo` idle check without needing to read axis/button state.

```json
"Power": {
  "AutoSleepAfterMinutes": 30
}
```

- [ ] Service: inactivity monitor loop + `Power.AutoSleepAfterMinutes` config *(service)*
- [ ] Service: Power settings tab in tray *(service)*
- [ ] Service: `GET/PUT /api/system/power` endpoints *(service)*
- [ ] Integration: number entity for auto-sleep timeout *(integration)*

### 13. Help Tooltips for All UI Elements

Add contextual help to every setting in the tray app. Each field gets a `ToolTip`
with a small "ⓘ" icon label explaining what the setting does.

- [ ] Service: add `ToolTip` component + help icons to Modes tab *(service)*
- [ ] Service: add help icons to Games tab *(service)*
- [ ] Service: add help icons to General tab *(service)*
- [ ] Service: add help icons to Power tab (when built) *(service)*

---

### 19. Integration Brand Icons

HA 2026.3 supports bundling brand images directly in the custom integration — no external CDN or separate brands repo required. Local files take priority over CDN automatically.

Add a `brand/` directory under `custom_components/pc_remote/` with:

```
custom_components/pc_remote/
└── brand/
    ├── icon.png          (256×256 integration icon)
    ├── dark_icon.png
    ├── logo.png          (full wordmark/logo)
    └── dark_logo.png
```

- [ ] Integration: create `brand/` folder with `icon.png` and `logo.png` (light + dark variants) *(integration)*

---

### 18. Steam Logo as Media Player Artwork When Idle

Show the Steam logo on the HA media player card when the PC is online but no game is running (`IDLE` state). Currently `media_image_url` returns `None` in idle state, leaving the card blank.

**Integration only.** When the media player is `IDLE` (online, no game running), return a Steam logo URL from the public Steam CDN as `media_image_url`. No service changes needed — no bundled assets, no licensing concerns.

URL: `https://store.steampowered.com/public/shared/images/header/logo_steam_steam.png` (or equivalent stable CDN URL).

- [ ] Integration: return Steam CDN logo URL from `media_image_url` when idle *(integration)*

---

### 17. App Key Autocomplete for LaunchApp / KillApp in Modes Tab

`_launchAppCombo` and `_killAppCombo` currently use `DropDownStyle.DropDownList` — only configured app keys are selectable. Switch to `DropDownStyle.DropDown` with `AutoCompleteMode.SuggestAppend` so the user can type a custom app key or pick from suggestions.

Suggestion list order: configured apps from `PcRemoteOptions.Apps` first, then well-known built-ins (`steam`, `steam-bigpicture`) if not already present.

Fully compatible: `ModeConfig.LaunchApp` / `KillApp` are already `string?` and accept any key. The `AppDropdownItem` wrapper needs to handle free-text input (read `ComboBox.Text` instead of casting `SelectedItem` when no item is selected).

- [ ] Service: change `_launchAppCombo` and `_killAppCombo` to `DropDownStyle.DropDown` with autocomplete *(service)*
- [ ] Service: populate autocomplete source from `Apps` keys + well-known built-ins *(service)*
- [ ] Service: update save logic to read `.Text` when `SelectedItem` is null (free-text path) *(service)*

---

### 16. Immediate Row Creation on "Add New" in PC Mode UI

When clicking Add in the Modes tab, immediately insert a new blank row in the list and select it. Editing then targets the new entry, not whichever row was previously selected. Prevents accidental overwrites of existing modes when a user clicks Add without first deselecting.

- [ ] Service: Add inserts a blank placeholder row and selects it immediately *(service)*
- [ ] Service: Form fields clear and bind to the new row on creation *(service)*
- [ ] Service: Discard/Cancel removes the uncommitted row if the user abandons it *(service)*

---

### 15. Apply Button for Settings UI

Replace auto-save on change with an explicit Apply button. Settings are staged in memory and only written to disk when Apply is clicked. Discard/Cancel reverts unsaved changes.

- [ ] Service: add Apply and Cancel buttons to each settings tab *(service)*
- [ ] Service: defer config writes until Apply is clicked *(service)*
- [ ] Service: mark tabs dirty when unsaved changes exist (e.g. tab title asterisk or button enabled state) *(service)*
- [ ] Service: Cancel/discard reloads current config from disk and resets form fields *(service)*

---

### Double-Click Tray Icon Opens Settings

Double-clicking the system tray icon should open the settings window — same as clicking "Settings" from the context menu. Standard Windows tray convention.

- [ ] Service: handle `NotifyIcon.DoubleClick` event and open the settings form *(service)*

---

### Other

- [ ] Verify Linux headless daemon + systemd user service end-to-end *(service)*
