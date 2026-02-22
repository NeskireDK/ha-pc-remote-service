using FakeItEasy;
using HaPcRemote.Shared.Ipc;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Ipc;

public class IpcRequestHandlerTests
{
    private readonly IpcRequestHandler _handler;

    public IpcRequestHandlerTests()
    {
        _handler = new IpcRequestHandler(A.Fake<ILogger>());
    }

    [Fact]
    public async Task HandleAsync_Ping_ReturnsSuccess()
    {
        var request = new IpcRequest { Type = "ping" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UnknownType_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "unknown_type" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("Unknown request type");
    }

    [Fact]
    public async Task HandleAsync_RunCli_MissingExePath_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "runCli" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("ExePath is required");
    }

    [Fact]
    public async Task HandleAsync_RunCli_NonexistentExe_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "runCli", ExePath = "/nonexistent/tool.exe" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("not found");
    }

    [Fact]
    public async Task HandleAsync_LaunchProcess_MissingExePath_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "launchProcess" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("ExePath is required");
    }

    [Fact]
    public async Task HandleAsync_SteamGetPath_ReturnsSuccessOnWindows()
    {
        var request = new IpcRequest { Type = "steamGetPath" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        if (OperatingSystem.IsWindows())
            // Registry may not have Steam installed in CI — Success with null Stdout is valid
            response.Success.ShouldBeTrue();
        else
            response.Error!.ShouldContain("Windows");
    }

    [Fact]
    public async Task HandleAsync_SteamGetRunningId_ReturnsNumericStringOnWindows()
    {
        var request = new IpcRequest { Type = "steamGetRunningId" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        if (OperatingSystem.IsWindows())
        {
            response.Success.ShouldBeTrue();
            int.TryParse(response.Stdout, out _).ShouldBeTrue();
        }
        else
        {
            response.Error!.ShouldContain("Windows");
        }
    }

    [Fact]
    public async Task HandleAsync_SteamLaunchUrl_MissingUrl_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "steamLaunchUrl" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("ProcessArguments");
    }

    [Fact]
    public async Task HandleAsync_SteamKillDir_MissingDirectory_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "steamKillDir" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error!.ShouldContain("ProcessArguments");
    }

    [Fact]
    public async Task HandleAsync_SteamKillDir_NonexistentDir_ReturnsSuccess()
    {
        var request = new IpcRequest { Type = "steamKillDir", ProcessArguments = @"C:\DoesNotExist\SomeGame\" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        // No matching processes — should still succeed
        response.Success.ShouldBeTrue();
    }
}
