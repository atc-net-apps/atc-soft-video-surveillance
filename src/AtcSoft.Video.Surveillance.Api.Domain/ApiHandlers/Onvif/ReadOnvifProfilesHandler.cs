namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Onvif;

/// <summary>
/// Handler business logic for the ReadOnvifProfiles operation: a camera's streams, read over
/// ONVIF, with the main stream and the sub-stream proposed. The camera's user and password are
/// used for this call only and are never logged; the addresses come back as a port and a path.
/// </summary>
public sealed class ReadOnvifProfilesHandler(IOnvifMediaClient onvif) : IReadOnvifProfilesHandler
{
    public async Task<ReadOnvifProfilesResult> ExecuteAsync(
        ReadOnvifProfilesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var address = request.Address.Trim();
        if (Uri.CheckHostName(address.Trim('[', ']')) == UriHostNameType.Unknown)
        {
            return ReadOnvifProfilesResult.BadRequest($"'{request.Address}' is no address of a camera.");
        }

        var read = await onvif
            .ReadProfilesAsync(address, request.Port, request.UserName, request.Password, cancellationToken)
            .ConfigureAwait(false);

        var main = read.Choice?.Main.StreamUri;
        var sub = read.Choice?.Sub?.StreamUri;
        return ReadOnvifProfilesResult.Ok(new OnvifProfilesResult(
            Outcome: ToApiOutcome(read.Outcome),
            Profiles: [.. read.Profiles.Select(ToApiProfile)])
        {
            Port = main is null ? null : PortOf(main),
            Path = main?.PathAndQuery,
            SubStreamPath = sub?.PathAndQuery,
            SubStreamPort = sub is null || main is null || PortOf(sub) == PortOf(main) ? null : PortOf(sub),
        });
    }

    private static OnvifProfile ToApiProfile(OnvifStreamProfile profile)
        => new(
            Token: profile.Token,
            Name: profile.Name,
            Encoding: profile.Encoding,
            Width: profile.Width,
            Height: profile.Height)
        {
            Port = profile.StreamUri is { } uri ? PortOf(uri) : null,
            Path = profile.StreamUri?.PathAndQuery,
        };

    private static int PortOf(Uri uri)
        => uri.IsDefaultPort || uri.Port <= 0 ? 554 : uri.Port;

    private static OnvifProfilesResultOutcome ToApiOutcome(
        OnvifImportOutcome outcome)
        => outcome switch
        {
            OnvifImportOutcome.Read => OnvifProfilesResultOutcome.Read,
            OnvifImportOutcome.Unauthorized => OnvifProfilesResultOutcome.Unauthorized,
            OnvifImportOutcome.Timeout => OnvifProfilesResultOutcome.Timeout,
            OnvifImportOutcome.NoRtspProfile => OnvifProfilesResultOutcome.NoRtspProfile,
            _ => OnvifProfilesResultOutcome.NotOnvif,
        };
}