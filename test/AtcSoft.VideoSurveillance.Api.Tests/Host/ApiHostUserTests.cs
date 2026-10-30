namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// An admin manages users and API keys on the running server; only an admin
/// may, and what they change is in the audit log.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostUserTests(ApiHostFixture host)
{
    [Fact]
    public async Task An_Admin_Adds_A_Viewer_Who_May_Not_Manage_Users_And_The_Changes_Are_Audited()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await host.CreateAdminClientAsync();
        var name = "viewer-" + Guid.NewGuid().ToString("N")[..8];

        var created = await admin.PostAsJsonAsync("/users", new { userName = name, password = "viewer password", role = "viewer" }, cancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        created.Headers.Location!.ToString().Should().Be($"/users/{userId}");
        (await admin.PostAsJsonAsync("/users", new { userName = name.ToUpperInvariant(), password = "viewer password", role = "viewer" }, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The viewer signs in, and is not let in to manage users.
        var (status, session) = await host.SignInAsync(name, "viewer password");
        status.Should().Be(StatusCodes.Status200OK);
        session.GetProperty("role").GetString().Should().Be("viewer");
        using (var asViewer = new HttpRequestMessage(HttpMethod.Get, "/users"))
        {
            asViewer.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
            using var client = host.CreateAnonymousClient();
            (await client.SendAsync(asViewer, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync(new Uri("/users", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // The admin made the user, the user signed in: both are in the audit log.
        var audit = await admin.GetFromJsonAsync<JsonElement[]>("/audit?take=20", cancellationToken);
        audit!.Should().Contain(e => e.GetProperty("action").GetString() == "createUser" && e.GetProperty("userName").GetString() == ApiHostFixture.AdminName);
        audit.Should().Contain(e => e.GetProperty("action").GetString() == "createSession" && e.GetProperty("userName").GetString() == name);

        (await admin.DeleteAsync(new Uri($"/users/{userId}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.SignInAsync(name, "viewer password")).Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task An_Api_Key_Is_Shown_When_It_Is_Made_And_Never_Again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await host.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api-keys", new { name = "Home Assistant", role = "viewer" }, cancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var key = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var secret = key.GetProperty("key").GetString()!;
        var keyId = key.GetProperty("id").GetGuid();

        var listed = await admin.GetStringAsync(new Uri("/api-keys", UriKind.Relative), cancellationToken);
        listed.Should().Contain("Home Assistant").And.NotContain(secret);

        (await admin.DeleteAsync(new Uri($"/api-keys/{keyId}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync(new Uri($"/api-keys/{keyId}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_Last_Admin_Stays()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await host.CreateAdminClientAsync();
        var users = await admin.GetFromJsonAsync<JsonElement[]>("/users", cancellationToken);
        var adminId = users!.Single(u => u.GetProperty("userName").GetString() == ApiHostFixture.AdminName).GetProperty("id").GetGuid();

        var demoted = await admin.PutAsJsonAsync($"/users/{adminId}", new { role = "viewer" }, cancellationToken);

        demoted.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}