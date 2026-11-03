namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Hubs;

/// <summary>
/// Handler business logic for the AddHubCamera operation: the hub is listed again, and the camera
/// is added as a network camera on its stream, with the stream's credentials from the hub (they
/// never go through a client) and the hub as its origin.
/// </summary>
public sealed class AddHubCameraHandler(
    ICameraHubProvider hubs,
    ICameraStorageService storage) : IAddHubCameraHandler
{
    public async Task<AddHubCameraResult> ExecuteAsync(
        AddHubCameraParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var listing = await hubs.ListAsync(request.Address, request.UserName, request.Password, cancellationToken).ConfigureAwait(false);
        if (listing.Outcome != CameraHubOutcome.Listed)
        {
            return AddHubCameraResult.BadRequest(listing.Outcome switch
            {
                CameraHubOutcome.SignInRefused => "The hub refused the user name or password.",
                CameraHubOutcome.NotThisKind => "Something answered at that address, but it is not motionEye.",
                _ => "Nothing answered at that address.",
            });
        }

        if (listing.Cameras.FirstOrDefault(c => string.Equals(c.Id, request.CameraId, StringComparison.Ordinal)) is not { } hubCamera)
        {
            return AddHubCameraResult.BadRequest($"The hub has no camera '{request.CameraId}'.");
        }

        var camera = new AtcSoft.Video.Surveillance.Models.CameraConfiguration();
        if (!HubCameraSetup.ApplyTo(camera, listing, hubCamera))
        {
            return AddHubCameraResult.BadRequest(hubCamera.Problem switch
            {
                HubCameraProblem.Disabled => $"The camera '{hubCamera.Name}' is switched off on the hub.",
                HubCameraProblem.OnAnotherHub => $"The camera '{hubCamera.Name}' is a camera of another motionEye; add it from that one.",
                _ => $"The camera '{hubCamera.Name}' streams its video to the hub only; turn on Video Streaming for it in motionEye.",
            });
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            camera.Display.DisplayName = request.DisplayName.Trim();
        }

        storage.AddOrUpdateCamera(camera);
        storage.Save();
        return AddHubCameraResult.Created(new HubCameraAdded(Id: camera.Id, DisplayName: camera.Display.DisplayName), location: $"/cameras/{camera.Id}");
    }
}