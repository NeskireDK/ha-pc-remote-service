using System.Text.Json.Serialization;
using HaWindowsRemote.Service.Models;

namespace HaWindowsRemote.Service;

[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(ApiResponse<HealthResponse>))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext;
