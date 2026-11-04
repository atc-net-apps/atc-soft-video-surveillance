namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

/// <summary>
/// Atc's dialog boxes (<c>QuestionDialogBox</c>, <c>InfoDialogBox</c>) put their text in a
/// horizontal panel inside a scroll viewer that scrolls sideways, so the text never wraps and a
/// long message has to be scrolled to be read. This wraps it at the dialog's width and makes the
/// dialog as tall as the text needs (up to most of the screen).
/// </summary>
public static class DialogBoxText
{
    /// <summary>
    /// The dialog's width a default message gets.
    /// </summary>
    public const double DefaultWidth = 520;

    /// <summary>
    /// Wraps the dialog's text once it is loaded.
    /// </summary>
    /// <typeparam name="T">The dialog's type.</typeparam>
    /// <param name="dialog">An Atc dialog box, not shown yet.</param>
    /// <returns>The same dialog.</returns>
    public static T Wrap<T>(T dialog)
        where T : Window
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.Loaded += OnLoaded;
        return dialog;
    }

    private static void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = (Window)sender;
        dialog.Loaded -= OnLoaded;

        // Sideways scrolling off, and the icon and the text in a grid, which gives the text the
        // width that is left, so it wraps.
        foreach (var viewer in Descendants(dialog).OfType<ScrollViewer>().ToList())
        {
            viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            foreach (var text in Descendants(viewer).OfType<TextBlock>().ToList())
            {
                text.TextWrapping = TextWrapping.Wrap;
                if (text.Parent is StackPanel { Orientation: Orientation.Vertical } textPanel &&
                    textPanel.Parent is StackPanel { Orientation: Orientation.Horizontal } row &&
                    row.Parent is ContentControl holder)
                {
                    holder.Content = InGrid(row, textPanel, text);
                }
            }
        }

        // Grown or shrunk about the middle it was placed at, so it stays centred where it was.
        var middle = dialog.Top + (dialog.ActualHeight / 2);
        dialog.MaxHeight = SystemParameters.WorkArea.Height * 0.8;
        dialog.SizeToContent = SizeToContent.Height;
        dialog.UpdateLayout();
        if (!double.IsNaN(middle))
        {
            dialog.Top = middle - (dialog.ActualHeight / 2);
        }
    }

    private static Grid InGrid(
        StackPanel row,
        StackPanel textPanel,
        TextBlock text)
    {
        var icons = row.Children.OfType<UIElement>().Where(c => !ReferenceEquals(c, textPanel)).ToList();
        row.Children.Clear();
        textPanel.Children.Remove(text);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var icon in icons)
        {
            grid.Children.Add(icon);
        }

        text.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static IEnumerable<DependencyObject> Descendants(
        DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}