namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// The web client in Edge (headless) against the test server: sign in, every page, every tab,
/// the dialogs the pages and the user menu open, and every camera's form. It fails on a step
/// that breaks, an error in the browser console or a page error, a 5xx answer, and a fatal
/// entry in the server's output.
/// </summary>
/// <remarks>
/// Left out on purpose: downloads, deleting, the webhook test send, switching the language
/// (a page reload), and signing out.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class WebClientSmokeTests(ServerFixture server)
{
    private static readonly string[] Pages = ["/", "/cameras", "/cameras/create", "/layouts", "/live", "/recordings", "/timeline", "/webhooks", "/settings", "/users"];

    [Fact]
    public async Task Every_Page_Opens_Without_Errors()
    {
        E2ESettings.SkipUnlessEnabled();

        using var web = await WebClientRun.StartAsync(server);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = web.BaseUri.ToString(),
            Locale = "en-US",
        });

        // The app reads its server address from appsettings.json at start.
        await context.RouteAsync(
            "**/appsettings.json",
            route => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = WebClientRun.AppSettingsJson(server),
            }));

        var page = await context.NewPageAsync();
        var problems = new List<string>();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.Ordinal))
            {
                problems.Add($"console ({page.Url}): {message.Text}");
            }
        };
        page.PageError += (_, error) => problems.Add($"page error ({page.Url}): {error}");
        page.RequestFailed += (_, request) =>
        {
            // Leaving a page cancels its open requests (the event stream): not a failure.
            if (request.Failure?.Contains("ERR_ABORTED", StringComparison.Ordinal) != true)
            {
                problems.Add($"request failed: {request.Method} {request.Url} ({request.Failure})");
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status >= 500)
            {
                problems.Add($"{response.Status} {response.Request.Method} {response.Url}");
            }
        };

        var steps = new SmokeSteps();
        var shots = E2ESettings.NewFolder("web-client-ui");
        Func<string, Func<Task>, Task> run = (name, step) => steps.RunAsync(name, async () =>
        {
            try
            {
                await step();
            }
            catch
            {
                await SaveEvidenceAsync(page, shots, name);
                throw;
            }
        });

        await run("sign in", () => SignInAsync(page));

        foreach (var path in Pages)
        {
            await run($"page {path}", () => VisitAsync(page, path));
        }

        var cameraIds = await CameraIdsAsync();
        foreach (var cameraId in cameraIds)
        {
            await run($"camera form {cameraId}", () => VisitAsync(page, $"/cameras/{cameraId}/edit"));
        }

        await run("draw a motion zone", () => DrawMotionZoneAsync(page, cameraIds[^1], shots));

        await run("play a recording with its activity timeline", () => PlayWithTimelineAsync(page));
        await run("timeline lanes", () => TimelineLanesAsync(page));
        await run("play all from here", () => PlayAllAsync(page, shots));
        await run("Add user", () => OpenAndCancelAsync(page, "/users", "Add user"));
        await run("Make API key", () => OpenAndCancelAsync(page, "/users", "Make API key", tab: "API keys"));
        await run("Add receiver", () => OpenAndCancelAsync(page, "/webhooks", "Add receiver"));
        await run("Change password", () => ChangePasswordAsync(page));

        await File.WriteAllLinesAsync(Path.Combine(shots, "browser-problems.txt"), problems, TestContext.Current.CancellationToken);
        steps.Failures.Should().BeEmpty(steps.Report());
        problems.Should().BeEmpty("the web client must open every page without errors");
        server.ServerFailures().Should().BeEmpty($"the server must not fail while the web client walks (output: {server.ServerOutputPath})");
    }

    // The server wrote an activity index while the fixture recorded the live camera: the player
    // shows it under the video, whether or not the browser plays the file.
    private static async Task PlayWithTimelineAsync(IPage page)
    {
        await VisitAsync(page, "/recordings");

        // The list shows the recording's activity as a sparkline.
        await page.GetByRole(AriaRole.Img, new PageGetByRoleOptions { Name = "Activity", Exact = true }).First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Play", Exact = true }).First.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Img, new LocatorGetByRoleOptions { Name = "Activity", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Play events only", Exact = true }).ClickAsync();
        foreach (var name in new[] { "Zoom in", "Zoom out" })
        {
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        }

        // Resting the mouse on the timeline tells what happened there, above it.
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Img, new LocatorGetByRoleOptions { Name = "Activity", Exact = true }).HoverAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Tooltip).Filter(new LocatorFilterOptions { HasText = ":" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // A right-click offers verdicts on the place; "missed something here" is saved by the server.
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Img, new LocatorGetByRoleOptions { Name = "Activity", Exact = true })
            .ClickAsync(new LocatorClickOptions { Button = MouseButton.Right });
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Missed something here", Exact = true }).ClickAsync();
        await page.GetByText(VerdictWalk.Saved).WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // Its tooltip stays while the pointer rests on it, over the dialog's buttons.
        await page.Mouse.MoveAsync(0, 0);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Close", Exact = true }).Last.ClickAsync();
    }

    // Play all from here: a tile for the live camera, which leaves "Loading..." within the pacer's
    // give-up time whether or not the browser plays the recording's format; one pause for all.
    private static async Task PlayAllAsync(
        IPage page,
        string shots)
    {
        await VisitAsync(page, "/timeline");
        await page.GetByRole(AriaRole.Img, new PageGetByRoleOptions { Name = TestCameras.LiveCameraName, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Play all from here", Exact = true }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText(TestCameras.LiveCameraName, new LocatorGetByTextOptions { Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await dialog.GetByText("Loading...", new LocatorGetByTextOptions { Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 25_000 });
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(shots, "play-all.png") });

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Pause", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Play", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Close", Exact = true }).Last.ClickAsync();
    }

    // The form asks the server for a snapshot and shows the answer (the live test camera serves one
    // client, so there is often none to be had); a zone added is picked and can be dragged.
    // The form is left without saving. The form is loaded again although the step before has it
    // open: a page load renews the session, which once lost the race with a renewal in flight.
    private static async Task DrawMotionZoneAsync(
        IPage page,
        Guid cameraId,
        string shots)
    {
        await VisitAsync(page, $"/cameras/{cameraId}/edit");

        await page.GetByText("Motion zones", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Take Snapshot", Exact = true }).ClickAsync();
        await page.GetByText("No picture yet: take a snapshot to draw on the camera's picture.", new PageGetByTextOptions { Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 45_000 });

        var drawing = page.GetByRole(AriaRole.Img, new PageGetByRoleOptions { Name = "Motion zones", Exact = true });
        var failed = await page.GetByText("Failed to capture snapshot.", new PageGetByTextOptions { Exact = true }).IsVisibleAsync();
        (await drawing.Locator("image").CountAsync()).Should().Be(failed ? 0 : 1, "a snapshot that came is drawn under the zones");

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add zone", Exact = true }).ClickAsync();
        var removeZone = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Remove zone", Exact = true });
        (await removeZone.IsEnabledAsync()).Should().BeTrue("the new zone is picked");

        // The new zone covers 0.25 to 0.55 of the picture: drag it by its middle.
        var zone = drawing.Locator("polygon").Last;
        var before = await zone.GetAttributeAsync("points");
        await drawing.ScrollIntoViewIfNeededAsync();
        var box = (await drawing.BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + (box.Width * 0.4f), box.Y + (box.Height * 0.4f));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + (box.Width * 0.6f), box.Y + (box.Height * 0.5f), new MouseMoveOptions { Steps = 5 });
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(300);

        (await zone.GetAttributeAsync("points")).Should().NotBe(before, "the zone follows the pointer");
        (await removeZone.IsEnabledAsync()).Should().BeTrue("the dragged zone stays picked");
        await drawing.ScreenshotAsync(new LocatorScreenshotOptions { Path = Path.Combine(shots, "motion-zone.png") });
    }

    // Every camera has a lane on the Timeline page; the offline camera's is all gap.
    private static async Task TimelineLanesAsync(IPage page)
    {
        await VisitAsync(page, "/timeline");
        foreach (var name in new[] { TestCameras.LiveCameraName, TestCameras.DeadCameraName })
        {
            await page.GetByRole(AriaRole.Img, new PageGetByRoleOptions { Name = name, Exact = true })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        }
    }

    private static async Task SaveEvidenceAsync(
        IPage page,
        string folder,
        string step)
    {
        var name = string.Concat(step.Where(char.IsLetterOrDigit));
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(folder, name + ".png"), FullPage = true });
            await File.WriteAllTextAsync(Path.Combine(folder, name + ".txt"), page.Url + Environment.NewLine + await page.InnerTextAsync("body"));
        }
        catch (PlaywrightException)
        {
            // Evidence only.
        }
    }

    private static async Task SignInAsync(IPage page)
    {
        await page.GotoAsync("/");
        await page.GetByLabel("Username").FillAsync(ServerFixture.AdminName);
        await page.GetByLabel("Password").FillAsync(ServerFixture.AdminPassword);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = $"Signed in as {ServerFixture.AdminName}" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
    }

    private static async Task VisitAsync(
        IPage page,
        string path)
    {
        await page.GotoAsync(path);
        await page.WaitForTimeoutAsync(2500);

        // Every tab of the page, so each tab's content is rendered.
        var tabs = page.GetByRole(AriaRole.Tab);
        var count = await tabs.CountAsync();
        for (var i = 0; i < count; i++)
        {
            await tabs.Nth(i).ClickAsync();
            await page.WaitForTimeoutAsync(400);
        }
    }

    private static async Task OpenAndCancelAsync(
        IPage page,
        string path,
        string button,
        string? tab = null)
    {
        await page.GotoAsync(path);
        await page.WaitForTimeoutAsync(1500);
        if (tab is not null)
        {
            await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = tab }).ClickAsync();
            await page.WaitForTimeoutAsync(400);
        }

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = button }).ClickAsync();
        await CancelDialogAsync(page);
    }

    private static async Task ChangePasswordAsync(IPage page)
    {
        await page.GotoAsync("/");
        await page.WaitForTimeoutAsync(1500);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = $"Signed in as {ServerFixture.AdminName}" }).ClickAsync();
        await page.GetByText("Change password").First.ClickAsync();
        await CancelDialogAsync(page);
    }

    private static async Task CancelDialogAsync(IPage page)
    {
        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(500);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel" }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10_000 });
    }

    private async Task<IReadOnlyList<Guid>> CameraIdsAsync()
    {
        using var admin = await server.CreateAdminClientAsync();
        var cameras = await admin.GetFromJsonAsync<JsonElement>("/cameras");
        var ids = new List<Guid>();
        foreach (var camera in cameras.EnumerateArray())
        {
            ids.Add(camera.GetProperty("id").GetGuid());
        }

        return ids;
    }
}