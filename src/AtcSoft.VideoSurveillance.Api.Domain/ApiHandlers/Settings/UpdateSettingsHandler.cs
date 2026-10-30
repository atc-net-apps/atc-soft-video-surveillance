namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Settings;

/// <summary>
/// Handler business logic for the UpdateSettings operation.
/// </summary>
public sealed class UpdateSettingsHandler(
    IApplicationSettingsService settingsService,
    IRecordingService recordingService) : IUpdateSettingsHandler
{
    private static readonly string[] RecordingFileExtensions = [".mp4", ".mkv", ".avi"];

    public async Task<UpdateSettingsResult> ExecuteAsync(
        UpdateSettingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var layoutBefore = RecordingLayoutOptions.From(settingsService.Recording);

        var general = settingsService.General;
        var cameraDisplay = settingsService.CameraDisplay;
        var connection = settingsService.Connection;
        var performance = settingsService.Performance;
        var motionDetection = settingsService.MotionDetection;
        var recording = settingsService.Recording;
        var advanced = settingsService.Advanced;

        parameters.Request.ApplyToCore(
            general,
            cameraDisplay,
            connection,
            performance,
            motionDetection,
            recording,
            advanced);

        settingsService.SaveGeneral(general);
        settingsService.SaveCameraDisplay(cameraDisplay);
        settingsService.SaveConnection(connection);
        settingsService.SavePerformance(performance);
        settingsService.SaveMotionDetection(motionDetection);
        settingsService.SaveRecording(recording);
        settingsService.SaveAdvanced(advanced);

        // A changed folder layout moves the existing recordings, so the
        // folder matches the settings when this call returns.
        var layoutAfter = RecordingLayoutOptions.From(settingsService.Recording);
        if (layoutAfter != layoutBefore)
        {
            await SynchronizeRecordingLayoutAsync(layoutAfter, cancellationToken).ConfigureAwait(false);
        }

        var updated = SettingsMappingExtensions.ToApiModel(
            settingsService.General,
            settingsService.CameraDisplay,
            settingsService.Connection,
            settingsService.Performance,
            settingsService.MotionDetection,
            settingsService.Recording,
            settingsService.Advanced);

        return UpdateSettingsResult.Ok(updated);
    }

    private Task SynchronizeRecordingLayoutAsync(
        RecordingLayoutOptions layout,
        CancellationToken cancellationToken)
    {
        var recordingPath = settingsService.Recording.RecordingPath;
        if (string.IsNullOrEmpty(recordingPath))
        {
            return Task.CompletedTask;
        }

        var activeSessions = recordingService.GetActiveSessions();

        // File-system work: keep it off the request thread. The server
        // names its files in UTC.
        return Task.Run(
            () => RecordingLayoutReorganizer.Synchronize(
                recordingPath,
                layout,
                RecordingFileExtensions,
                activeSessions,
                recordingService.SegmentRecording,
                fileNamesAreUtc: true),
            cancellationToken);
    }
}