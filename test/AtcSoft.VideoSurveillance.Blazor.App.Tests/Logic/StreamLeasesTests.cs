namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The page's leases on the streams it shows: renewed while it watches, and a
/// lease the server no longer has is handed back so its stream starts again.
/// </summary>
public sealed class StreamLeasesTests
{
    private static readonly Guid Front = Guid.NewGuid();
    private static readonly Guid Back = Guid.NewGuid();

    [Fact]
    public async Task Every_Lease_Is_Renewed_And_Those_The_Server_No_Longer_Has_Are_Given_Up()
    {
        var sut = new StreamLeases();
        var frontLease = Guid.NewGuid();
        var backLease = Guid.NewGuid();
        sut.Add(Front, frontLease, renewSeconds: 30);
        sut.Add(Back, backLease, renewSeconds: 30);
        var renewed = new List<(Guid Camera, Guid Lease)>();

        var gone = await sut.RenewAllAsync((camera, lease) =>
        {
            renewed.Add((camera, lease));
            return Task.FromResult(camera == Back ? StreamLeaseRenewal.Gone : StreamLeaseRenewal.Renewed);
        });

        renewed.Should().BeEquivalentTo([(Front, frontLease), (Back, backLease)]);
        gone.Should().Equal(Back);
        sut.Has(Back).Should().BeFalse();
        sut.Has(Front).Should().BeTrue();
    }

    [Fact]
    public async Task A_Lease_That_Could_Not_Be_Renewed_Because_The_Server_Is_Away_Is_Kept_For_The_Next_Time()
    {
        var sut = new StreamLeases();
        sut.Add(Front, Guid.NewGuid(), renewSeconds: 30);

        var gone = await sut.RenewAllAsync((_, _) => Task.FromResult(StreamLeaseRenewal.Unreachable));

        gone.Should().BeEmpty();
        sut.Has(Front).Should().BeTrue();
    }

    [Fact]
    public void A_Lease_Is_Taken_Once_To_Be_Given_Back()
    {
        var sut = new StreamLeases();
        var lease = Guid.NewGuid();
        sut.Add(Front, lease, renewSeconds: 30);

        sut.TryTake(Front, out var taken).Should().BeTrue();
        taken.Should().Be(lease);
        sut.TryTake(Front, out _).Should().BeFalse();
    }

    [Fact]
    public void The_Leases_Are_Renewed_As_Often_As_The_Server_Asks_And_Every_Half_Minute_Before_It_Said()
    {
        var sut = new StreamLeases();
        sut.RenewInterval.Should().Be(TimeSpan.FromSeconds(30));

        sut.Add(Front, Guid.NewGuid(), renewSeconds: 20);
        sut.Add(Back, Guid.NewGuid(), renewSeconds: 40);

        sut.RenewInterval.Should().Be(TimeSpan.FromSeconds(20));
    }
}