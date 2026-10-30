namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - storage status using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Gets how full the server's disk with the recordings is.
    /// </summary>
    /// <returns>The status; null when it cannot be read or the server did not answer.</returns>
    public async Task<StorageStatus?> GetStorageStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await getStorageStatusEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }
}