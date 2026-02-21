namespace HaPcRemote.Service.Services;

public interface ICliRunner
{
    Task<string> RunAsync(string exePath, IEnumerable<string> arguments, int timeoutMs = 10000);
}
