using HaWindowsRemote.Service.Services;
using Shouldly;

namespace HaWindowsRemote.Service.Tests.Services;

public class CliRunnerTests
{
    [Fact]
    public async Task RunAsync_MissingExe_ThrowsFileNotFoundException()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-tool.exe");

        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => CliRunner.RunAsync(fakePath, ""));

        ex.FileName.ShouldBe(fakePath);
    }

    [Fact]
    public async Task RunAsync_MissingExe_IncludesPathInMessage()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), "does-not-exist.exe");

        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => CliRunner.RunAsync(fakePath, "--list"));

        ex.Message.ShouldContain(fakePath);
    }
}
