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
        response.Error.ShouldContain("Unknown request type");
    }

    [Fact]
    public async Task HandleAsync_RunCli_MissingExePath_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "runCli" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error.ShouldContain("ExePath is required");
    }

    [Fact]
    public async Task HandleAsync_RunCli_NonexistentExe_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "runCli", ExePath = "/nonexistent/tool.exe" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task HandleAsync_LaunchProcess_MissingExePath_ReturnsFailure()
    {
        var request = new IpcRequest { Type = "launchProcess" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Success.ShouldBeFalse();
        response.Error.ShouldContain("ExePath is required");
    }
}
