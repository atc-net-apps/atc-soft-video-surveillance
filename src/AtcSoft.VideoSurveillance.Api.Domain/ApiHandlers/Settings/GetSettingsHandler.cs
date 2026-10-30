namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Settings;

/// <summary>
/// Handler business logic for the GetSettings operation.
/// </summary>
public sealed class GetSettingsHandler(
    IApplicationSettingsService settingsService) : IGetSettingsHandler
{
    public Task<GetSettingsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var apiSettings = SettingsMappingExtensions.ToApiModel(
            settingsService.General,
            settingsService.CameraDisplay,
            settingsService.Connection,
            settingsService.Performance,
            settingsService.MotionDetection,
            settingsService.Recording,
            settingsService.Advanced);

        return Task.FromResult(GetSettingsResult.Ok(apiSettings));
    }
}