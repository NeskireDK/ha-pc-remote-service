namespace HaPcRemote.Service.Services;

public interface IModeService
{
    IReadOnlyList<string> GetModeNames();
    Task ApplyModeAsync(string modeName);
}
