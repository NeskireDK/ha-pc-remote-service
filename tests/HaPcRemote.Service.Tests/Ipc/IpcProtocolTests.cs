using System.IO.Pipes;
using HaPcRemote.Shared.Ipc;
using Shouldly;

namespace HaPcRemote.Service.Tests.Ipc;

public class IpcProtocolTests
{
    [Fact]
    public async Task WriteAndRead_Request_RoundTrip()
    {
        var original = new IpcRequest
        {
            Type = "runCli",
            ExePath = "/usr/bin/test",
            Arguments = ["--list", "--format=csv"],
            TimeoutMs = 5000
        };

        var pipeName = $"test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var connectTask = client.ConnectAsync(5000);
        await server.WaitForConnectionAsync();
        await connectTask;

        await IpcProtocol.WriteMessageAsync(server, original, IpcJsonContext.Default.IpcRequest);
        var result = await IpcProtocol.ReadMessageAsync(client, IpcJsonContext.Default.IpcRequest);

        result.ShouldNotBeNull();
        result.Type.ShouldBe("runCli");
        result.ExePath.ShouldBe("/usr/bin/test");
        result.Arguments.ShouldBe(["--list", "--format=csv"]);
        result.TimeoutMs.ShouldBe(5000);
    }

    [Fact]
    public async Task WriteAndRead_Response_RoundTrip()
    {
        var original = IpcResponse.Ok(stdout: "line1\nline2", stderr: "warn", exitCode: 42);

        var pipeName = $"test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var connectTask = client.ConnectAsync(5000);
        await server.WaitForConnectionAsync();
        await connectTask;

        await IpcProtocol.WriteMessageAsync(server, original, IpcJsonContext.Default.IpcResponse);
        var result = await IpcProtocol.ReadMessageAsync(client, IpcJsonContext.Default.IpcResponse);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Stdout.ShouldBe("line1\nline2");
        result.Stderr.ShouldBe("warn");
        result.ExitCode.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task ReadMessage_EmptyPipe_ReturnsNull()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var connectTask = client.ConnectAsync(5000);
        await server.WaitForConnectionAsync();
        await connectTask;

        // Close the write end so the read side sees EOF
        server.Close();

        var result = await IpcProtocol.ReadMessageAsync(client, IpcJsonContext.Default.IpcRequest);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadMessage_OversizedLength_ThrowsInvalidOperation()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var connectTask = client.ConnectAsync(5000);
        await server.WaitForConnectionAsync();
        await connectTask;

        // Write a 4-byte length header claiming 2 MB (exceeds the 1 MB limit)
        var oversizedLength = BitConverter.GetBytes(2 * 1024 * 1024);
        await server.WriteAsync(oversizedLength);
        await server.FlushAsync();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => IpcProtocol.ReadMessageAsync(client, IpcJsonContext.Default.IpcRequest));

        ex.Message.ShouldContain("Invalid IPC message length");
    }
}
