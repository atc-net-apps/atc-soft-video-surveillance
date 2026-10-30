namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Settings operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<AppSettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await getSettingsEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    public async Task<GatewayResult<AppSettings>> UpdateSettingsAsync(
        UpdateSettingsRequest settings,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateSettingsParameters(Request: settings);
        var result = await updateSettingsEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<AppSettings>(result);
    }
}