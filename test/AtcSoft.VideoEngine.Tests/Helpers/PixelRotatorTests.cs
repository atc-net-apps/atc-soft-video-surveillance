namespace AtcSoft.VideoEngine.Helpers;

public sealed unsafe class PixelRotatorTests
{
    // A 3x2 picture, one byte per pixel:
    //   1 2 3
    //   4 5 6
    private static readonly byte[] Source = [1, 2, 3, 4, 5, 6];

    [Theory]
    [InlineData(VideoRotation.None, 3, 2)]
    [InlineData(VideoRotation.Rotate180, 3, 2)]
    [InlineData(VideoRotation.Rotate90, 2, 3)]
    [InlineData(VideoRotation.Rotate270, 2, 3)]
    public void GetRotatedSize_Swaps_Dimensions_Only_For_Quarter_Turns(
        VideoRotation rotation,
        int expectedWidth,
        int expectedHeight)
    {
        PixelRotator.GetRotatedSize(3, 2, rotation).Should().Be((expectedWidth, expectedHeight));
    }

    [Fact]
    public void Rotate_None_Copies_The_Picture()
    {
        Rotate(VideoRotation.None).Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Fact]
    public void Rotate_90_Turns_The_Picture_Clockwise()
    {
        // 4 1
        // 5 2
        // 6 3
        Rotate(VideoRotation.Rotate90).Should().Equal(4, 1, 5, 2, 6, 3);
    }

    [Fact]
    public void Rotate_180_Turns_The_Picture_Upside_Down()
    {
        // 6 5 4
        // 3 2 1
        Rotate(VideoRotation.Rotate180).Should().Equal(6, 5, 4, 3, 2, 1);
    }

    [Fact]
    public void Rotate_270_Turns_The_Picture_Counter_Clockwise()
    {
        // 3 6
        // 2 5
        // 1 4
        Rotate(VideoRotation.Rotate270).Should().Equal(3, 6, 2, 5, 1, 4);
    }

    [Fact]
    public void Rotate_Keeps_Multi_Byte_Pixels_Together_And_Skips_Row_Padding()
    {
        // 2x1 picture, 4 bytes per pixel, source rows padded to 12 bytes.
        byte[] source = [1, 1, 1, 1, 2, 2, 2, 2, 99, 99, 99, 99];
        var destination = new byte[16];

        fixed (byte* src = source)
        {
            fixed (byte* dst = destination)
            {
                // Rotated 90 degrees it is 1x2, destination rows padded to 8 bytes.
                PixelRotator.Rotate(src, 12, 2, 1, 4, VideoRotation.Rotate90, dst, 8);
            }
        }

        destination.Should().Equal(1, 1, 1, 1, 0, 0, 0, 0, 2, 2, 2, 2, 0, 0, 0, 0);
    }

    private static byte[] Rotate(VideoRotation rotation)
    {
        var (width, height) = PixelRotator.GetRotatedSize(3, 2, rotation);
        var destination = new byte[width * height];

        fixed (byte* src = Source)
        {
            fixed (byte* dst = destination)
            {
                PixelRotator.Rotate(src, 3, 3, 2, 1, rotation, dst, width);
            }
        }

        return destination;
    }
}