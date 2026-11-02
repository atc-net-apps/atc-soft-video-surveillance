namespace AtcSoft.Video.Surveillance.Api.Security;

public sealed partial class AccountService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The server has no user yet. Create the first admin with the setup code in {SetupCodePath}")]
    private partial void LogSetupRequired(string setupCodePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The server has no user yet. Create the first admin with the setup code {SetupCode} (also in {SetupCodePath})")]
    private partial void LogSetupRequiredWithCode(string setupCodePath, string setupCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "The first admin '{UserName}' was created")]
    private partial void LogSetupCompleted(string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "The first admin '{UserName}' was created on the server's own machine, without the setup code")]
    private partial void LogSetupCompletedLocally(string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A request to create the first admin had the wrong setup code")]
    private partial void LogWrongSetupCode();

    [LoggerMessage(Level = LogLevel.Information, Message = "A sign-in was refused")]
    private partial void LogSignInRefused();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A refresh token of '{UserName}' that was already renewed was used again after its grace window; the session was ended")]
    private partial void LogRefreshTokenReused(string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The access to {Path} could not be restricted; other users of this machine may be able to read it. Move the server's data folder (Storage:DataPath) to a folder only the server's account can use")]
    private partial void LogSecretNotRestricted(string path);
}