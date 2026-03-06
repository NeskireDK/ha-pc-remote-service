using FakeItEasy;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class EmulatorTrackerTests : IDisposable
{
    private readonly ILogger<EmulatorTracker> _logger = A.Fake<ILogger<EmulatorTracker>>();
    private readonly string _tempDir;
    private readonly string _filePath;

    public EmulatorTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"emulator-tracker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "emulator-launches.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* best effort cleanup */ }
    }

    private EmulatorTracker CreateTracker() => new(_logger, _filePath);

    [Fact]
    public void TrackLaunch_StoresMapping()
    {
        var tracker = CreateTracker();

        tracker.TrackLaunch(@"D:\shadps4\shadPS4.exe", -959860145, "Bloodborne");

        var result = tracker.GetLastLaunched(@"D:\shadps4\shadPS4.exe");
        result.ShouldNotBeNull();
        result.Value.AppId.ShouldBe(-959860145);
        result.Value.Name.ShouldBe("Bloodborne");
    }

    [Fact]
    public void GetLastLaunched_ReturnsStoredMapping()
    {
        var tracker = CreateTracker();
        tracker.TrackLaunch(@"C:\emulators\rpcs3.exe", -123456, "Demon's Souls");

        var result = tracker.GetLastLaunched(@"C:\emulators\rpcs3.exe");

        result.ShouldNotBeNull();
        result.Value.AppId.ShouldBe(-123456);
        result.Value.Name.ShouldBe("Demon's Souls");
    }

    [Fact]
    public void GetLastLaunched_UnknownExe_ReturnsNull()
    {
        var tracker = CreateTracker();

        var result = tracker.GetLastLaunched(@"C:\unknown\emulator.exe");

        result.ShouldBeNull();
    }

    [Fact]
    public void TrackLaunch_OverwritesPreviousMapping()
    {
        var tracker = CreateTracker();
        tracker.TrackLaunch(@"D:\shadps4\shadPS4.exe", -100, "Game A");

        tracker.TrackLaunch(@"D:\shadps4\shadPS4.exe", -200, "Game B");

        var result = tracker.GetLastLaunched(@"D:\shadps4\shadPS4.exe");
        result.ShouldNotBeNull();
        result.Value.AppId.ShouldBe(-200);
        result.Value.Name.ShouldBe("Game B");
    }

    [Fact]
    public void Persistence_DataSurvivesReload()
    {
        var tracker1 = CreateTracker();
        tracker1.TrackLaunch(@"D:\shadps4\shadPS4.exe", -959860145, "Bloodborne");

        // Create a new tracker instance that reads from the same file
        var tracker2 = CreateTracker();

        var result = tracker2.GetLastLaunched(@"D:\shadps4\shadPS4.exe");
        result.ShouldNotBeNull();
        result.Value.AppId.ShouldBe(-959860145);
        result.Value.Name.ShouldBe("Bloodborne");
    }

    [Fact]
    public void Persistence_MultipleEntriesSurviveReload()
    {
        var tracker1 = CreateTracker();
        tracker1.TrackLaunch(@"D:\shadps4\shadPS4.exe", -100, "Bloodborne");
        tracker1.TrackLaunch(@"C:\rpcs3\rpcs3.exe", -200, "Demon's Souls");

        var tracker2 = CreateTracker();

        var r1 = tracker2.GetLastLaunched(@"D:\shadps4\shadPS4.exe");
        r1.ShouldNotBeNull();
        r1.Value.AppId.ShouldBe(-100);

        var r2 = tracker2.GetLastLaunched(@"C:\rpcs3\rpcs3.exe");
        r2.ShouldNotBeNull();
        r2.Value.AppId.ShouldBe(-200);
    }

    [Fact]
    public void CaseInsensitive_ExePathMatching()
    {
        var tracker = CreateTracker();
        tracker.TrackLaunch(@"D:\ShadPS4\SHADPS4.EXE", -959860145, "Bloodborne");

        // Query with different casing
        var result = tracker.GetLastLaunched(@"d:\shadps4\shadps4.exe");

        if (OperatingSystem.IsWindows())
        {
            result.ShouldNotBeNull();
            result.Value.AppId.ShouldBe(-959860145);
            result.Value.Name.ShouldBe("Bloodborne");
        }
        else
        {
            // On Linux, paths are case-sensitive
            result.ShouldBeNull();
        }
    }

    [Fact]
    public void GetLastLaunched_NoFile_ReturnsNull()
    {
        // Use a path that doesn't exist
        var tracker = new EmulatorTracker(_logger, Path.Combine(_tempDir, "nonexistent", "data.json"));

        var result = tracker.GetLastLaunched(@"D:\shadps4\shadPS4.exe");

        result.ShouldBeNull();
    }

    [Fact]
    public void TrackLaunch_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "emulator-launches.json");
        var tracker = new EmulatorTracker(_logger, nestedPath);

        tracker.TrackLaunch(@"D:\shadps4\shadPS4.exe", -100, "Bloodborne");

        File.Exists(nestedPath).ShouldBeTrue();
    }
}
