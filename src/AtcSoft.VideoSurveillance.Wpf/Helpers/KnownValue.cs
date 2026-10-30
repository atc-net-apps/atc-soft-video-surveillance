namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

/// <summary>
/// Picks a listed value of a generated API enum by name.
/// </summary>
public static class KnownValue
{
    /// <summary>
    /// Gets the listed value with the name (<c>BottomRight</c> or <c>bottomRight</c>),
    /// or the fallback. A name the specification does not list is never sent: the
    /// server would refuse it.
    /// </summary>
    /// <typeparam name="T">A generated API enum.</typeparam>
    public static T FromName<T>(
        IReadOnlyList<T> knownValues,
        string? name,
        T fallback)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(knownValues);

        foreach (var value in knownValues)
        {
            // An API enum's ToString() is its value on the wire.
            if (string.Equals(value.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return fallback;
    }
}