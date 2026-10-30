namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The renderer draws the same frame the same way every time, and its truth is where the
/// pixels really are.
/// </summary>
public sealed class SceneRendererTests
{
    [Fact]
    public void A_Frame_Is_The_Same_Every_Time_And_On_Its_Own()
    {
        var scene = Scene();

        var first = new SceneRenderer(scene).Render(30);
        var again = new SceneRenderer(scene).Render(30);

        again.Luma.Should().Equal(first.Luma);
    }

    [Fact]
    public void Another_Seed_Draws_Another_Scene()
    {
        var scene = Scene();
        var other = Scene(seed: 8);

        new SceneRenderer(other).Render(0).Luma.Should().NotEqual(new SceneRenderer(scene).Render(0).Luma);
    }

    [Fact]
    public void An_Objects_Truth_Is_Where_It_Was_Drawn()
    {
        // A car 200 × 100 px whose centre goes from x 300 (at 1 s) to x 900 (at 5 s), at y 360: halfway at 3 s.
        var renderer = new SceneRenderer(Scene());

        var frame = renderer.Render(3 * 15);
        var car = frame.Truth.Single(t => t.Id == "car");

        car.State.Should().Be(TruthState.MustBox);
        car.Box.CenterX.Should().BeApproximately(600, 4);
        car.Box.CenterY.Should().BeApproximately(360, 8);
        car.Box.Width.Should().BeApproximately(200, 4);
    }

    [Fact]
    public void An_Object_Is_Only_There_Between_Its_First_And_Last_Key_Point()
    {
        var renderer = new SceneRenderer(Scene());

        renderer.Render(0).Truth.Should().NotContain(t => t.Id == "car");
        renderer.Render(6 * 15).Truth.Should().NotContain(t => t.Id == "car");
    }

    [Fact]
    public void An_Object_That_Stands_Still_Need_Not_Get_A_Box()
    {
        var scene = Scene();
        scene.Objects[0].Path.Add(new StorybookKeyframe { T = 7, At = [0.703125, 0.5] });
        var renderer = new SceneRenderer(scene);

        renderer.Render((int)(6.5 * 15)).Truth.Single(t => t.Id == "car").State.Should().Be(TruthState.DontCare);
    }

    [Fact]
    public void An_Object_Mostly_Outside_The_Picture_Need_Not_Get_A_Box_Yet()
    {
        var scene = Scene();
        scene.Objects[0].Path[0].At[0] = -0.1;
        var renderer = new SceneRenderer(scene);

        // At 1.5 s a third of the car is in the picture.
        renderer.Render((int)(1.5 * 15)).Truth.Single(t => t.Id == "car").State.Should().Be(TruthState.DontCare);
    }

    [Fact]
    public void Someone_Close_To_The_Lens_Needs_A_Box_Though_Most_Of_Them_Is_Outside()
    {
        var scene = Scene();
        scene.Objects.Add(new StorybookObject
        {
            Id = "human",
            Kind = "human",
            Size = [1.3, 2.2],
            Path =
            [
                new StorybookKeyframe { T = 1, At = [0.6, 0.95] },
                new StorybookKeyframe { T = 5, At = [0.4, 0.95] },
            ],
        });
        var renderer = new SceneRenderer(scene);

        renderer.Render(3 * 15).Truth.Single(t => t.Id == "human").State.Should().Be(TruthState.MustBox);
    }

    [Fact]
    public void A_Tree_Is_A_Region_No_Box_May_Be_In()
    {
        var scene = Scene();
        scene.Background.Distractors.Add(new StorybookDistractor { Id = "tree", Kind = "swayingTree", At = [0.2, 0.8], Size = 0.4 });

        var tree = new SceneRenderer(scene).Render(10).Truth.Single(t => t.Id == "tree");

        tree.IsDistractor.Should().BeTrue();
        tree.State.Should().Be(TruthState.MustNotBox);
        tree.Box.Area.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_Swaying_Tree_Changes_Its_Pixels_From_Frame_To_Frame()
    {
        var scene = Scene();
        scene.Video.Noise = 0;
        scene.Objects.Clear();
        scene.Background.Distractors.Add(new StorybookDistractor { Id = "tree", Kind = "swayingTree", At = [0.2, 0.8], Size = 0.4, Amplitude = 0.02 });
        var renderer = new SceneRenderer(scene);

        var a = renderer.Render(0).Luma;
        var b = renderer.Render(5).Luma;

        a.Zip(b).Count(p => p.First != p.Second).Should().BeGreaterThan(100);
    }

    private static StorybookScene Scene(int seed = 7)
        => new()
        {
            Name = "test",
            Video = new StorybookVideo { Width = 640 * 2, Height = 720, Fps = 15, DurationSeconds = 8, Noise = 2 },
            Background = new StorybookBackground { Seed = seed },
            Objects =
            [
                new StorybookObject
                {
                    Id = "car",
                    Kind = "car",
                    Size = [200 / 1280.0, 100 / 720.0],
                    Path =
                    [
                        new StorybookKeyframe { T = 1, At = [300 / 1280.0, 0.5] },
                        new StorybookKeyframe { T = 5, At = [900 / 1280.0, 0.5] },
                    ],
                },
            ],
        };
}