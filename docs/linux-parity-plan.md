# Linux Platform Parity Plan

## Overview
Bring Linux support to full parity with Windows across 3 feature gaps.
Target: Ubuntu (GNOME) + Arch (KDE). Both X11 and Wayland.

## Phase 1: Idle Detection (Wayland + X11)

**Goal**: Replace X11-only `xprintidle` with a multi-backend idle detector.

**Current**: `LinuxIdleService` shells out to `xprintidle`. Fails silently on Wayland.

**Approach**: Layered detection via D-Bus + CLI fallback:
1. **GNOME (X11 + Wayland)**: `org.gnome.Mutter.IdleMonitor.GetIdletime()` on session bus
2. **logind fallback (KDE, Sway, etc.)**: `org.freedesktop.login1.Session.IdleSinceHintMonotonic` on system bus
3. **X11 fallback**: existing `xprintidle` for non-GNOME X11 sessions

**Detection logic**:
- `XDG_SESSION_TYPE` → `wayland` or `x11`
- `XDG_CURRENT_DESKTOP` → `GNOME`, `KDE`, `sway`, etc.
- Try GNOME D-Bus first, fall back to logind, then xprintidle

**Dependencies**:
- Add `Tmds.DBus.Protocol` NuGet package (NativeAOT-compatible)

**Files to modify**:
- `LinuxIdleService.cs` — rewrite with backend selection
- `HaPcRemote.Core.csproj` — add Tmds.DBus.Protocol

**Tests**:
- Unit test backend selection logic (mock D-Bus responses)
- Unit test fallback chain (GNOME unavailable → logind → xprintidle)

**Verification**:
- `dotnet test` passes
- Manual test on Linux with `curl /api/idle` returning seconds

---

## Phase 2: Auto-Sleep on Linux

**Goal**: Port Windows `AutoSleepService` to work cross-platform.

**Current**: `AutoSleepService` is `[SupportedOSPlatform("windows")]` and only registered in `TrayWebHost.cs`.

**Approach**:
- Remove `[SupportedOSPlatform("windows")]` attribute
- Replace `SystemEvents.PowerModeChanged` (Windows-only) with cross-platform wake detection
- Linux wake detection: monitor `org.freedesktop.login1.Manager.PrepareForSleep(false)` D-Bus signal
- Register `AutoSleepService` in headless `Program.cs`
- Game-running check already works cross-platform via `ISteamService`

**Files to modify**:
- `AutoSleepService.cs` — make cross-platform, abstract wake detection
- `Program.cs` (Headless) — register AutoSleepService

**Tests**:
- Unit test sleep check logic (idle threshold, game running, wake cooldown)
- Existing Windows behavior must not regress

**Verification**:
- `dotnet test` passes
- Config: `PcRemote:Power:AutoSleepAfterMinutes` works on Linux

---

## Phase 3: Monitor Profiles on Linux

**Goal**: Save/restore monitor configurations on Linux.

**Current**: `LinuxMonitorService.GetProfilesAsync()` returns empty list. `ApplyProfileAsync()` throws.

**Approach**: Compositor-agnostic profile storage + compositor-specific apply:
1. **Profile format**: JSON files in `monitor-profiles/` dir (same path as Windows .cfg files)
   - Store: output name, resolution, refresh rate, position, scale, primary flag
   - Fingerprint by EDID/output name for matching
2. **Save**: Snapshot current config via detection backend
3. **Apply**: Restore config via compositor-specific tool

**Backends**:
| Environment | Save (read config) | Apply (write config) |
|---|---|---|
| X11 | `xrandr --query` | `xrandr --output ... --mode ... --pos ...` |
| GNOME Wayland | `org.gnome.Mutter.DisplayConfig.GetCurrentState()` D-Bus | `ApplyMonitorsConfig()` D-Bus |
| KDE Wayland | `kscreen-doctor -o` | `kscreen-doctor output.NAME.mode.WxH@R` |

**Detection**: Same env var logic as Phase 1.

**Files to modify**:
- `LinuxMonitorService.cs` — add profile save/load/apply with backend selection
- Add `MonitorProfileData.cs` model for JSON serialization

**Tests**:
- Unit test profile serialization/deserialization
- Unit test backend selection
- Unit test xrandr output parsing (already partially exists)

**Verification**:
- `dotnet test` passes
- `GET /api/monitor/profiles` returns saved profiles
- `POST /api/monitor/set/{profile}` applies correctly

---

## Execution Order

1. Create feature branch `feature/linux-parity`
2. Implement Phase 1 (idle detection)
3. Run tests, verify
4. Implement Phase 2 (auto-sleep)
5. Run tests, verify
6. Implement Phase 3 (monitor profiles)
7. Run tests, verify
8. Create PR, review, merge
9. Release, deploy to Linux target, integration test

## NuGet Dependencies
- `Tmds.DBus.Protocol` — D-Bus client, NativeAOT-compatible, covers idle + monitor + sleep signals
