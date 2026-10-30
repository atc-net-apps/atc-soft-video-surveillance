namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// A pipeline that is only passed along: every member returns its default.
/// </summary>
public class NullPipeline : DispatchProxy
{
    public static IMediaPipeline New()
        => Create<IMediaPipeline, NullPipeline>();

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
        => targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void)
            ? Activator.CreateInstance(type)
            : null;
}