namespace HaPcRemote.Service.Services;

public interface IRestartService
{
    /// <summary>
    /// Triggers a graceful restart of the service.
    /// Returns immediately; the actual restart happens after a brief delay.
    /// </summary>
    void ScheduleRestart();
}
