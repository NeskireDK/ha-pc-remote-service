using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HaPcRemote.Service.Services;

public static class ApiKeyService
{
    private const int KeyLength = 32;
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string GenerateApiKey()
    {
        return string.Create(KeyLength, (object?)null, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = AllowedChars[RandomNumberGenerator.GetInt32(AllowedChars.Length)];
        });
    }

    public static void WriteApiKeyToConfig(string configPath, string apiKey)
    {
        var json = File.Exists(configPath)
            ? File.ReadAllText(configPath)
            : "{}";

        var root = JsonNode.Parse(json) ?? new JsonObject();

        var pcRemote = root["PcRemote"]?.AsObject() ?? new JsonObject();
        root["PcRemote"] = pcRemote;

        var auth = pcRemote["Auth"]?.AsObject() ?? new JsonObject();
        pcRemote["Auth"] = auth;

        auth["ApiKey"] = apiKey;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
        }
        File.WriteAllBytes(configPath, stream.ToArray());
    }
}
