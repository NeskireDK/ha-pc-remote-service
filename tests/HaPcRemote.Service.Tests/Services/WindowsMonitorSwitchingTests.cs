using System.ComponentModel;
using FakeItEasy;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;
using static HaPcRemote.Service.Native.DisplayConfigApi;
using Placement = HaPcRemote.Service.Tests.Services.FakeDisplay.Placement;

namespace HaPcRemote.Service.Tests.Services;

public class WindowsMonitorSwitchingTests
{
    private const ushort GsmWire = 0x6D1E;
    private const ushort PhlWire = 0x0C41;

    private const uint UltrawideTarget = 33025;
    private const uint TvTarget = 50364672;
    private const uint PhilipsTarget = 4354;

    private static readonly FakeDisplay.Monitor Ultrawide = new(UltrawideTarget, "LG ULTRAGEAR+", GsmWire, 0x9E98, 3440, 1440, 240);
    private static readonly FakeDisplay.Monitor Tv = new(TvTarget, "LG TV SSCR2", GsmWire, 0xC0C8, 3840, 2160, 60);
    private static readonly FakeDisplay.Monitor Philips = new(PhilipsTarget, "PHL BDM3270", PhlWire, 0x08E7, 2560, 1440, 60);

    private static readonly Placement UltrawideAlone = new(0, 3440, 1440, 240, 0, 0);

    private static FakeDisplay AtlasWithUltrawideOnly() => new FakeDisplay()
        .WithMonitor(Ultrawide)
        .WithMonitor(Tv)
        .WithMonitor(Philips)
        .WithActive(UltrawideTarget, UltrawideAlone);

    private static WindowsMonitorService CreateService(FakeDisplay display) =>
        new(display, A.Fake<ILogger<WindowsMonitorService>>()) { RetryDelaysMs = [0, 0, 0] };

    // ── Solo ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Solo_WithSavedLayout_RestoresSavedResolutionAndRefresh()
    {
        var display = AtlasWithUltrawideOnly()
            .WithSavedLayout(new() { [TvTarget] = new Placement(0, 3840, 2160, 120, 0, 0) });

        await CreateService(display).SoloMonitorAsync("GSMC0C8");

        display.Active.Keys.ShouldBe([TvTarget]);
        display.Active[TvTarget].ShouldBe(new Placement(display.Active[TvTarget].SourceId, 3840, 2160, 120, 0, 0));
        display.Applies.Count.ShouldBe(1);
        display.Applies[0].Flags.HasFlag(SetDisplayConfigFlags.SDC_TOPOLOGY_SUPPLIED).ShouldBeTrue();
    }

