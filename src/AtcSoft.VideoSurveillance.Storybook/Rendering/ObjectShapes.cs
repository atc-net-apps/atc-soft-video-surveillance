namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// Simple silhouettes of a car, a human, a cat and a dog, with some texture so they look like
/// more than a flat patch to a motion detector. Good enough for motion; not for an AI model.
/// </summary>
internal static class ObjectShapes
{
    public static void Draw(
        Canvas canvas,
        string kind,
        double left,
        double top,
        double w,
        double h,
        double time,
        bool moving,
        int seed,
        int? shade = null)
    {
        switch (kind)
        {
            case "car":
                DrawCar(canvas, left, top, w, h, seed);
                break;
            case "human":
                DrawHuman(canvas, left, top, w, h, time, moving, seed);
                break;
            case "cat":
                DrawAnimal(canvas, left, top, w, h, shade ?? 40, seed);
                break;
            case "dog":
                DrawAnimal(canvas, left, top, w, h, shade ?? 150, seed);
                break;
        }
    }

    private static Func<int, int, int> Textured(
        int shade,
        int amplitude,
        int seed)
        => (x, y) => shade + (int)((ScenePrng.Hash(seed, x / 2, y / 2) - 0.5) * amplitude);

    private static void DrawCar(
        Canvas canvas,
        double left,
        double top,
        double w,
        double h,
        int seed)
    {
        var body = Textured(205, 16, seed + 11);
        canvas.FillRect(left, top + (h * 0.35), w, h * 0.45, body);
        canvas.FillRect(left + (w * 0.2), top, w * 0.55, h * 0.4, body);
        canvas.FillRect(left + (w * 0.25), top + (h * 0.06), w * 0.45, h * 0.28, Textured(70, 10, seed + 12));
        var wheel = Textured(25, 8, seed + 13);
        canvas.FillEllipse(left + (w * 0.2), top + (h * 0.82), h * 0.18, h * 0.18, wheel);
        canvas.FillEllipse(left + (w * 0.8), top + (h * 0.82), h * 0.18, h * 0.18, wheel);
    }

    private static void DrawHuman(
        Canvas canvas,
        double left,
        double top,
        double w,
        double h,
        double time,
        bool moving,
        int seed)
    {
        var skin = Textured(170, 12, seed + 21);
        var clothes = Textured(35, 14, seed + 22);
        var trousers = Textured(55, 12, seed + 23);
        canvas.FillEllipse(left + (w * 0.5), top + (h * 0.1), w * 0.3, h * 0.1, skin);
        canvas.FillRect(left + (w * 0.15), top + (h * 0.2), w * 0.7, h * 0.42, clothes);

        // The legs swing while walking.
        var swing = moving ? Math.Sin(time * Math.PI * 2 * 1.6) * w * 0.18 : 0;
        canvas.FillRect(left + (w * 0.2) + swing, top + (h * 0.62), w * 0.25, h * 0.38, trousers);
        canvas.FillRect(left + (w * 0.55) - swing, top + (h * 0.62), w * 0.25, h * 0.38, trousers);
    }

    private static void DrawAnimal(
        Canvas canvas,
        double left,
        double top,
        double w,
        double h,
        int shade,
        int seed)
    {
        var fur = Textured(shade, 20, seed + 31);
        canvas.FillEllipse(left + (w * 0.45), top + (h * 0.6), w * 0.4, h * 0.28, fur);
        canvas.FillEllipse(left + (w * 0.85), top + (h * 0.35), w * 0.15, h * 0.25, fur);
        canvas.FillRect(left, top + (h * 0.45), w * 0.12, h * 0.1, fur);
        canvas.FillRect(left + (w * 0.15), top + (h * 0.75), w * 0.1, h * 0.25, fur);
        canvas.FillRect(left + (w * 0.65), top + (h * 0.75), w * 0.1, h * 0.25, fur);
    }
}