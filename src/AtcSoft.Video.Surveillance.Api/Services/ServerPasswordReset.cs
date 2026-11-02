namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// <c>--reset-password &lt;user&gt;</c>: gives a user a new password, written to the console, for
/// the admin who forgot theirs. Run on the server by someone who may read its data folder (as
/// administrator on Windows). The server must not run meanwhile, as it keeps the users in
/// memory and would write over the change: on Windows the service is stopped and started again,
/// elsewhere a running server is refused.
/// </summary>
public static class ServerPasswordReset
{
    /// <summary>
    /// The command-line switch, followed by the user's name.
    /// </summary>
    public const string Argument = "--reset-password";

    /// <summary>
    /// How many characters a new password has.
    /// </summary>
    public const int PasswordLength = 20;

    // Letters and digits that cannot be taken for one another.
    private const string NewSecretCharacters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Takes the switch and the name after it out of the command line.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="userName">The name after the switch; <see langword="null"/> when it has none.</param>
    /// <param name="rest">The command line without them.</param>
    /// <returns>Whether the switch was there.</returns>
    public static bool TakeArgument(
        string[] args,
        out string? userName,
        out string[] rest)
    {
        ArgumentNullException.ThrowIfNull(args);

        var index = Array.FindIndex(args, a => string.Equals(a, Argument, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            userName = null;
            rest = args;
            return false;
        }

        var hasName = index + 1 < args.Length && !args[index + 1].StartsWith('-');
        userName = hasName ? args[index + 1] : null;
        rest = args.Where((_, i) => i != index && !(hasName && i == index + 1)).ToArray();
        return true;
    }

    /// <summary>
    /// Gives the user a new password; on Windows the running service is stopped first and
    /// started again after.
    /// </summary>
    /// <param name="paths">The server's data folder.</param>
    /// <param name="userName">The user, whatever the case.</param>
    /// <param name="output">Where the new password and any problem are written.</param>
    /// <returns>The exit code: 0 when the password was changed.</returns>
    public static int Run(
        ServerDataPaths paths,
        string? userName,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(output);

#if WINDOWS
        using var service = RunningService();
        if (service is not null)
        {
            try
            {
                output.WriteLine("Stopping the service " + ServerHosting.ServiceName + " ...");
                service.Stop();
                service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException)
            {
                output.WriteLine("The service " + ServerHosting.ServiceName + " could not be stopped; run this as administrator.");
                return 1;
            }
        }

        try
        {
            return Reset(paths, userName, output);
        }
        finally
        {
            if (service is not null)
            {
                service.Start();
                output.WriteLine("The service " + ServerHosting.ServiceName + " is started again.");
            }
        }
#else
        return Reset(paths, userName, output);
#endif
    }

    /// <summary>
    /// Gives the user a new password, when no server runs on the data folder.
    /// </summary>
    /// <param name="paths">The server's data folder.</param>
    /// <param name="userName">The user, whatever the case.</param>
    /// <param name="output">Where the new password and any problem are written.</param>
    /// <returns>The exit code: 0 when the password was changed.</returns>
    public static int Reset(
        ServerDataPaths paths,
        string? userName,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(userName))
        {
            output.WriteLine("Name the user: " + Argument + " <user name>");
            return 2;
        }

        if (!File.Exists(paths.AccountsPath))
        {
            output.WriteLine(
                "The server has no user yet. Open the web client in a browser on the server to create the first admin, " +
                "or use the code in " + paths.SetupCodePath + ".");
            return 1;
        }

        try
        {
            using var readable = File.OpenRead(paths.AccountsPath);
        }
        catch (UnauthorizedAccessException)
        {
            output.WriteLine("The server's users in " + paths.AccountsPath + " cannot be read; run this as administrator.");
            return 1;
        }

        using var serverLock = ServerLock.TryAcquire(paths.LockPath);
        if (serverLock is null)
        {
            output.WriteLine("The server is running on this data folder; stop it first. It would write over the new password.");
            return 1;
        }

        var accounts = new AccountService(paths, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);
        var users = accounts.ListUsers();
        var user = users.FirstOrDefault(u => string.Equals(u.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            output.WriteLine($"The server has no user '{userName.Trim()}'. Its users: {string.Join(", ", users.Select(u => u.UserName))}");
            return 1;
        }

        var password = new string(RandomNumberGenerator.GetItems(NewSecretCharacters.AsSpan(), PasswordLength));
        var (outcome, _) = accounts.UpdateUser(user.Id, role: null, password);
        if (outcome != UserChangeOutcome.Done)
        {
            output.WriteLine($"The password of '{user.UserName}' could not be changed ({outcome}).");
            return 1;
        }

        // To the console only, never to the log.
        output.WriteLine($"The new password of '{user.UserName}' is: {password}");
        output.WriteLine("Sign in with it and choose your own under your account. The user's sessions have ended.");
        return 0;
    }

#if WINDOWS
    private static System.ServiceProcess.ServiceController? RunningService()
    {
        var service = new System.ServiceProcess.ServiceController(ServerHosting.ServiceName);
        try
        {
            if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
            {
                return service;
            }
        }
        catch (InvalidOperationException)
        {
            // Not installed.
        }

        service.Dispose();
        return null;
    }
#endif
}