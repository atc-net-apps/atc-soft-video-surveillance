using CoreHubCamera = AtcSoft.Video.Surveillance.Services.Hubs.HubCamera;
using CoreHubOrigin = AtcSoft.Video.Surveillance.Models.Settings.CameraHubOrigin;

namespace AtcSoft.Video.Surveillance.Api.Domain.Mapping;

internal static class HubMappingExtensions
{
    public static HubCameraInfo ToApiModel(
        this CoreHubCamera camera,
        byte[]? picture)
        => new(
            Id: camera.Id,
            Name: camera.Name,
            StreamKind: camera.StreamKind == HubStreamKind.Rtsp ? HubCameraInfoStreamKind.Rtsp : HubCameraInfoStreamKind.Mjpeg,
            Resolution: camera.Resolution,
            FramesPerSecond: camera.FramesPerSecond,
            IsUsb: camera.IsUsb,
            Problem: camera.Problem switch
            {
                HubCameraProblem.Disabled => HubCameraInfoProblem.Disabled,
                HubCameraProblem.StreamingOff => HubCameraInfoProblem.StreamingOff,
                HubCameraProblem.OnAnotherHub => HubCameraInfoProblem.OnAnotherHub,
                _ => HubCameraInfoProblem.None,
            },
            Picture: picture);

    public static HubOrigin? ToApiModel(this CoreHubOrigin? origin)
        => origin is null
            ? null
            : new HubOrigin(
                Kind: origin.Kind,
                Address: origin.Address,
                CameraId: origin.CameraId,
                CameraName: origin.CameraName);

    public static HubProbeResultOutcome ToApiOutcome(
        this CameraHubOutcome outcome)
        => outcome switch
        {
            CameraHubOutcome.Listed => HubProbeResultOutcome.Listed,
            CameraHubOutcome.SignInRefused => HubProbeResultOutcome.SignInRefused,
            CameraHubOutcome.NotThisKind => HubProbeResultOutcome.NotThisKind,
            _ => HubProbeResultOutcome.Unreachable,
        };
}