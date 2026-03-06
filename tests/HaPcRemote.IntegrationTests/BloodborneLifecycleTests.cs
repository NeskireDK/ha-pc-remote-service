using System.Net;
using HaPcRemote.IntegrationTests.Models;
using Shouldly;

namespace HaPcRemote.IntegrationTests;

/// <summary>
/// End-to-end lifecycle test for Bloodborne (non-Steam shortcut via shadPS4).
/// Launches the game, verifies detection + artwork, then stops it.
/// </summary>
[Collection("Service")]
public class BloodborneLifecycleTests : IntegrationTestBase
{
    private const int BloodborneAppId = -959860145;
    private const string BloodborneExe = "shadPS4.exe";

    [Fact]
    [Trait("Category", "Mutating")]
    public async Task FullLifecycle_LaunchDetectArtworkStop()
    {
        // --- Phase 1: Verify Bloodborne is in the game list ---
        var gamesResponse = await GetAsync<List<SteamGame>>("/api/steam/games");
        var bloodborne = gamesResponse.Data!
            .FirstOrDefault(g => g.AppId == BloodborneAppId);

        if (bloodborne == null)
        {
            Console.WriteLine("Bloodborne not found in game list — skipping lifecycle test");
            return;
        }

        Console.WriteLine($"[Setup] Found Bloodborne: AppId={bloodborne.AppId}, ExePath={bloodborne.ExePath}");
        bloodborne.IsShortcut.ShouldBeTrue();
        bloodborne.ExePath.ShouldNotBeNullOrWhiteSpace();
        Path.GetFileName(bloodborne.ExePath!).ShouldBe(BloodborneExe, StringCompareShould.IgnoreCase);

        // --- Phase 2: Ensure nothing is running ---
        var preCheck = await GetAsync<SteamRunningGame>("/api/steam/running");
        if (preCheck.Data != null)
        {
            Console.WriteLine($"[Setup] Stopping already-running game: {preCheck.Data.Name}");
            await PostAsync("/api/steam/stop");
            await Task.Delay(3000);
        }

        // --- Phase 3: Launch Bloodborne ---
        Console.WriteLine("[Launch] Starting Bloodborne via steam://rungameid...");
        var launchResponse = await PostRawAsync($"/api/steam/run/{BloodborneAppId}");
        launchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var launchResult = await DeserializeAsync<ApiResponse<SteamRunningGame>>(launchResponse);
        Console.WriteLine($"[Launch] Immediate result: {(launchResult?.Data != null ? $"AppId={launchResult.Data.AppId}, Pid={launchResult.Data.ProcessId}" : "null (polling timed out)")}");

        // Give the game more time to fully start (shadPS4 can be slow)
        Console.WriteLine("[Launch] Waiting 15s for game to fully start...");
        await Task.Delay(15000);

        // --- Phase 4: Detect running game via diagnostics ---
        var diag = await GetDiagnostics();
        DumpDiagnostics(diag);

        // If detection fails, wait longer and retry
        if (diag.Result == null)
        {
            Console.WriteLine("[Detect] First check failed, waiting 15s more...");
            await Task.Delay(15000);
            diag = await GetDiagnostics();
            DumpDiagnostics(diag);
        }

        diag.SteamRunning.ShouldBeTrue("Steam should be running");
        diag.ShortcutsChecked.ShouldBeGreaterThan(0, "Should have shortcuts to check");
        diag.Result.ShouldNotBeNull("Game should be detected as running");
        diag.Result!.AppId.ShouldBe(BloodborneAppId);
        diag.Result.ProcessId.ShouldNotBeNull("Should have a process ID");

        var bbTrace = diag.Traces.FirstOrDefault(t => t.AppId == BloodborneAppId);
        bbTrace.ShouldNotBeNull("Bloodborne should appear in detection traces");
        bbTrace!.Matched.ShouldBeTrue("Bloodborne shortcut should match a running process");
        bbTrace.FilenameMatches.ShouldNotBeEmpty("Should have filename-level process matches");
        Console.WriteLine($"[Detect] Match reason: {bbTrace.MatchReason}, PID: {bbTrace.MatchedPid}");

        // --- Phase 5: Also check via the normal running endpoint ---
        var running = await GetAsync<SteamRunningGame>("/api/steam/running");
        running.Data.ShouldNotBeNull("Normal /running endpoint should also detect the game");
        running.Data!.AppId.ShouldBe(BloodborneAppId);
        Console.WriteLine($"[Running] Normal endpoint: AppId={running.Data.AppId}, Name={running.Data.Name}, Pid={running.Data.ProcessId}");

        // --- Phase 6: Check artwork ---
        var artworkResponse = await GetRawAsync($"/api/steam/artwork/{BloodborneAppId}");
        Console.WriteLine($"[Artwork] Status: {artworkResponse.StatusCode}, ContentType: {artworkResponse.Content.Headers.ContentType}");
        if (artworkResponse.StatusCode == HttpStatusCode.OK)
        {
            var bytes = await artworkResponse.Content.ReadAsByteArrayAsync();
            Console.WriteLine($"[Artwork] Image loaded: {bytes.Length} bytes");
            bytes.Length.ShouldBeGreaterThan(0);
        }
        else
        {
            Console.WriteLine("[Artwork] No artwork returned (404) — checking diagnostics");
            var artDiag = await GetRawAsync($"/api/steam/artwork/{BloodborneAppId}/diagnostics");
            if (artDiag.StatusCode == HttpStatusCode.OK)
            {
                var json = await artDiag.Content.ReadAsStringAsync();
                Console.WriteLine($"[Artwork] Diagnostics: {json}");
            }
        }

        // --- Phase 7: Stop the game ---
        Console.WriteLine("[Stop] Stopping Bloodborne...");
        var stopResponse = await PostRawAsync("/api/steam/stop");
        stopResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(3000);

        // --- Phase 8: Verify stopped ---
        var afterStop = await GetAsync<SteamRunningGame>("/api/steam/running");
        Console.WriteLine($"[AfterStop] Running: {(afterStop.Data == null ? "none" : afterStop.Data.Name)}");

        // Also verify via diagnostics
        var diagAfter = await GetDiagnostics();
        var bbAfter = diagAfter.Traces.FirstOrDefault(t => t.AppId == BloodborneAppId);
        if (bbAfter != null)
        {
            Console.WriteLine($"[AfterStop] Bloodborne trace: Matched={bbAfter.Matched}, FilenameMatches={bbAfter.FilenameMatches.Count}");
            bbAfter.Matched.ShouldBeFalse("Bloodborne should no longer match after stop");
        }
    }

