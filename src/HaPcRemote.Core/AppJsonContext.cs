using System.Text.Json.Serialization;
using HaPcRemote.Service.Models;

namespace HaPcRemote.Service;

[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(ApiResponse<HealthResponse>))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(AppInfo))]
[JsonSerializable(typeof(ApiResponse<AppInfo>))]
[JsonSerializable(typeof(List<AppInfo>))]
[JsonSerializable(typeof(ApiResponse<List<AppInfo>>))]
[JsonSerializable(typeof(AudioDevice))]
[JsonSerializable(typeof(List<AudioDevice>))]
[JsonSerializable(typeof(ApiResponse<AudioDevice>))]
[JsonSerializable(typeof(ApiResponse<List<AudioDevice>>))]
[JsonSerializable(typeof(MonitorProfile))]
[JsonSerializable(typeof(List<MonitorProfile>))]
[JsonSerializable(typeof(ApiResponse<List<MonitorProfile>>))]
[JsonSerializable(typeof(MonitorInfo))]
[JsonSerializable(typeof(List<MonitorInfo>))]
[JsonSerializable(typeof(ApiResponse<MonitorInfo>))]
[JsonSerializable(typeof(ApiResponse<List<MonitorInfo>>))]
[JsonSerializable(typeof(MacAddressInfo))]
[JsonSerializable(typeof(List<MacAddressInfo>))]
[JsonSerializable(typeof(SteamGame))]
[JsonSerializable(typeof(List<SteamGame>))]
[JsonSerializable(typeof(ApiResponse<List<SteamGame>>))]
[JsonSerializable(typeof(SteamRunningGame))]
[JsonSerializable(typeof(ApiResponse<SteamRunningGame>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext;
