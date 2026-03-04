namespace HaPcRemote.Service.Services;

/// <summary>
/// Fallback for platforms that don't support auto-update (e.g. Linux headless).
/// </summary>
public sealed class NoOpUpdateService : IUpdateService
{
    public Task<UpdateResult> CheckAndApplyAsync(CancellationToken ct = default)
        => Task.FromResult(UpdateResult.Failed("Auto-update not available on this platform"));
}
