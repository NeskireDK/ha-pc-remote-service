using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class DirectAppLauncherTests
{
    [Theory]
    [InlineData("steam://open/bigpicture", true)]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData(@"steam:\open\bigpicture", true)]   // mangled URI from stale config
    [InlineData("steam:open/bigpicture", true)]      // scheme:path without slashes
    [InlineData(@"C:\Program Files\steam.exe", false)] // drive letter — single char before colon
    [InlineData(@"D:\Games\app.exe", false)]
    [InlineData("notepad.exe", false)]
    [InlineData("notepad", false)]
    [InlineData("", false)]
    public void IsProtocolUri_DetectsCorrectly(string path, bool expected)
    {
        DirectAppLauncher.IsProtocolUri(path).ShouldBe(expected);
    }
}
