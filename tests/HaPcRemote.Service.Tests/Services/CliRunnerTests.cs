using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class CliRunnerTests
{
    [Fact]
    public async Task RunAsync_MissingExe_ThrowsFileNotFoundException()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-tool.exe");
        var runner = new CliRunner();

        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => runner.RunAsync(fakePath, []));

        ex.FileName.ShouldBe(fakePath);
    }

    [Fact]
    public async Task RunAsync_MissingExe_IncludesPathInMessage()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), "does-not-exist.exe");
        var runner = new CliRunner();

        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => runner.RunAsync(fakePath, ["--list"]));

        ex.Message.ShouldContain(fakePath);
    }
}
