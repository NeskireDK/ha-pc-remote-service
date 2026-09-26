using System.ComponentModel;
using System.Runtime.Versioning;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Native;
using Microsoft.Extensions.Logging;
using static HaPcRemote.Service.Native.DisplayConfigApi;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsMonitorService : IMonitorService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private const SetDisplayConfigFlags RestoreSavedLayoutFlags =
        SetDisplayConfigFlags.SDC_APPLY
        | SetDisplayConfigFlags.SDC_TOPOLOGY_SUPPLIED
        | SetDisplayConfigFlags.SDC_ALLOW_PATH_ORDER_CHANGES;

    private const SetDisplayConfigFlags SuppliedConfigFlags =
        SetDisplayConfigFlags.SDC_APPLY
        | SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG
        | SetDisplayConfigFlags.SDC_ALLOW_CHANGES
        | SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE;

    private readonly IDisplayConfigApi _api;
    private readonly ILogger<WindowsMonitorService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private List<MonitorInfo>? _cachedMonitors;
    private DateTime _cacheTime;

    // Maps MonitorId (e.g. "GSM59A4") → native (adapterId, targetId) for path resolution
    private readonly Dictionary<string, (LUID adapterId, uint targetId)> _targetKeys = new(StringComparer.OrdinalIgnoreCase);

    public WindowsMonitorService(IDisplayConfigApi api, ILogger<WindowsMonitorService> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ── Query ─────────────────────────────────────────────────────────

    public async Task<List<MonitorInfo>> GetMonitorsAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_cachedMonitors is not null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return _cachedMonitors;

            _cachedMonitors = QueryMonitors();
            _cacheTime = DateTime.UtcNow;
            return _cachedMonitors;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    internal List<MonitorInfo> QueryMonitors()
    {
        _logger.LogDebug("QueryMonitors: starting enumeration");
        var (paths, modes) = _api.QueryConfig(QueryDisplayConfigFlags.QDC_ALL_PATHS);
        var monitors = new List<MonitorInfo>();
        var seen = new HashSet<(LUID adapterId, uint targetId)>();
        var edidCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        _targetKeys.Clear();

        _logger.LogDebug("QueryMonitors: processing {Count} paths", paths.Length);

        var unavailableCount = 0;
        var duplicateCount = 0;

        foreach (var path in paths)
        {
            if (path.targetInfo.targetAvailable == 0)
            {
                unavailableCount++;
                continue;
            }

            var key = (path.targetInfo.adapterId, path.targetInfo.id);
            var isActive = (path.flags & DISPLAYCONFIG_PATH_FLAGS.ACTIVE) != 0;

            // Prefer the active path when deduplicating
            if (seen.Contains(key))
            {
                var existingIdx = monitors.FindIndex(m =>
                    _targetKeys.TryGetValue(m.MonitorId, out var k) && k == key);
                if (existingIdx >= 0 && !monitors[existingIdx].IsActive && isActive)
                {
                    var oldId = monitors[existingIdx].MonitorId;
                    _logger.LogDebug("  Replacing inactive duplicate {OldId} with active path", oldId);
                    monitors.RemoveAt(existingIdx);
                    _targetKeys.Remove(oldId);

                    var oldBaseId = oldId.Contains('#') ? oldId[..oldId.IndexOf('#')] : oldId;
                    if (edidCounts.TryGetValue(oldBaseId, out var c))
                        edidCounts[oldBaseId] = c - 1;
                }
                else
                {
                    duplicateCount++;
                    continue;
                }
            }

            seen.Add(key);

            string friendlyName;
            ushort edidMfg, edidProduct;
            string gdiName;

            try
            {
                (friendlyName, edidMfg, edidProduct) = _api.GetTargetDeviceInfo(path.targetInfo.adapterId, path.targetInfo.id);
                gdiName = isActive
                    ? _api.GetSourceGdiName(path.sourceInfo.adapterId, path.sourceInfo.id)
                    : "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get device info for target {TargetId}", path.targetInfo.id);
                continue;
            }

            var baseId = FormatEdidId(edidMfg, edidProduct);
            edidCounts.TryGetValue(baseId, out var count);
            count++;
            edidCounts[baseId] = count;

            var monitorId = count == 1 ? baseId : $"{baseId}#{count}";
            _targetKeys[monitorId] = key;

            int width = 0, height = 0, hz = 0;
            var isPrimary = false;

            if (isActive && path.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
            {
                var sourceMode = FindSourceMode(modes, path.sourceInfo.modeInfoIdx);
                if (sourceMode.HasValue)
                {
                    width = (int)sourceMode.Value.width;
                    height = (int)sourceMode.Value.height;
                    isPrimary = sourceMode.Value.position.x == 0 && sourceMode.Value.position.y == 0;
                }
            }

            if (isActive && path.targetInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
            {
                var targetMode = FindTargetMode(modes, path.targetInfo.modeInfoIdx);
                if (targetMode.HasValue)
                    hz = targetMode.Value.targetVideoSignalInfo.vSyncFreq.ToHz();
            }

            if (hz == 0)
                hz = path.targetInfo.refreshRate.ToHz();

            _logger.LogDebug(
                "  Monitor: id={MonitorId} name=\"{FriendlyName}\" gdi={Gdi} {W}x{H}@{Hz}Hz active={Active} primary={Primary}",
                monitorId, friendlyName, gdiName, width, height, hz, isActive, isPrimary);

            monitors.Add(new MonitorInfo
            {
                Name = gdiName,
                MonitorId = monitorId,
                SerialNumber = null,
                MonitorName = friendlyName,
                Width = width,
                Height = height,
                DisplayFrequency = hz,
                IsActive = isActive,
                IsPrimary = isPrimary,
            });
        }

        _logger.LogDebug("QueryMonitors: skipped {Unavailable} unavailable and {Duplicate} duplicate paths", unavailableCount, duplicateCount);
        _logger.LogDebug("QueryMonitors: found {Count} monitors", monitors.Count);
        foreach (var m in monitors)
            _logger.LogDebug("  {Id}: \"{Name}\" ({Gdi}) {W}x{H}@{Hz}Hz active={Active} primary={Primary}",
                m.MonitorId, m.MonitorName, m.Name, m.Width, m.Height, m.DisplayFrequency, m.IsActive, m.IsPrimary);

        return monitors;
    }

    // ── Control ───────────────────────────────────────────────────────

    public Task EnableMonitorAsync(string id)
    {
        var monitors = InvalidateAndQueryMonitors();
        var target = FindMonitor(monitors, id);

        if (target.IsActive)
        {
            _logger.LogDebug("Monitor '{Id}' is already enabled, skipping", id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Enabling monitor: {Name} ({Id})", target.MonitorName, target.MonitorId);
        SwitchTo([.. monitors.Where(m => m.IsActive), target], $"Enable monitor '{id}'");
        return Task.CompletedTask;
    }

    public Task DisableMonitorAsync(string id)
    {
        var monitors = InvalidateAndQueryMonitors();
        var target = FindMonitor(monitors, id);

        if (!target.IsActive)
        {
            _logger.LogDebug("Monitor '{Id}' is already disabled, skipping", id);
            return Task.CompletedTask;
        }

        var remaining = monitors.Where(m => m.IsActive && !MatchesId(m, id)).ToList();
        if (remaining.Count == 0)
            throw new InvalidOperationException($"Cannot disable '{id}': it is the only active monitor.");

        _logger.LogInformation("Disabling monitor: {Name} ({Id})", target.MonitorName, target.MonitorId);
        SwitchTo(remaining, $"Disable monitor '{id}'");
        return Task.CompletedTask;
    }

    public Task SoloMonitorAsync(string id)
    {
        var monitors = InvalidateAndQueryMonitors();
        var target = FindMonitor(monitors, id);
        var active = monitors.Where(m => m.IsActive).ToList();

        if (active.Count == 1 && MatchesId(active[0], id))
        {
            _logger.LogDebug("Monitor '{Id}' is already the only active monitor, skipping", id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Solo monitor: {Name} ({Id})", target.MonitorName, target.MonitorId);
        SwitchTo([target], $"Solo monitor '{id}'");
        return Task.CompletedTask;
    }

    public Task SetPrimaryAsync(string id)
    {
        var monitors = InvalidateAndQueryMonitors();
        var target = FindMonitor(monitors, id);

        if (!target.IsActive)
            throw new InvalidOperationException($"Cannot make '{id}' primary: it is not active. Enable it first.");

        if (target.IsPrimary)
        {
            _logger.LogDebug("Monitor {Id} is already primary", id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Setting primary monitor: {Name} ({Id})", target.MonitorName, target.MonitorId);
        var targetKey = ResolveTargetKey(target);
        RetryOnTransientFailure(() =>
        {
            var (paths, modes) = _api.QueryConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS);
            DisplayTopologyBuilder.MoveToOrigin(paths, modes, targetKey);
            _api.ApplyConfig(paths, modes, SuppliedConfigFlags);
        });
        InvalidateCache();

        if (!FindMonitor(QueryMonitors(), id).IsPrimary)
            throw new InvalidOperationException($"Set primary '{id}' did not take effect.");

        return Task.CompletedTask;
    }

    /// <summary>Makes exactly <paramref name="wanted"/> active, restoring Windows' saved layout for that set when one exists.</summary>
    private void SwitchTo(IReadOnlyList<MonitorInfo> wanted, string action)
    {
        var wantedKeys = wanted.Select(ResolveTargetKey).ToList();
        RetryOnTransientFailure(() => ApplyTopology(wantedKeys));
        InvalidateCache();

        var active = QueryMonitors().Where(m => m.IsActive).ToList();
        var wantedIds = wanted.Select(m => m.MonitorId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasExactlyWanted = active.Count == wantedIds.Count && active.All(m => wantedIds.Contains(m.MonitorId));
        var isCloned = active.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() < active.Count;

        if (hasExactlyWanted && !isCloned)
        {
            _logger.LogDebug("{Action} verified", action);
            return;
        }

        var activeNames = string.Join(", ", active.Select(m => $"{m.MonitorName} ({m.MonitorId}) on {m.Name}"));
        throw new InvalidOperationException($"{action} did not take effect. Active monitors: [{activeNames}]");
    }

    private void ApplyTopology(IReadOnlyList<(LUID adapterId, uint targetId)> wantedKeys)
    {
        var (allPaths, _) = _api.QueryConfig(QueryDisplayConfigFlags.QDC_ALL_PATHS);
        var (activePaths, activeModes) = _api.QueryConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS);
        var paths = DisplayTopologyBuilder.BuildPaths(allPaths, activePaths, wantedKeys);

        try
        {
            _api.ApplyConfig(paths, [], RestoreSavedLayoutFlags);
            _logger.LogInformation("Applied Windows' saved layout for {Count} monitor(s)", paths.Length);
            return;
        }
        catch (Win32Exception ex)
        {
            _logger.LogInformation(
                "No saved Windows layout for this set of monitors (error {Code}); keeping current modes, Windows picks the rest",
                ex.NativeErrorCode);
        }

        var (keptPaths, keptModes) = DisplayTopologyBuilder.KeepActiveModes(paths, activePaths, activeModes);
        _api.ApplyConfig(keptPaths, keptModes, SuppliedConfigFlags);
    }

    private List<MonitorInfo> InvalidateAndQueryMonitors()
    {
        InvalidateCache();
        return QueryMonitors();
    }

    // ── Helpers ────────────────────────────────────────────────────────

    internal void InvalidateCache()
    {
        _cachedMonitors = null;
    }

    internal int[] RetryDelaysMs = [500, 1000, 2000];

    /// <summary>Retries <paramref name="apply"/> on error 31, which the driver returns while a previous change is still settling.</summary>
    private void RetryOnTransientFailure(Action apply)
    {
        for (var attempt = 0;; attempt++)
        {
            try
            {
                apply();
                return;
            }
            catch (Win32Exception ex) when (attempt < RetryDelaysMs.Length && ex.NativeErrorCode == ERROR_GEN_FAILURE)
            {
                var delay = RetryDelaysMs[attempt];
                _logger.LogWarning("SetDisplayConfig failed with error 31 on attempt {Attempt}, retrying in {Delay}ms",
                    attempt + 1, delay);
                Thread.Sleep(delay);
            }
        }
    }

    private (LUID adapterId, uint targetId) ResolveTargetKey(MonitorInfo monitor)
    {
        if (_targetKeys.TryGetValue(monitor.MonitorId, out var key))
            return key;

        throw new KeyNotFoundException($"No native target key found for monitor '{monitor.MonitorId}'. Was GetMonitorsAsync() called first?");
    }

    internal static MonitorInfo FindMonitor(List<MonitorInfo> monitors, string id) =>
        MonitorMatchHelper.FindMonitor(monitors, id);

    internal static bool MatchesId(MonitorInfo m, string id) =>
        MonitorMatchHelper.MatchesId(m, id);

    /// <summary>
    /// Decodes EDID manufacturer ID (big-endian compressed PNP) + product code into "GSM59A4" format.
    /// </summary>
    internal static string FormatEdidId(ushort edidManufacturerId, ushort edidProductCodeId)
    {
        // EDID manufacturer ID is big-endian 3x5-bit compressed ASCII
        // Swap bytes from big-endian to native
        var mfg = (ushort)((edidManufacturerId >> 8) | (edidManufacturerId << 8));

        var c1 = (char)('A' + ((mfg >> 10) & 0x1F) - 1);
        var c2 = (char)('A' + ((mfg >> 5) & 0x1F) - 1);
        var c3 = (char)('A' + (mfg & 0x1F) - 1);

        return $"{c1}{c2}{c3}{edidProductCodeId:X4}";
    }

    private static DISPLAYCONFIG_SOURCE_MODE? FindSourceMode(DISPLAYCONFIG_MODE_INFO[] modes, uint index)
    {
        if (index >= modes.Length)
            return null;
        return modes[index].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE
            ? modes[index].info.sourceMode
            : null;
    }

    private static DISPLAYCONFIG_TARGET_MODE? FindTargetMode(DISPLAYCONFIG_MODE_INFO[] modes, uint index)
    {
        if (index >= modes.Length)
            return null;
        return modes[index].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.TARGET
            ? modes[index].info.targetMode
            : null;
    }
}
