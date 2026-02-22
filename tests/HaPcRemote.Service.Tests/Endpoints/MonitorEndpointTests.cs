using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class MonitorEndpointTests : EndpointTestBase
{
    private const string SampleXml =
        """
        <?xml version="1.0" ?>
        <monitors_list>
        <item>
        <resolution>3840 X 2160</resolution>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>Yes</primary>
        <frequency>144</frequency>
        <name>\\.\DISPLAY1</name>
        <short_monitor_id>GSM59A4</short_monitor_id>
        <monitor_name>LG ULTRAGEAR</monitor_name>
        <monitor_serial_number>ABC123</monitor_serial_number>
        </item>
        <item>
        <resolution>2560 X 1440</resolution>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>No</primary>
        <frequency>60</frequency>
        <name>\\.\DISPLAY2</name>
        <short_monitor_id>DEL4321</short_monitor_id>
        <monitor_name>Dell U2723QE</monitor_name>
        <monitor_serial_number>XYZ789</monitor_serial_number>
        </item>
        </monitors_list>
        """;

    private void SetupCliRunnerWithXml()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Invokes((string _, IEnumerable<string> args, int _) =>
            {
                var argList = args.ToList();
                if (argList.Count >= 2 && argList[0] == "/sxml" && !string.IsNullOrEmpty(argList[1]))
                    File.WriteAllText(argList[1], SampleXml);
            })
            .Returns(string.Empty);
    }

    [Fact]
    public async Task GetMonitors_ReturnsMonitorList()
    {
        SetupCliRunnerWithXml();
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
        SetupCliRunnerWithXml();
        using var client = CreateClient();

        var response = await client.PostAsync("/api/monitor/enable/UNKNOWN", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EnableMonitor_ValidId_ReturnsOk()
    {
        SetupCliRunnerWithXml();
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
