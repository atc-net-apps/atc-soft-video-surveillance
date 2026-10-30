namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The leases a page holds on the streams it shows, one per camera.
/// </summary>
public sealed class StreamLeases
{
    // How often the leases are renewed until the server has said.
    private static readonly TimeSpan DefaultRenewInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<Guid, Guid> leases = new();

    /// <summary>
    /// Gets how often the leases are renewed: the shortest the server asked for.
    /// </summary>
    public TimeSpan RenewInterval { get; private set; } = DefaultRenewInterval;

    public bool Has(Guid cameraId)
        => leases.ContainsKey(cameraId);

    public void Add(
        Guid cameraId,
        Guid leaseId,
        int renewSeconds)
    {
        leases[cameraId] = leaseId;
        if (renewSeconds > 0 && TimeSpan.FromSeconds(renewSeconds) < RenewInterval)
        {
            RenewInterval = TimeSpan.FromSeconds(renewSeconds);
        }
    }

    /// <summary>
    /// Takes a camera's lease off the page, to give it back.
    /// </summary>
    public bool TryTake(
        Guid cameraId,
        out Guid leaseId)
        => leases.Remove(cameraId, out leaseId);

    /// <summary>
    /// Renews every lease; returns the cameras whose lease the server no
    /// longer has, which are taken off the page.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> RenewAllAsync(
        Func<Guid, Guid, Task<StreamLeaseRenewal>> renew)
    {
        ArgumentNullException.ThrowIfNull(renew);

        var gone = new List<Guid>();
        foreach (var (cameraId, leaseId) in leases.ToArray())
        {
            if (await renew(cameraId, leaseId).ConfigureAwait(true) == StreamLeaseRenewal.Gone &&
                leases.TryGetValue(cameraId, out var current) &&
                current == leaseId)
            {
                leases.Remove(cameraId);
                gone.Add(cameraId);
            }
        }

        return gone;
    }
}