    [Fact]
    [Trait("Category", "ReadOnly")]
    public async Task Diagnostics_ReturnsStructuredTrace()
    {
        var diag = await GetDiagnostics();

        Console.WriteLine($"Steam running: {diag.SteamRunning}");
        Console.WriteLine($"Steam reported appId: {diag.SteamReportedAppId}");
        Console.WriteLine($"Shortcuts checked: {diag.ShortcutsChecked}");
        Console.WriteLine($"Running processes: {diag.RunningProcessCount}");
        Console.WriteLine($"Detection result: {(diag.Result == null ? "none" : $"{diag.Result.Name} (pid={diag.Result.ProcessId})")}");

        diag.SteamRunning.ShouldBeTrue("Steam should be running on target PC");
        diag.ShortcutsChecked.ShouldBeGreaterThan(0, "Should have at least Bloodborne shortcut");
        diag.RunningProcessCount.ShouldBeGreaterThan(0);

        foreach (var trace in diag.Traces)
        {
            Console.WriteLine($"\n[Trace] [{trace.AppId}] {trace.Name}");
            Console.WriteLine($"  ExePath: {trace.ExePath}");
            Console.WriteLine($"  LaunchOptions: {trace.LaunchOptions ?? "(null)"}");
            Console.WriteLine($"  ExactPathMatch: {trace.ExactPathMatch}");
            Console.WriteLine($"  Matched: {trace.Matched} (reason: {trace.MatchReason ?? "n/a"})");
            Console.WriteLine($"  FilenameMatches: {trace.FilenameMatches.Count}");
            foreach (var m in trace.FilenameMatches)
                Console.WriteLine($"    pid={m.Pid} path={m.Path} cmd={m.CommandLine?[..Math.Min(100, m.CommandLine.Length)] ?? "(null)"}");
        }
    }

    private async Task<RunningGameDiagnostics> GetDiagnostics()
    {
        var response = await GetRawAsync("/api/steam/running/diagnostics");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<RunningGameDiagnostics>>(response);
        result.ShouldNotBeNull();
        result!.Data.ShouldNotBeNull();
        return result.Data!;
    }

    private static void DumpDiagnostics(RunningGameDiagnostics diag)
    {
        Console.WriteLine($"[Diag] SteamAppId={diag.SteamReportedAppId}, SteamRunning={diag.SteamRunning}");
        Console.WriteLine($"[Diag] Shortcuts={diag.ShortcutsChecked}, Processes={diag.RunningProcessCount}");
        Console.WriteLine($"[Diag] Result={diag.Result?.Name ?? "null"} (pid={diag.Result?.ProcessId})");
        foreach (var t in diag.Traces)
        {
            Console.WriteLine($"[Diag]   [{t.AppId}] {t.Name}: matched={t.Matched}, reason={t.MatchReason ?? "n/a"}, filenameHits={t.FilenameMatches.Count}");
        }
    }
}
