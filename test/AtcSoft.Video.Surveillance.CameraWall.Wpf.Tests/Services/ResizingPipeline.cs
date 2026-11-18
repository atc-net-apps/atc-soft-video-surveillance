namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A 16:9 picture that turns 4:3 as soon as it has been captured, and back with the next capture.
/// </summary>
public class ResizingPipeline : DispatchProxy
{
    private bool captured;

    public static IMediaPipeline New()
        => Create<IMediaPipeline, ResizingPipeline>();

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case "get_DisplaySize":
                return captured ? (640, 480) : (1920, 1080);
            case nameof(IMediaPipeline.CaptureGreyAsync):
                ArgumentNullException.ThrowIfNull(args);
                captured = !captured;
                var width = (int)args[0]!;
                var height = (int)args[1]!;
                return Task.FromResult<byte[]?>(new byte[width * height]);
            default:
                return targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void)
                    ? Activator.CreateInstance(type)
                    : null;
        }
    }
}