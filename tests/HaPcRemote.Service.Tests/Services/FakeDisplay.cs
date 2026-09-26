using System.ComponentModel;
using HaPcRemote.Service.Native;
using static HaPcRemote.Service.Native.DisplayConfigApi;

namespace HaPcRemote.Service.Tests.Services;

/// <summary>
/// Stateful stand-in for the Windows CCD API: every target is reachable from every source, like real
/// QDC_ALL_PATHS output, and SetDisplayConfig enforces the rules that broke the old implementation.
/// </summary>
internal sealed class FakeDisplay : IDisplayConfigApi
{
    public const int ErrorNoSavedLayout = 1610;

    public static readonly LUID Adapter = new() { LowPart = 0xC748, HighPart = 0 };

    public sealed record Monitor(uint TargetId, string Name, ushort EdidManufacturer, ushort EdidProduct, uint Width, uint Height, uint Hz);

    public sealed record Placement(uint SourceId, uint Width, uint Height, uint Hz, int X, int Y);

    public sealed record Apply(DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes, SetDisplayConfigFlags Flags);

    private readonly List<Monitor> _monitors = [];
    private readonly Dictionary<string, Dictionary<uint, Placement>> _savedLayouts = [];

    public Dictionary<uint, Placement> Active { get; private set; } = [];
    public List<Apply> Applies { get; } = [];
    public Queue<int> ApplyErrors { get; } = new();
    public bool IgnoresApplies { get; set; }
    public uint SourceCount { get; set; } = 4;

    public FakeDisplay WithMonitor(Monitor monitor)
    {
        _monitors.Add(monitor);
        return this;
    }

    public FakeDisplay WithActive(uint targetId, Placement placement)
    {
        Active[targetId] = placement;
        return this;
    }

    public FakeDisplay WithSavedLayout(Dictionary<uint, Placement> layout)
    {
        _savedLayouts[LayoutKey(layout.Keys)] = layout;
        return this;
    }

    public (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) QueryConfig(QueryDisplayConfigFlags flags)
    {
        if (flags == QueryDisplayConfigFlags.QDC_DATABASE_CURRENT)
            throw new Win32Exception(ERROR_INVALID_PARAMETER);

        var paths = new List<DISPLAYCONFIG_PATH_INFO>();
        var modes = new List<DISPLAYCONFIG_MODE_INFO>();
        var onlyActive = flags == QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS;

        foreach (var monitor in _monitors)
        {
            for (uint source = 0; source < SourceCount; source++)
            {
                var isActive = Active.TryGetValue(monitor.TargetId, out var placement) && placement.SourceId == source;
                if (onlyActive && !isActive)
                    continue;

                var path = new DISPLAYCONFIG_PATH_INFO
                {
                    sourceInfo = new() { adapterId = Adapter, id = source, modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID },
                    targetInfo = new()
                    {
                        adapterId = Adapter, id = monitor.TargetId, modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID,
                        targetAvailable = 1,
                        refreshRate = new() { Numerator = monitor.Hz * 1000, Denominator = 1000 },
                    },
                    flags = isActive ? DISPLAYCONFIG_PATH_FLAGS.ACTIVE : DISPLAYCONFIG_PATH_FLAGS.NONE,
                };

                if (isActive)
                {
                    path.sourceInfo.modeInfoIdx = (uint)modes.Count;
                    modes.Add(SourceMode(source, placement!));
                    path.targetInfo.modeInfoIdx = (uint)modes.Count;
                    modes.Add(TargetMode(monitor.TargetId, placement!));
                }

                paths.Add(path);
            }
        }

        return ([.. paths], [.. modes]);
    }

