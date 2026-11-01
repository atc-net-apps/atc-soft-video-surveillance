namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Settings;

/// <summary>
/// Saves several changes to the settings as one: the settings file is written once, when the
/// batch ends, instead of once per section.
/// </summary>
public interface ISettingsBatch
{
    /// <summary>
    /// Starts a batch; the settings saved until it is disposed are written then, once.
    /// </summary>
    /// <returns>The batch; disposing it writes the settings.</returns>
    IDisposable BeginBatch();
}