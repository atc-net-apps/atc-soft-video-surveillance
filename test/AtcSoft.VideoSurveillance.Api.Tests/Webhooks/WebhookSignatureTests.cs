namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed class WebhookSignatureTests
{
    [Fact]
    public void The_Signature_Is_The_Hmac_Sha256_Of_The_Body()
    {
        // A known pair: any receiver can check it with a standard HMAC function.
        var signature = WebhookSignature.Compute("secret", "{\"event\":\"test\"}"u8);

        signature.Should().Be("sha256=" + Convert.ToHexStringLower(
            System.Security.Cryptography.HMACSHA256.HashData("secret"u8, "{\"event\":\"test\"}"u8)));
        signature.Should().MatchRegex("^sha256=[0-9a-f]{64}$");
    }

    [Fact]
    public void A_Different_Body_Or_Secret_Gives_A_Different_Signature()
    {
        var original = WebhookSignature.Compute("secret", "a"u8);

        WebhookSignature.Compute("secret", "b"u8).Should().NotBe(original);
        WebhookSignature.Compute("other", "a"u8).Should().NotBe(original);
    }
}