    public void ApplyConfig(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, SetDisplayConfigFlags flags)
    {
        Applies.Add(new Apply(paths, modes, flags));

        if (ApplyErrors.TryDequeue(out var error))
            throw new Win32Exception(error);

        var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_FLAGS.ACTIVE) != 0).ToArray();
        if (activePaths.GroupBy(p => p.targetInfo.id).Any(g => g.Count() > 1))
            throw new Win32Exception(ERROR_GEN_FAILURE);

        var next = flags.HasFlag(SetDisplayConfigFlags.SDC_TOPOLOGY_SUPPLIED)
            ? RestoreSavedLayout(activePaths, modes)
            : UseSuppliedConfig(activePaths, modes, flags);

        if (flags.HasFlag(SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE))
            _savedLayouts[LayoutKey(next.Keys)] = next;

        if (!IgnoresApplies)
            Active = next;
    }

    public (string FriendlyName, ushort ManufacturerId, ushort ProductCodeId) GetTargetDeviceInfo(LUID adapterId, uint targetId)
    {
        var monitor = _monitors.Single(m => m.TargetId == targetId);
        return (monitor.Name, monitor.EdidManufacturer, monitor.EdidProduct);
    }

    public string GetSourceGdiName(LUID adapterId, uint sourceId) => $@"\\.\DISPLAY{sourceId + 1}";

    private Dictionary<uint, Placement> RestoreSavedLayout(DISPLAYCONFIG_PATH_INFO[] activePaths, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        var modeIndicesSupplied = activePaths.Any(p =>
            p.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID
            || p.targetInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID);
        if (modes.Length > 0 || modeIndicesSupplied)
            throw new Win32Exception(ERROR_INVALID_PARAMETER);

        if (!_savedLayouts.TryGetValue(LayoutKey(activePaths.Select(p => p.targetInfo.id)), out var saved))
            throw new Win32Exception(ErrorNoSavedLayout);

        return activePaths.ToDictionary(
            p => p.targetInfo.id,
            p => saved[p.targetInfo.id] with { SourceId = p.sourceInfo.id });
    }

    private Dictionary<uint, Placement> UseSuppliedConfig(
        DISPLAYCONFIG_PATH_INFO[] activePaths, DISPLAYCONFIG_MODE_INFO[] modes, SetDisplayConfigFlags flags)
    {
        var next = new Dictionary<uint, Placement>();
        var rightEdge = 0;

        foreach (var path in activePaths.Where(p => p.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID))
        {
            var source = modes[path.sourceInfo.modeInfoIdx].info.sourceMode;
            var hz = (uint)modes[path.targetInfo.modeInfoIdx].info.targetMode.targetVideoSignalInfo.vSyncFreq.ToHz();
            next[path.targetInfo.id] = new Placement(path.sourceInfo.id, source.width, source.height, hz, source.position.x, source.position.y);
            rightEdge = Math.Max(rightEdge, source.position.x + (int)source.width);
        }

        foreach (var path in activePaths.Where(p => p.sourceInfo.modeInfoIdx == DISPLAYCONFIG_PATH_MODE_IDX_INVALID))
        {
            if (!flags.HasFlag(SetDisplayConfigFlags.SDC_ALLOW_CHANGES))
                throw new Win32Exception(ERROR_INVALID_PARAMETER);

            var monitor = _monitors.Single(m => m.TargetId == path.targetInfo.id);
            next[path.targetInfo.id] = new Placement(path.sourceInfo.id, monitor.Width, monitor.Height, monitor.Hz, rightEdge, 0);
            rightEdge += (int)monitor.Width;
        }

        return next;
    }

    private static string LayoutKey(IEnumerable<uint> targetIds) => string.Join(",", targetIds.Order());

    private static DISPLAYCONFIG_MODE_INFO SourceMode(uint sourceId, Placement placement) => new()
    {
        infoType = DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE,
        id = sourceId,
        adapterId = Adapter,
        info = new()
        {
            sourceMode = new()
            {
                width = placement.Width,
                height = placement.Height,
                position = new() { x = placement.X, y = placement.Y },
            },
        },
    };

    private static DISPLAYCONFIG_MODE_INFO TargetMode(uint targetId, Placement placement) => new()
    {
        infoType = DISPLAYCONFIG_MODE_INFO_TYPE.TARGET,
        id = targetId,
        adapterId = Adapter,
        info = new()
        {
            targetMode = new()
            {
                targetVideoSignalInfo = new()
                {
                    vSyncFreq = new() { Numerator = placement.Hz * 1000, Denominator = 1000 },
                },
            },
        },
    };
}
