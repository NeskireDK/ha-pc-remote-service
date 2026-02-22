using FakeItEasy;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class IpcSteamPlatformTests
{
    [Fact]
    public void Constructor_CreatesPlatform()
    {
        var logger = A.Fake<ILogger<IpcSteamPlatform>>();

        var platform = new IpcSteamPlatform(logger);

        platform.ShouldNotBeNull();
        platform.ShouldBeAssignableTo<ISteamPlatform>();
    }
}
