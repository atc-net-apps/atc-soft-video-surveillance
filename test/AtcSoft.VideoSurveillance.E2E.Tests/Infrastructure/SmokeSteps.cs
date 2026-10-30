namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Runs the steps of a smoke walk one after another. A step that fails is recorded and the
/// walk goes on, so one run reports every dialog that is broken, not only the first.
/// </summary>
internal sealed class SmokeSteps
{
    private readonly List<string> failures = [];
    private readonly List<string> done = [];
    private readonly Action? recover;

    public SmokeSteps(Action? recover = null)
        => this.recover = recover;

    public IReadOnlyList<string> Failures
        => failures;

    public IReadOnlyList<string> Done
        => done;

    public void Run(
        string name,
        Action step)
    {
        ArgumentNullException.ThrowIfNull(step);

        try
        {
            step();
            done.Add(name);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
            try
            {
                recover?.Invoke();
            }
            catch (Exception recoverEx) when (recoverEx is not OutOfMemoryException)
            {
                failures.Add($"{name} (recovery): {recoverEx.Message}");
            }
        }
    }

    public async Task RunAsync(
        string name,
        Func<Task> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        try
        {
            await step();
            done.Add(name);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public string Report()
        => $"Steps done ({done.Count}): {string.Join(", ", done)}{Environment.NewLine}"
           + $"Steps failed ({failures.Count}):{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", failures)}";
}