namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A storybook that cannot be read or makes no sense; the message names where (for example
/// <c>objects[1].path[0].t</c>).
/// </summary>
public sealed class StorybookException : Exception
{
    public StorybookException()
    {
    }

    public StorybookException(string message)
        : base(message)
    {
    }

    public StorybookException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}