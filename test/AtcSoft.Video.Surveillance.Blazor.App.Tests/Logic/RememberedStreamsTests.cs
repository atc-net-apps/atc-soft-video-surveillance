namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The cameras the viewer had playing in each layout are kept in the browser, so a reload starts
/// them again. Before, a reload (a language change, the browser started again) started nothing.
/// </summary>
public sealed class RememberedStreamsTests
{
    private static readonly Guid Garden = Guid.NewGuid();
    private static readonly Guid Yard = Guid.NewGuid();
    private static readonly Guid FrontDoor = Guid.NewGuid();
    private static readonly Guid Garage = Guid.NewGuid();

    [Fact]
    public void What_The_Viewer_Started_Is_Started_Again_After_A_Reload_In_Its_Own_Layout()
    {
        var before = new RememberedStreams();
        before.Remember(Garden, FrontDoor).Should().BeTrue();
        before.Remember(Garden, Garage).Should().BeTrue();
        before.Remember(Yard, Garage).Should().BeTrue();

        var after = RememberedStreams.Parse(before.ToText());

        after.For(Garden).Should().BeEquivalentTo([FrontDoor, Garage]);
        after.For(Yard).Should().BeEquivalentTo([Garage]);
    }

    [Fact]
    public void A_Camera_The_Viewer_Stopped_Is_Not_Started_Again()
    {
        var remembered = new RememberedStreams();
        remembered.Remember(Garden, FrontDoor);
        remembered.Remember(Garden, Garage);

        remembered.Forget(Garden, FrontDoor).Should().BeTrue();

        RememberedStreams.Parse(remembered.ToText()).For(Garden).Should().Equal(Garage);
    }

    [Fact]
    public void Only_A_Change_Is_To_Be_Kept()
    {
        var remembered = new RememberedStreams();
        remembered.Remember(Garden, FrontDoor);

        remembered.Remember(Garden, FrontDoor).Should().BeFalse("it was remembered already");
        remembered.Forget(Yard, FrontDoor).Should().BeFalse("it was never remembered there");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    public void Nothing_Or_Something_Damaged_Kept_Is_An_Empty_Memory(
        string? text)
        => RememberedStreams.Parse(text).For(Garden).Should().BeEmpty();
}