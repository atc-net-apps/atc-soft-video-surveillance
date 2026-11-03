namespace BoundingBoxReview;

/// <summary>
/// A picture in the review's list: its boxes as shown, and whether the person has reviewed it.
/// </summary>
public sealed partial class PictureItem : ObservableObject
{
    public PictureItem(ReviewPicture picture)
    {
        Picture = picture ?? throw new ArgumentNullException(nameof(picture));
        Detector = [.. picture.Detector.Select(BoxView.Of)];
        Refresh();
    }

    public ReviewPicture Picture { get; }

    public string Label
        => string.Create(CultureInfo.InvariantCulture, $"{Picture.Seconds:0} s");

    public IReadOnlyList<BoxView> Detector { get; }

    public ObservableCollection<BoxView> Person { get; } = [];

    [ObservableProperty]
    private string state = string.Empty;

    public void Refresh()
    {
        Person.Clear();
        foreach (var box in Picture.Person ?? [])
        {
            Person.Add(BoxView.Of(box));
        }

        State = Picture.Person switch
        {
            null => $"to do · {Picture.Detector.Count} yellow",
            { Count: 0 } => $"nothing · {Picture.Detector.Count} yellow",
            { } boxes => $"{boxes.Count} red · {Picture.Detector.Count} yellow",
        };
    }
}