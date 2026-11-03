namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Hubs;

/// <summary>
/// Handler business logic for the ProbeHub operation: a camera hub's cameras, each with a
/// picture. The hub's user and password are used for this call only; the cameras' stream
/// credentials are not returned (AddHubCamera reads them itself).
/// </summary>
public sealed class ProbeHubHandler(ICameraHubProvider hubs) : IProbeHubHandler
{
    public async Task<ProbeHubResult> ExecuteAsync(
        ProbeHubParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        if (MotionEyeHubProvider.ToHubAddress(request.Address) is null)
        {
            return ProbeHubResult.BadRequest($"'{request.Address}' is no address of a hub.");
        }

        var listing = await hubs.ListAsync(request.Address, request.UserName, request.Password, cancellationToken).ConfigureAwait(false);
        var cameras = new List<HubCameraInfo>(listing.Cameras.Count);
        foreach (var camera in listing.Cameras)
        {
            var picture = camera.Problem == HubCameraProblem.OnAnotherHub
                ? null
                : await hubs.GetSnapshotAsync(listing.Address, request.UserName, request.Password, camera.Id, cancellationToken).ConfigureAwait(false);
            cameras.Add(camera.ToApiModel(picture));
        }

        return ProbeHubResult.Ok(new HubProbeResult(
            Outcome: listing.Outcome.ToApiOutcome(),
            Kind: listing.Kind,
            Address: listing.Address.ToString(),
            OlderSignIn: listing.OlderSignIn,
            Cameras: cameras));
    }
}