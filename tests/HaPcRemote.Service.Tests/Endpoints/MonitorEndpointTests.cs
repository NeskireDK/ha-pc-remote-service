using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class MonitorEndpointTests : EndpointTestBase
{
    private const string SampleCsv =
        @"\\.\DISPLAY1,GSM59A4,GSM59A4-1234,Active,\\.\DISPLAY1\GSM59A4,LG ULTRAGEAR,ABC123,NVIDIA,3840x2160,0,3840,2160,32,144,Yes" + "\n"
      + @"\\.\DISPLAY2,DEL4321,DEL4321-5678,Active,\\.\DISPLAY2\DEL4321,Dell U2723QE,XYZ789,NVIDIA,2560x1440,0,2560,1440,32,60,No";

    private void SetupCliRunnerWithCsv()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Invokes((string _, IEnumerable<string> args, int _) =>
            {
                var argList = args.ToList();
                if (argList.Count >= 2 && argList[0] == "/scomma" && !string.IsNullOrEmpty(argList[1]))
                    File.WriteAllText(argList[1], SampleCsv);
            })
            .Returns(string.Empty);
    }

    [Fact]
    public async Task GetMonitors_ReturnsMonitorList()
    {
        SetupCliRunnerWithCsv();
        using var client = CreateClient();

        var response = await client.GetAsync("/api/monitor/list");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<List<MonitorInfo>>>(
            AppJsonContext.Default.ApiResponseListMonitorInfo);
        json.ShouldNotBeNull();
        json.Data!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EnableMonitor_UnknownId_Returns404()
    {
        SetupCliRunnerWithCsv();
        using var client = CreateClient();

        var response = await client.PostAsync("/api/monitor/enable/UNKNOWN", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EnableMonitor_ValidId_ReturnsOk()
    {
        SetupCliRunnerWithCsv();
        using var client = CreateClient();

        var response = await client.PostAsync("/api/monitor/enable/DEL4321", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfiles_ReturnsEmptyList()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/monitor/profiles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<List<MonitorProfile>>>(
            AppJsonContext.Default.ApiResponseListMonitorProfile);
        json.ShouldNotBeNull();
        json.Data.ShouldNotBeNull();
    }
}
