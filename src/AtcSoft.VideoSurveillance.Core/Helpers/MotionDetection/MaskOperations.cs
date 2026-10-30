namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// Picture and mask operations of the detector: a light blur, and morphology on the foreground
/// mask (open removes specks, close fills holes). Separable 3×3 passes over flat arrays; the
/// overloads with buffers allocate nothing. The inner parts run on <see cref="Vector{T}"/> and
/// give exactly the bytes of the plain loops kept for the edges (integer arithmetic only).
/// </summary>
internal static class MaskOperations
{
    // (sum + 4) / 9 as a multiply and a shift: exact for every sum below 74 898, and a 3×3 sum is at most 2 295.
    internal const uint NinthMultiplier = 58_255;
    internal const int NinthShift = 19;

    /// <summary>
    /// Blurs a grey picture with a 3×3 box, which removes sensor noise and compression blocks.
    /// </summary>
    public static byte[] Blur3(
        byte[] source,
        int width,
        int height)
    {
        var result = new byte[source.Length];
        Blur3(source, result, new ushort[source.Length], width, height);
        return result;
    }

    /// <summary>
    /// Blurs a grey picture with a 3×3 box into <paramref name="result"/>.
    /// </summary>
    public static void Blur3(
        byte[] source,
        byte[] result,
        ushort[] rows,
        int width,
        int height)
    {
        var bytes = Vector<byte>.Count;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var x = 0;
            if (width >= bytes + 2)
            {
                BlurRowEdge(source, rows, row, width, 0);
                var span = source.AsSpan(row, width);
                var sums = rows.AsSpan(row, width);
                for (x = 1; x + bytes < width; x += bytes)
                {
                    Vector.Widen(new Vector<byte>(span[(x - 1)..]), out var leftLow, out var leftHigh);
                    Vector.Widen(new Vector<byte>(span[x..]), out var midLow, out var midHigh);
                    Vector.Widen(new Vector<byte>(span[(x + 1)..]), out var rightLow, out var rightHigh);
                    (leftLow + midLow + rightLow).CopyTo(sums[x..]);
                    (leftHigh + midHigh + rightHigh).CopyTo(sums[(x + Vector<ushort>.Count)..]);
                }
            }

            for (; x < width; x++)
            {
                BlurRowEdge(source, rows, row, width, x);
            }
        }

