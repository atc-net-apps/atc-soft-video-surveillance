namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// A camera's clock burnt into the picture: <c>HH:MM:SS</c> in seven-segment digits that change
/// every second, from the clock's <c>startsAt</c> (12:00:00 unless set). It must never get a box.
/// </summary>
internal static class ClockShape
{
    // Segments a-g of the digits 0-9, as bits (a = 1).
    private static readonly int[] Segments = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F];

    public static PixelBox Draw(
        Canvas canvas,
        StorybookDistractor clock,
        double time)
    {
        var boxW = canvas.Width * 0.2;
        var boxH = canvas.Height * 0.05;
        var marginX = canvas.Width * 0.02;
        var marginY = canvas.Height * 0.02;
        var left = clock.Corner.EndsWith("Left", StringComparison.Ordinal) ? marginX : canvas.Width - marginX - boxW;
        var top = clock.Corner.StartsWith("top", StringComparison.Ordinal) ? marginY : canvas.Height - marginY - boxH;

        canvas.FillRect(left, top, boxW, boxH, (_, _) => 15);

        var start = TimeSpan.ParseExact(clock.StartsAt, @"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var seconds = (int)start.TotalSeconds + (int)Math.Floor(time);
        var text = string.Create(CultureInfo.InvariantCulture, $"{(seconds / 3600) % 24:00}:{(seconds / 60) % 60:00}:{seconds % 60:00}");
        var cellW = boxW / text.Length;
        var pad = cellW * 0.15;
        for (var i = 0; i < text.Length; i++)
        {
            var cellLeft = left + (i * cellW) + pad;
            var cellTop = top + (boxH * 0.15);
            var w = cellW - (2 * pad);
            var h = boxH * 0.7;
            if (text[i] == ':')
            {
                var dot = Math.Max(1, w * 0.25);
                canvas.FillRect(cellLeft + ((w - dot) / 2), cellTop + (h * 0.25), dot, dot, (_, _) => 235);
                canvas.FillRect(cellLeft + ((w - dot) / 2), cellTop + (h * 0.65), dot, dot, (_, _) => 235);
                continue;
            }

            DrawDigit(canvas, text[i] - '0', cellLeft, cellTop, w, h);
        }

        return new PixelBox(left, top, boxW, boxH);
    }

    private static void DrawDigit(
        Canvas canvas,
        int digit,
        double left,
        double top,
        double w,
        double h)
    {
        var t = Math.Max(1, h * 0.12);
        var half = h / 2;
        var bits = Segments[digit];
        var light = (Func<int, int, int>)((_, _) => 235);
        if ((bits & 0x01) != 0)
        {
            canvas.FillRect(left, top, w, t, light); // a
        }

        if ((bits & 0x02) != 0)
        {
            canvas.FillRect(left + w - t, top, t, half, light); // b
        }

        if ((bits & 0x04) != 0)
        {
            canvas.FillRect(left + w - t, top + half, t, half, light); // c
        }

        if ((bits & 0x08) != 0)
        {
            canvas.FillRect(left, top + h - t, w, t, light); // d
        }

        if ((bits & 0x10) != 0)
        {
            canvas.FillRect(left, top + half, t, half, light); // e
        }

        if ((bits & 0x20) != 0)
        {
            canvas.FillRect(left, top, t, half, light); // f
        }

        if ((bits & 0x40) != 0)
        {
            canvas.FillRect(left, top + half - (t / 2), w, t, light); // g
        }
    }
}