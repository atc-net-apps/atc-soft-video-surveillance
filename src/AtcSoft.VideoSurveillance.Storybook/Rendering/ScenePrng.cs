namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// A small random generator of our own (SplitMix64), keyed by the scene's seed, the frame and a
/// purpose. Any frame can be drawn on its own, and a .NET update can never change a scene, as it
/// could with <see cref="System.Random"/>.
/// </summary>
internal struct ScenePrng
{
    private ulong state;

    public ScenePrng(
        int seed,
        int frame,
        int purpose)
    {
        state = Mix(((ulong)(uint)seed << 32) ^ ((ulong)(uint)frame * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)purpose * 0xBF58476D1CE4E5B9UL));
    }

    /// <summary>
    /// Gets the next number, 0 (inclusive) to 1 (exclusive).
    /// </summary>
    public double NextDouble()
        => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// Gets the next number, <paramref name="min"/> (inclusive) to <paramref name="max"/> (exclusive).
    /// </summary>
    public int Next(
        int min,
        int max)
        => min + (int)(NextDouble() * (max - min));

    /// <summary>
    /// Gets a number from -1 to 1 that is more often near 0 (the sum of two uniform numbers).
    /// </summary>
    public double NextTriangle()
        => NextDouble() + NextDouble() - 1;

    /// <summary>
    /// Hashes a position into 0 to 1, the same for the same input, for textures.
    /// </summary>
    public static double Hash(
        int seed,
        int x,
        int y)
        => (Mix(((ulong)(uint)seed << 32) ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL)) >> 11) * (1.0 / (1UL << 53));

    private ulong NextULong()
    {
        state += 0x9E3779B97F4A7C15UL;
        return Mix(state);
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}