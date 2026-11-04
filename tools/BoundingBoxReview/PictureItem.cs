namespace BoundingBoxReview;

/// <summary>
/// A picture in the review's list: its boxes as shown, and whether the person has reviewed it.
/// </summary>
public sealed partial class PictureItem : ObservableObject
{
    [ObservableProperty]
    private string state = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnCommentChanged))]
    private string comment;

    public PictureItem(ReviewPicture picture)
    {
        Picture = picture ?? throw new ArgumentNullException(nameof(picture));
        Detector = [.. picture.Detector.Select(BoxView.Of)];
        comment = picture.Comment ?? string.Empty;
        Refresh();
    }

    /// <summary>
    /// Occurs when the person changes the comment, so the review can be saved.
    /// </summary>
    public event EventHandler? CommentChanged;

    public ReviewPicture Picture { get; }

    public string Label
        => string.Create(CultureInfo.InvariantCulture, $"{Picture.Seconds:0} s");

    public IReadOnlyList<BoxView> Detector { get; }

    public ObservableCollection<BoxView> Person { get; } = [];

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
        } + (Picture.Comment is null ? string.Empty : " · comment")
          + (Picture.Verdict is null ? string.Empty : " · " + ReviewVerdict.Symbol(Picture.Verdict));
    }

    // An empty comment is no comment: nothing new since the picture before.
    private void OnCommentChanged()
    {
        Picture.Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim();
        Refresh();
        CommentChanged?.Invoke(this, EventArgs.Empty);
    }
}