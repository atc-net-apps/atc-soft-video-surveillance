namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// A camera password or a webhook secret is kept encrypted in the data folder;
/// a value written before that is read as it is.
/// </summary>
public sealed class SecretProtectorTests
{
    private readonly SecretProtector sut = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void A_Secret_Is_Kept_Encrypted_And_Read_Back()
    {
        var stored = sut.Protect("camera-password");

        stored.Should().StartWith(SecretProtector.Prefix);
        stored.Should().NotContain("camera-password");

        sut.TryUnprotect(stored, out var value, out var wasPlain).Should().BeTrue();
        value.Should().Be("camera-password");
        wasPlain.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_Stays_Nothing(string? secret)
    {
        sut.Protect(secret).Should().Be(secret);

        sut.TryUnprotect(secret, out var value, out var wasPlain).Should().BeTrue();
        value.Should().Be(secret);
        wasPlain.Should().BeFalse();
    }

    [Fact]
    public void A_Secret_Written_Before_Encryption_Is_Read_As_It_Is()
    {
        sut.TryUnprotect("old-password", out var value, out var wasPlain).Should().BeTrue();

        value.Should().Be("old-password");
        wasPlain.Should().BeTrue();
    }

    [Fact]
    public void A_Secret_From_Other_Keys_Cannot_Be_Read()
    {
        var stored = new SecretProtector(new EphemeralDataProtectionProvider()).Protect("camera-password");

        sut.TryUnprotect(stored, out var value, out _).Should().BeFalse();
        value.Should().BeNull();
    }
}