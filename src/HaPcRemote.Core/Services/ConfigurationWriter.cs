using System.Text.Json;
using System.Text.Json.Nodes;
using HaPcRemote.Service.Configuration;

namespace HaPcRemote.Service.Services;

public sealed class ConfigurationWriter(string configPath) : IConfigurationWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null // PascalCase to match appsettings convention
    };

    private readonly Lock _lock = new();

    public PcRemoteOptions Read()
    {
        lock (_lock)
        {
            var root = ReadJsonRoot();
            var section = root[PcRemoteOptions.SectionName];
            if (section is null)
                return new PcRemoteOptions();

            return section.Deserialize<PcRemoteOptions>(WriteOptions) ?? new PcRemoteOptions();
        }
    }

    public void Write(PcRemoteOptions options)
    {
        lock (_lock)
        {
            var root = ReadJsonRoot();
            root[PcRemoteOptions.SectionName] = JsonSerializer.SerializeToNode(options, WriteOptions);
            WriteJsonRoot(root);
        }
    }

    public void SaveMode(string name, ModeConfig mode)
    {
        lock (_lock)
        {
            var options = ReadInternal();
            options.Modes[name] = mode;
            WriteInternal(options);
        }
    }

    public void DeleteMode(string name)
    {
        lock (_lock)
        {
            var options = ReadInternal();
            options.Modes.Remove(name);
            WriteInternal(options);
        }
    }

    public void SavePowerSettings(PowerSettings settings)
    {
        lock (_lock)
        {
            var options = ReadInternal();
            options.Power = settings;
            WriteInternal(options);
        }
    }

    public void SavePort(int port)
    {
        lock (_lock)
        {
            var options = ReadInternal();
            options.Port = port;
            WriteInternal(options);
        }
    }

    private PcRemoteOptions ReadInternal()
    {
        var root = ReadJsonRoot();
        var section = root[PcRemoteOptions.SectionName];
        return section?.Deserialize<PcRemoteOptions>(WriteOptions) ?? new PcRemoteOptions();
    }

    private void WriteInternal(PcRemoteOptions options)
    {
        var root = ReadJsonRoot();
        root[PcRemoteOptions.SectionName] = JsonSerializer.SerializeToNode(options, WriteOptions);
        WriteJsonRoot(root);
    }

    private JsonObject ReadJsonRoot()
    {
        if (!File.Exists(configPath))
            return new JsonObject();

        var json = File.ReadAllText(configPath);
        return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
    }

    private void WriteJsonRoot(JsonObject root)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(configPath, root.ToJsonString(WriteOptions));
    }
}