    [Fact]
    public async Task Solo_WithoutSavedLayout_FallsBackAndSavesTheResult()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).SoloMonitorAsync("GSMC0C8");

        display.Active.Keys.ShouldBe([TvTarget]);
        display.Applies.Count.ShouldBe(2);
        display.Applies[1].Flags.HasFlag(SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE).ShouldBeTrue();
    }

    [Fact]
    public async Task Solo_SuppliesExactlyOnePathForTheTarget()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).SoloMonitorAsync("GSMC0C8");

        foreach (var apply in display.Applies)
        {
            apply.Paths.Length.ShouldBe(1);
            apply.Paths[0].targetInfo.id.ShouldBe(TvTarget);
        }
    }

    [Fact]
    public async Task Solo_ToMonitorThatIsAlreadyActiveAsSecondary_MovesItToOrigin()
    {
        var display = AtlasWithUltrawideOnly()
            .WithActive(TvTarget, new Placement(1, 3840, 2160, 120, 3440, 0));

        await CreateService(display).SoloMonitorAsync("GSMC0C8");

        display.Active.Keys.ShouldBe([TvTarget]);
        display.Active[TvTarget].ShouldBe(new Placement(1, 3840, 2160, 120, 0, 0));
    }

    [Fact]
    public async Task Solo_AlreadyOnlyActiveMonitor_DoesNotApply()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).SoloMonitorAsync("GSM9E98");

        display.Applies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Solo_WhenWindowsDoesNotChange_Throws()
    {
        var display = AtlasWithUltrawideOnly();
        display.IgnoresApplies = true;

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => CreateService(display).SoloMonitorAsync("GSMC0C8"));

        ex.Message.ShouldContain("LG ULTRAGEAR+");
    }

    [Fact]
    public async Task Solo_TransientError31_RetriesAndSucceeds()
    {
        var display = AtlasWithUltrawideOnly();
        display.ApplyErrors.Enqueue(ERROR_GEN_FAILURE);
        display.ApplyErrors.Enqueue(ERROR_GEN_FAILURE);

        await CreateService(display).SoloMonitorAsync("GSMC0C8");

        display.Active.Keys.ShouldBe([TvTarget]);
    }

    [Fact]
    public async Task Solo_PersistentError31_Throws()
    {
        var display = AtlasWithUltrawideOnly();
        for (var i = 0; i < 20; i++)
            display.ApplyErrors.Enqueue(ERROR_GEN_FAILURE);

        var ex = await Should.ThrowAsync<Win32Exception>(() => CreateService(display).SoloMonitorAsync("GSMC0C8"));

        ex.NativeErrorCode.ShouldBe(ERROR_GEN_FAILURE);
    }

    [Fact]
    public async Task Solo_UnknownId_Throws()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => CreateService(AtlasWithUltrawideOnly()).SoloMonitorAsync("UNKNOWN"));
    }

    // ── Enable ────────────────────────────────────────────────────────

    [Fact]
    public async Task Enable_UsesAFreeSourceSoWindowsExtendsInsteadOfCloning()
    {
        var display = AtlasWithUltrawideOnly();
        var service = CreateService(display);

        await service.EnableMonitorAsync("GSMC0C8");

        display.Active.Keys.Order().ShouldBe([UltrawideTarget, TvTarget]);
        display.Active[TvTarget].SourceId.ShouldNotBe(display.Active[UltrawideTarget].SourceId);
        var monitors = await service.GetMonitorsAsync();
        monitors.Where(m => m.IsActive).Select(m => m.Name).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Enable_WithoutSavedLayout_KeepsTheExistingMonitorsMode()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).EnableMonitorAsync("GSMC0C8");

        display.Active[UltrawideTarget].ShouldBe(UltrawideAlone);
    }

    [Fact]
    public async Task Enable_AlreadyActive_DoesNotApply()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).EnableMonitorAsync("GSM9E98");

        display.Applies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Enable_NoFreeSource_Throws()
    {
        var display = AtlasWithUltrawideOnly();
        display.SourceCount = 1;

        await Should.ThrowAsync<InvalidOperationException>(() => CreateService(display).EnableMonitorAsync("GSMC0C8"));

        display.Applies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Enable_IdenticalEdid_EnablesTheSecondMonitor()
    {
        var display = new FakeDisplay()
            .WithMonitor(Ultrawide)
            .WithMonitor(Ultrawide with { TargetId = 777 })
            .WithActive(UltrawideTarget, UltrawideAlone);

        await CreateService(display).EnableMonitorAsync("GSM9E98#2");

        display.Active.Keys.Order().ShouldBe([777u, UltrawideTarget]);
    }

    // ── Disable ───────────────────────────────────────────────────────

    [Fact]
    public async Task Disable_Primary_LeavesTheOtherMonitorAsPrimary()
    {
        var display = AtlasWithUltrawideOnly()
            .WithActive(TvTarget, new Placement(1, 3840, 2160, 120, 3440, 0));
        var service = CreateService(display);

        await service.DisableMonitorAsync("GSM9E98");

        display.Active.Keys.ShouldBe([TvTarget]);
        (await service.GetMonitorsAsync()).Single(m => m.IsActive).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public async Task Disable_OnlyActiveMonitor_Throws()
    {
        var display = AtlasWithUltrawideOnly();

        await Should.ThrowAsync<InvalidOperationException>(() => CreateService(display).DisableMonitorAsync("GSM9E98"));

        display.Applies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Disable_AlreadyInactive_DoesNotApply()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).DisableMonitorAsync("GSMC0C8");

        display.Applies.ShouldBeEmpty();
    }

    // ── Primary ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetPrimary_MovesTargetToOriginAndShiftsTheOthers()
    {
        var display = AtlasWithUltrawideOnly()
            .WithActive(TvTarget, new Placement(1, 3840, 2160, 120, 3440, 0));

        await CreateService(display).SetPrimaryAsync("GSMC0C8");

        display.Active[TvTarget].X.ShouldBe(0);
        display.Active[UltrawideTarget].X.ShouldBe(-3440);
    }

    [Fact]
    public async Task SetPrimary_InactiveMonitor_Throws()
    {
        var display = AtlasWithUltrawideOnly();

        await Should.ThrowAsync<InvalidOperationException>(() => CreateService(display).SetPrimaryAsync("GSMC0C8"));

        display.Applies.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPrimary_AlreadyPrimary_DoesNotApply()
    {
        var display = AtlasWithUltrawideOnly();

        await CreateService(display).SetPrimaryAsync("GSM9E98");

        display.Applies.ShouldBeEmpty();
    }
}