        var four = new Vector<ushort>(4);
        var multiplier = new Vector<uint>(NinthMultiplier);
        for (var y = 0; y < height; y++)
        {
            var up = rows.AsSpan(Math.Max(0, y - 1) * width, width);
            var mid = rows.AsSpan(y * width, width);
            var down = rows.AsSpan(Math.Min(height - 1, y + 1) * width, width);
            var target = result.AsSpan(y * width, width);
            var x = 0;
            for (; x + bytes <= width; x += bytes)
            {
                var low = Ninth(new Vector<ushort>(up[x..]) + new Vector<ushort>(mid[x..]) + new Vector<ushort>(down[x..]) + four, multiplier);
                var high = Ninth(
                    new Vector<ushort>(up[(x + Vector<ushort>.Count)..]) + new Vector<ushort>(mid[(x + Vector<ushort>.Count)..]) + new Vector<ushort>(down[(x + Vector<ushort>.Count)..]) + four,
                    multiplier);
                Vector.Narrow(low, high).CopyTo(target[x..]);
            }

            for (; x < width; x++)
            {
                target[x] = (byte)((up[x] + mid[x] + down[x] + 4) / 9);
            }
        }
    }

    private static void BlurRowEdge(
        byte[] source,
        ushort[] rows,
        int row,
        int width,
        int x)
    {
        var left = source[row + Math.Max(0, x - 1)];
        var right = source[row + Math.Min(width - 1, x + 1)];
        rows[row + x] = (ushort)(left + source[row + x] + right);
    }

    // Divides sums (with the 4 for rounding added) by 9; the results fit a byte.
    private static Vector<ushort> Ninth(
        Vector<ushort> sums,
        Vector<uint> multiplier)
    {
        Vector.Widen(sums, out var low, out var high);
        return Vector.Narrow(
            Vector.ShiftRightLogical(low * multiplier, NinthShift),
            Vector.ShiftRightLogical(high * multiplier, NinthShift));
    }

    /// <summary>
    /// Removes specks smaller than the structuring element (erode, then dilate).
    /// </summary>
    public static void Open(
        byte[] mask,
        int width,
        int height,
        int radius,
        byte[]? temp = null)
    {
        temp ??= new byte[mask.Length];
        Apply(mask, temp, width, height, radius, keepIfAny: false);
        Apply(mask, temp, width, height, radius, keepIfAny: true);
    }

    /// <summary>
    /// Fills holes and gaps smaller than the structuring element (dilate, then erode).
    /// </summary>
    public static void Close(
        byte[] mask,
        int width,
        int height,
        int radius,
        byte[]? temp = null)
    {
        temp ??= new byte[mask.Length];
        Apply(mask, temp, width, height, radius, keepIfAny: true);
        Apply(mask, temp, width, height, radius, keepIfAny: false);
    }

    // A square element, as a horizontal then a vertical pass: max (dilate) or min (erode). The
    // window is cut at the picture's edges. The mask holds only 0 and 1, so max is "any" and min "all".
    private static void Apply(
        byte[] mask,
        byte[] temp,
        int width,
        int height,
        int radius,
        bool keepIfAny)
    {
        if (radius <= 0)
        {
            return;
        }

        var bytes = Vector<byte>.Count;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var x = 0;
            if (width >= bytes + (2 * radius))
            {
                // The edges, where the window is cut, by the plain loop; the inside a vector at a time.
                for (; x < radius; x++)
                {
                    temp[row + x] = Reduce(mask, row, Math.Max(0, x - radius), Math.Min(width - 1, x + radius), keepIfAny);
                }

                var source = mask.AsSpan(row, width);
                var target = temp.AsSpan(row, width);
                for (; x + radius + bytes <= width; x += bytes)
                {
                    var value = new Vector<byte>(source[(x - radius)..]);
                    for (var k = 1 - radius; k <= radius; k++)
                    {
                        var next = new Vector<byte>(source[(x + k)..]);
                        value = keepIfAny ? Vector.Max(value, next) : Vector.Min(value, next);
                    }

                    value.CopyTo(target[x..]);
                }
            }

            for (; x < width; x++)
            {
                temp[row + x] = Reduce(mask, row, Math.Max(0, x - radius), Math.Min(width - 1, x + radius), keepIfAny);
            }
        }

        for (var y = 0; y < height; y++)
        {
            var y0 = Math.Max(0, y - radius);
            var y1 = Math.Min(height - 1, y + radius);
            var target = mask.AsSpan(y * width, width);
            var x = 0;
            for (; x + bytes <= width; x += bytes)
            {
                var value = new Vector<byte>(temp.AsSpan((y0 * width) + x, bytes));
                for (var yy = y0 + 1; yy <= y1; yy++)
                {
                    var next = new Vector<byte>(temp.AsSpan((yy * width) + x, bytes));
                    value = keepIfAny ? Vector.Max(value, next) : Vector.Min(value, next);
                }

                value.CopyTo(target[x..]);
            }

            for (; x < width; x++)
            {
                var value = keepIfAny ? (byte)0 : (byte)1;
                for (var yy = y0; yy <= y1; yy++)
                {
                    var v = temp[(yy * width) + x];
                    if (keepIfAny && v != 0)
                    {
                        value = 1;
                        break;
                    }

                    if (!keepIfAny && v == 0)
                    {
                        value = 0;
                        break;
                    }
                }

                mask[(y * width) + x] = value;
            }
        }
    }

    private static byte Reduce(
        byte[] mask,
        int row,
        int x0,
        int x1,
        bool keepIfAny)
    {
        for (var x = x0; x <= x1; x++)
        {
            var v = mask[row + x];
            if (keepIfAny && v != 0)
            {
                return 1;
            }

            if (!keepIfAny && v == 0)
            {
                return 0;
            }
        }

        return keepIfAny ? (byte)0 : (byte)1;
    }
}