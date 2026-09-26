using System.Runtime.Versioning;
using static HaPcRemote.Service.Native.DisplayConfigApi;

namespace HaPcRemote.Service.Services;

/// <summary>Pure builders for SetDisplayConfig path and mode arrays.</summary>
[SupportedOSPlatform("windows")]
internal static class DisplayTopologyBuilder
{
    /// <summary>
    /// One active path per wanted target, each on its own source so Windows extends rather than clones.
    /// Targets that are already active keep their current source.
    /// </summary>
    public static DISPLAYCONFIG_PATH_INFO[] BuildPaths(
        DISPLAYCONFIG_PATH_INFO[] allPaths,
        DISPLAYCONFIG_PATH_INFO[] activePaths,
        IReadOnlyList<(LUID adapterId, uint targetId)> wantedTargets)
    {
        var usedSources = new HashSet<(LUID adapterId, uint sourceId)>();
        var chosen = new Dictionary<(LUID adapterId, uint targetId), DISPLAYCONFIG_PATH_INFO>();

        foreach (var target in wantedTargets)
        {
            var active = FirstPathWithFreeSource(activePaths, target, usedSources);
            if (active is { } path)
                Claim(path, target, chosen, usedSources);
        }

        foreach (var target in wantedTargets.Where(t => !chosen.ContainsKey(t)))
        {
            var path = FirstPathWithFreeSource(allPaths, target, usedSources)
                ?? throw new InvalidOperationException(
                    $"No free display source can drive target {target.targetId}; the GPU may not support this many monitors at once.");
            Claim(path, target, chosen, usedSources);
        }

        return wantedTargets.Select(t => AsTopologyPath(chosen[t])).ToArray();
    }

    /// <summary>
    /// Attaches the current source and target modes to paths that were already active, so a fallback apply
    /// keeps their resolution, refresh rate and position. New paths are left for Windows to fill in.
    /// </summary>
    public static (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) KeepActiveModes(
        DISPLAYCONFIG_PATH_INFO[] paths,
        DISPLAYCONFIG_PATH_INFO[] activePaths,
        DISPLAYCONFIG_MODE_INFO[] activeModes)
    {
        var keptPaths = (DISPLAYCONFIG_PATH_INFO[])paths.Clone();
        var keptModes = new List<DISPLAYCONFIG_MODE_INFO>();

        for (var i = 0; i < keptPaths.Length; i++)
        {
            var match = Array.FindIndex(activePaths, a => IsSameRoute(a, keptPaths[i]) && HasValidModes(a, activeModes.Length));
            if (match < 0)
                continue;

            keptPaths[i].sourceInfo.modeInfoIdx = (uint)keptModes.Count;
            keptModes.Add(activeModes[activePaths[match].sourceInfo.modeInfoIdx]);
            keptPaths[i].targetInfo.modeInfoIdx = (uint)keptModes.Count;
            keptModes.Add(activeModes[activePaths[match].targetInfo.modeInfoIdx]);
        }

        var modes = keptModes.ToArray();
        var firstSourceMode = Array.FindIndex(modes, m => m.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE);
        var anyAtOrigin = modes.Any(m => m.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE && IsOrigin(m.info.sourceMode.position));
        if (firstSourceMode >= 0 && !anyAtOrigin)
            ShiftSourceModes(modes, modes[firstSourceMode].info.sourceMode.position);

        return (keptPaths, modes);
    }

    /// <summary>Shifts every source mode so the target's desktop sits at (0,0), which makes it primary.</summary>
    public static void MoveToOrigin(
        DISPLAYCONFIG_PATH_INFO[] activePaths,
        DISPLAYCONFIG_MODE_INFO[] modes,
        (LUID adapterId, uint targetId) target)
    {
        var path = Array.FindIndex(activePaths, p =>
            IsTarget(p, target) && p.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID);
        if (path < 0)
            throw new InvalidOperationException($"Could not find the source mode for target {target.targetId}.");

        ShiftSourceModes(modes, modes[activePaths[path].sourceInfo.modeInfoIdx].info.sourceMode.position);
    }

    private static DISPLAYCONFIG_PATH_INFO? FirstPathWithFreeSource(
        DISPLAYCONFIG_PATH_INFO[] paths,
        (LUID adapterId, uint targetId) target,
        HashSet<(LUID adapterId, uint sourceId)> usedSources)
    {
        foreach (var path in paths)
        {
            if (IsTarget(path, target) && !usedSources.Contains(SourceOf(path)))
                return path;
        }
        return null;
    }

    private static void Claim(
        DISPLAYCONFIG_PATH_INFO path,
        (LUID adapterId, uint targetId) target,
        Dictionary<(LUID adapterId, uint targetId), DISPLAYCONFIG_PATH_INFO> chosen,
        HashSet<(LUID adapterId, uint sourceId)> usedSources)
    {
        chosen[target] = path;
        usedSources.Add(SourceOf(path));
    }

    private static DISPLAYCONFIG_PATH_INFO AsTopologyPath(DISPLAYCONFIG_PATH_INFO path)
    {
        path.flags = DISPLAYCONFIG_PATH_FLAGS.ACTIVE;
        path.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        path.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        return path;
    }

    private static void ShiftSourceModes(DISPLAYCONFIG_MODE_INFO[] modes, POINTL newOrigin)
    {
        for (var i = 0; i < modes.Length; i++)
        {
            if (modes[i].infoType != DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE)
                continue;
            modes[i].info.sourceMode.position.x -= newOrigin.x;
            modes[i].info.sourceMode.position.y -= newOrigin.y;
        }
    }

    private static bool IsTarget(DISPLAYCONFIG_PATH_INFO path, (LUID adapterId, uint targetId) target) =>
        path.targetInfo.adapterId == target.adapterId && path.targetInfo.id == target.targetId;

    private static bool IsSameRoute(DISPLAYCONFIG_PATH_INFO a, DISPLAYCONFIG_PATH_INFO b) =>
        SourceOf(a) == SourceOf(b) && IsTarget(a, (b.targetInfo.adapterId, b.targetInfo.id));

    private static bool HasValidModes(DISPLAYCONFIG_PATH_INFO path, int modeCount) =>
        path.sourceInfo.modeInfoIdx < modeCount && path.targetInfo.modeInfoIdx < modeCount;

    private static (LUID adapterId, uint sourceId) SourceOf(DISPLAYCONFIG_PATH_INFO path) =>
        (path.sourceInfo.adapterId, path.sourceInfo.id);

    private static bool IsOrigin(POINTL position) => position.x == 0 && position.y == 0;
}
