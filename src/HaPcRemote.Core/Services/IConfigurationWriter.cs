using HaPcRemote.Service.Configuration;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Reads and writes PcRemote configuration sections in appsettings.json.
/// Changes are persisted to the writable config path and picked up by IOptionsMonitor.
/// </summary>
public interface IConfigurationWriter
{
    /// <summary>Read the current PcRemote options from the writable config file.</summary>
    PcRemoteOptions Read();

    /// <summary>Write the full PcRemote section to the writable config file.</summary>
    void Write(PcRemoteOptions options);

    /// <summary>Add or update a single mode by name.</summary>
    void SaveMode(string name, ModeConfig mode);

    /// <summary>Delete a mode by name.</summary>
    void DeleteMode(string name);

    /// <summary>Update power settings only.</summary>
    void SavePowerSettings(PowerSettings settings);
}
