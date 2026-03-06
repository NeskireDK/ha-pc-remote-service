using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace HaPcRemote.Service.Services;

[JsonSerializable(typeof(ConcurrentDictionary<string, EmulatorLaunchRecord>))]
[JsonSerializable(typeof(Dictionary<string, EmulatorLaunchRecord>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class EmulatorTrackerJsonContext : JsonSerializerContext;
