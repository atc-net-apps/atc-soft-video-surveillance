namespace AtcSoft.VideoSurveillance.Wpf.Core.Diagnostics;

/// <summary>
/// The apps log binding errors without a debugger: dotnet test runs without one,
/// so a broken binding here proves the trace is switched on for the process.
/// </summary>
public sealed class BindingErrorLoggingTests
{
    [Fact]
    public void A_Broken_Binding_Is_Written_Without_A_Debugger()
    {
        System.Diagnostics.Debugger.IsAttached.Should().BeFalse("the point is the run without a debugger");
        var written = new System.Collections.Concurrent.ConcurrentQueue<string>();

        var error = WpfTestThread.Run(() =>
        {
            BindingErrorLogging.Start(written.Enqueue, showMessageBox: false);

            var text = new System.Windows.Controls.TextBlock { DataContext = new object() };
            _ = text.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("NoSuchProperty"));
            text.Measure(new System.Windows.Size(100, 100));
        });

        error.Should().BeNull();
        written.Should().Contain(m => m.Contains("NoSuchProperty", StringComparison.Ordinal));
    }
}