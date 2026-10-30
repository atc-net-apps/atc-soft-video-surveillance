namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// How a change to a user went.
/// </summary>
public enum UserChangeOutcome
{
    Done,
    NotFound,
    NameTaken,

    /// <summary>The server always keeps one admin.</summary>
    LastAdmin,
}