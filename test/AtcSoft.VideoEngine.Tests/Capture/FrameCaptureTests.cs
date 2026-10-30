namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// A snapshot is the picture as it is displayed: a frame with a white top-left corner comes
/// back with that corner where the rotation puts it (the roadmap's "marked corner" test).
/// Needs the FFmpeg shared libraries; skipped where they are not installed.
/// </summary>
public sealed unsafe class FrameCaptureTests
{
    private const int FrameWidth = 8;
    private const int FrameHeight = 4;

    [Theory]
    [InlineData(VideoRotation.None, FrameWidth, FrameHeight, 0, 0)]
    [InlineData(VideoRotation.Rotate90, FrameHeight, FrameWidth, FrameHeight - 1, 0)]
    [InlineData(VideoRotation.Rotate180, FrameWidth, FrameHeight, FrameWidth - 1, FrameHeight - 1)]
    [InlineData(VideoRotation.Rotate270, FrameHeight, FrameWidth, 0, FrameWidth - 1)]
    public void The_Marked_Corner_Ends_Up_Where_The_Rotation_Puts_It(
        VideoRotation rotation,
        int expectedWidth,
        int expectedHeight,
        int cornerX,
        int cornerY)
    {
        TestEngine.Require();
        var frame = MarkedFrame();
        byte[]? png;
        try
        {
            using var capture = new FrameCapture();
            png = capture.CaptureFrame(frame, rotation);
        }
        finally
        {
            av_frame_free(ref frame);
        }

        png.Should().NotBeNull();
        var picture = TestPng.Read(png!);
        picture.Width.Should().Be(expectedWidth);
        picture.Height.Should().Be(expectedHeight);
        picture.IsWhite(cornerX, cornerY).Should().BeTrue($"the white corner is at ({cornerX}, {cornerY}) after {rotation}");
        picture.WhiteCount().Should().Be(1);
    }

    private static AVFrame* MarkedFrame()
    {
        var frame = av_frame_alloc();
        frame->format = (int)AVPixelFormat.Rgb24;
        frame->width = FrameWidth;
        frame->height = FrameHeight;
        av_frame_get_buffer(frame, 0).Should().Be(0);

        new Span<byte>((byte*)frame->data[0], frame->linesize[0] * FrameHeight).Clear();
        var topLeft = (byte*)frame->data[0];
        topLeft[0] = 255;
        topLeft[1] = 255;
        topLeft[2] = 255;
        return frame;
    }

    /// <summary>
    /// Reads an 8-bit RGB PNG (what <see cref="FrameCapture"/> writes), filters included.
    /// </summary>
    private sealed class TestPng
    {
        private readonly byte[] pixels;

        private TestPng(
            int width,
            int height,
            byte[] pixels)
        {
            Width = width;
            Height = height;
            this.pixels = pixels;
        }

        public int Width { get; }

        public int Height { get; }

        public static TestPng Read(byte[] png)
        {
            var width = 0;
            var height = 0;
            using var idat = new MemoryStream();
            var position = 8;
            while (position < png.Length)
            {
                var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
                var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
                var data = png.AsSpan(position + 8, length);
                if (type == "IHDR")
                {
                    width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data);
                    height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    data[8].Should().Be(8, "8 bits per sample");
                    data[9].Should().Be(2, "RGB");
                }
                else if (type == "IDAT")
                {
                    idat.Write(data);
                }

                position += 12 + length;
            }

            idat.Position = 0;
            using var inflater = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
            using var raw = new MemoryStream();
            inflater.CopyTo(raw);
            return new TestPng(width, height, Unfilter(raw.ToArray(), width, height));
        }

        public bool IsWhite(
            int x,
            int y)
        {
            var i = ((y * Width) + x) * 3;
            return pixels[i] > 200 && pixels[i + 1] > 200 && pixels[i + 2] > 200;
        }

        public int WhiteCount()
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    count += IsWhite(x, y) ? 1 : 0;
                }
            }

            return count;
        }

        private static byte[] Unfilter(
            byte[] raw,
            int width,
            int height)
        {
            const int Bpp = 3;
            var stride = width * Bpp;
            var output = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];
                for (var x = 0; x < stride; x++)
                {
                    var value = raw[(y * (stride + 1)) + 1 + x];
                    var left = x >= Bpp ? output[(y * stride) + x - Bpp] : 0;
                    var up = y > 0 ? output[((y - 1) * stride) + x] : 0;
                    var upLeft = x >= Bpp && y > 0 ? output[((y - 1) * stride) + x - Bpp] : 0;
                    output[(y * stride) + x] = (byte)(value + filter switch
                    {
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Paeth(left, up, upLeft),
                        _ => 0,
                    });
                }
            }

            return output;
        }

        private static int Paeth(
            int a,
            int b,
            int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}