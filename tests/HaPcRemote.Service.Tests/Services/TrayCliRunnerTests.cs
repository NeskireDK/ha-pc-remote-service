using FakeItEasy;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class TrayCliRunnerTests
{
    [Fact]
    public void Constructor_CreatesFallbackCliRunner()
    {
        var logger = A.Fake<ILogger<TrayCliRunner>>();

        var runner = new TrayCliRunner(logger);

        runner.ShouldNotBeNull();
        runner.ShouldBeAssignableTo<ICliRunner>();
    }
}
