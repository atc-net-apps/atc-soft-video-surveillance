namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The webhook receivers through the real server: added, changed, tested
/// and removed over the API, without a restart.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostWebhooksTests(ApiHostFixture host)
{
    [Fact]
    public async Task The_Configured_Receiver_Is_Listed_And_Cannot_Be_Changed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();

        var listed = await client.GetFromJsonAsync<JsonElement>("/webhooks", cancellationToken);

        var configured = listed.EnumerateArray().First(w => w.GetProperty("url").GetString() == host.Webhooks.Address);
        configured.GetProperty("isEditable").GetBoolean().Should().BeFalse();
        configured.GetProperty("hasSecret").GetBoolean().Should().BeTrue();
        listed.GetRawText().Should().NotContain(ApiHostFixture.WebhookSecret, "a secret never leaves the server");

        var id = configured.GetProperty("id").GetGuid();
        var deleted = await client.DeleteAsync(new Uri($"/webhooks/{id}", UriKind.Relative), cancellationToken);
        deleted.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_Receiver_Is_Added_Tested_Changed_And_Removed_Without_A_Restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        await using var receiver = new WebhookReceiver();

        // Add
        var created = await client.PostAsJsonAsync(
            "/webhooks",
            new { url = receiver.Address, secret = "added-secret", events = new[] { "motion.*" } },
            cancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var id = webhook.GetProperty("id").GetGuid();
        webhook.GetProperty("isEditable").GetBoolean().Should().BeTrue();
        webhook.GetProperty("hasSecret").GetBoolean().Should().BeTrue();
        webhook.GetRawText().Should().NotContain("added-secret");
        File.Exists(Path.Combine(host.DataPath, "webhooks.json")).Should().BeTrue("it has to be there after a restart");

        try
        {
            // Test — the receiver gets a signed test event
            var tested = await client.PostAsync(new Uri($"/webhooks/{id}/test", UriKind.Relative), content: null, cancellationToken);
            tested.StatusCode.Should().Be(HttpStatusCode.OK);
            (await tested.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("delivered").GetBoolean().Should().BeTrue();
            var received = receiver.Received.Should().ContainSingle().Subject;
            received.EventType.Should().Be("webhook.test");
            received.Signature.Should().Be(AtcSoft.VideoSurveillance.Api.Webhooks.WebhookSignature.Compute("added-secret", System.Text.Encoding.UTF8.GetBytes(received.Body)));

            // Change — without a secret in the request, the secret stays
            var changed = await client.PutAsJsonAsync(
                $"/webhooks/{id}",
                new { url = receiver.Address, events = new[] { "recording.*" } },
                cancellationToken);
            changed.StatusCode.Should().Be(HttpStatusCode.OK);
            var after = await changed.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            after.GetProperty("hasSecret").GetBoolean().Should().BeTrue();
            after.GetProperty("events").EnumerateArray().Select(e => e.GetString()).Should().Equal("recording.*");

            var invalid = await client.PutAsJsonAsync($"/webhooks/{id}", new { url = "nonsense" }, cancellationToken);
            invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            // Remove
            var deleted = await client.DeleteAsync(new Uri($"/webhooks/{id}", UriKind.Relative), cancellationToken);
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var listed = await client.GetFromJsonAsync<JsonElement>("/webhooks", cancellationToken);
        listed.EnumerateArray().Should().NotContain(w => w.GetProperty("id").GetGuid() == id);

        var gone = await client.PostAsync(new Uri($"/webhooks/{id}/test", UriKind.Relative), content: null, cancellationToken);
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Receiver_Without_A_Usable_Address_Is_Refused()
    {
        using var client = host.CreateClient();

        var created = await client.PostAsJsonAsync("/webhooks", new { url = "ftp://files.test/hook" }, TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}