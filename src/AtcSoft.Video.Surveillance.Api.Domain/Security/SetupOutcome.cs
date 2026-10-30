namespace AtcSoft.Video.Surveillance.Api.Domain.Security;

/// <summary>
/// How a request to create the first admin went.
/// </summary>
public enum SetupOutcome
{
    Done,
    WrongCode,
    AlreadySetUp,
}