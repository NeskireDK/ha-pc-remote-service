using FakeItEasy;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class TrayAppLauncherTests
{
    [Fact]
    public void Constructor_Succeeds()
    {
        var logger = A.Fake<ILogger<TrayAppLauncher>>();

        var launcher = new TrayAppLauncher(logger);

        launcher.ShouldNotBeNull();
        launcher.ShouldBeAssignableTo<IAppLauncher>();
    }
}
