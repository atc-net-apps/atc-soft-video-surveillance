namespace AtcSoft.VideoSurveillance.Api.Security;

public sealed partial class AccountService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The server has no user yet. Create the first admin with the setup code in {SetupCodePath}")]
    private partial void LogSetupRequired(string setupCodePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "The first admin '{UserName}' was created")]
    private partial void LogSetupCompleted(string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A request to create the first admin had the wrong setup code")]
    private partial void LogWrongSetupCode();

    [LoggerMessage(Level = LogLevel.Information, Message = "A sign-in was refused")]
    private partial void LogSignInRefused();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A refresh token of '{UserName}' that was already renewed was used again after its grace window; the session was ended")]
    private partial void LogRefreshTokenReused(string userName);

    [LoggerMessage(Level = LogLevel.Error, Message = "The accounts could not be saved to {Path}")]
    private partial void LogSaveFailed(string path);
}