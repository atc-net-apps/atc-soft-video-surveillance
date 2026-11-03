namespace BoundingBoxReview;

/// <summary>
/// The review: make the pictures of a recording, draw the boxes where they should be, compare.
/// Every change to a picture's boxes is saved at once (<c>boxes.json</c> and the picture).
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    // A drag shorter than this is a click, not a box.
    private const double MinBoxSide = 4;

    private string? folder;
    private ReviewFile? review;
    private Point? drawStart;

    [ObservableProperty]
    private string title = "Bounding box review";

    [ObservableProperty]
    private string status = "Open a recording to make its pictures, or a review folder to draw boxes on them.";

    [ObservableProperty]
    private double everySeconds = 10;

    [ObservableProperty]
    private string sensitivityText = string.Empty;

    [ObservableProperty]
    private bool showDetector = true;

    [ObservableProperty]
    private ImageSource? image;

    [ObservableProperty]
    private int pictureWidth = 1;

    [ObservableProperty]
    private int pictureHeight = 1;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedChanged))]
    private PictureItem? selected;

    [ObservableProperty]
    private IReadOnlyList<BoxView> drawing = [];

    [ObservableProperty]
    private string compareText = string.Empty;

    public ObservableCollection<PictureItem> Pictures { get; } = [];

    public bool HasReview
        => review is not null;

    /// <summary>
    /// Opens a recording (makes its review folder first, unless it has one) or a review folder.
    /// </summary>
    public async Task OpenAsync(string path)
    {
        if (Directory.Exists(path))
        {
            OpenFolder(path);
        }
        else if (File.Exists(path))
        {
            await MakeAsync(path).ConfigureAwait(true);
        }
    }

    public void BeginDraw(Point at)
    {
        if (Selected is null)
        {
            return;
        }

        drawStart = at;
        Drawing = [BoxView.Between(at, at)];
    }

    public void MoveDraw(Point at)
    {
        if (drawStart is { } start)
        {
            Drawing = [BoxView.Between(start, at)];
        }
    }

    public void EndDraw(Point at)
    {
        if (drawStart is not { } start || Selected is not { } item || review is null)
        {
            return;
        }

        drawStart = null;
        Drawing = [];
        var box = BoxView.Between(start, at);
        if (box.Width < MinBoxSide || box.Height < MinBoxSide)
        {
            return;
        }

        var person = item.Picture.Person ?? [];
        person.Add(ReviewBox.Within(box.X, box.Y, box.Width, box.Height, review.Width, review.Height));
        item.Picture.Person = person;
        Save(item);
    }

    public void Remove(BoxView box)
    {
        if (Selected is not { } item || item.Picture.Person is not { } person)
        {
            return;
        }

        var index = item.Person.IndexOf(box);
        if (index >= 0)
        {
            person.RemoveAt(index);
            Save(item);
        }
    }

    [RelayCommand("OpenRecording", CanExecute = nameof(CanOpen))]
    private async Task OpenRecordingFromDialogAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Recording (*.mkv;*.mp4)|*.mkv;*.mp4|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            await MakeAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    [RelayCommand("OpenFolder", CanExecute = nameof(CanOpen))]
    private void OpenFolderFromDialog()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Review folder (with boxes.json)" };
        if (dialog.ShowDialog() == true)
        {
            OpenFolder(dialog.FolderName);
        }
    }

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void Previous()
        => Step(-1);

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void Next()
        => Step(1);

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void NothingHere()
    {
        if (Selected is { } item)
        {
            item.Picture.Person = [];
            Save(item);
            Step(1);
        }
    }

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void UndoLast()
    {
        if (Selected is { Picture.Person: { Count: > 0 } person } item)
        {
            person.RemoveAt(person.Count - 1);
            Save(item);
        }
    }

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void Reopen()
    {
        if (Selected is { } item)
        {
            item.Picture.Person = null;
            Save(item);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRerun))]
    private async Task RerunAsync()
    {
        if (folder is null || !EnsureEngine())
        {
            return;
        }

        var path = folder;
        IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var progress = new Progress<string>(text => Status = text);
            await Task.Run(() => ReviewMaker.Rerun(path, progress)).ConfigureAwait(true);
            var at = Selected is null ? 0 : Pictures.IndexOf(Selected);
            OpenFolder(path);
            Selected = Pictures.Count == 0 ? null : Pictures[Math.Clamp(at, 0, Pictures.Count - 1)];
            Compare();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or Atc.Video.Engine.FFmpeg.FFmpegException)
        {
            Status = $"Could not run the detector again: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool CanRerun()
        => HasReview && !IsBusy;

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void Compare()
    {
        if (folder is null)
        {
            return;
        }

        var summary = ReviewComparer.Compare(folder);
        CompareText = string.Join(Environment.NewLine, summary);
        Status = $"Compared: {Path.Combine(folder, ReviewComparer.ReportName)}";
    }

    [RelayCommand(CanExecute = nameof(HasReview))]
    private void OpenInExplorer()
    {
        if (folder is not null)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{folder}\"") { UseShellExecute = true });
        }
    }

    private bool CanOpen()
        => !IsBusy;

    private async Task MakeAsync(string video)
    {
        var existing = Path.Combine(Path.GetDirectoryName(video)!, Path.GetFileNameWithoutExtension(video) + "-review");
        if (File.Exists(Path.Combine(existing, ReviewFile.FileName)))
        {
            OpenFolder(existing);
            Status = "This recording already has a review: opened it. Delete its -review folder to make new pictures.";
            return;
        }

        if (!EnsureEngine())
        {
            Status = "FFmpeg 9 is needed to read the recording.";
            return;
        }

        var settingsPath = ReviewMaker.FindSettings(video);
        var settings = ReviewMaker.LoadSettings(settingsPath);
        if (int.TryParse(SensitivityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sensitivity))
        {
            settings.Sensitivity = sensitivity;
        }

        IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            Status = settingsPath is null ? "No settings.json found beside the recording: the default motion settings are used." : $"Motion settings from {settingsPath}";
            var progress = new Progress<string>(text => Status = text);
            var every = Math.Max(1, EverySeconds);
            var made = await Task.Run(() => ReviewMaker.Make(video, settings, every, progress)).ConfigureAwait(true);
            OpenFolder(made);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or Atc.Video.Engine.FFmpeg.FFmpegException)
        {
            Status = $"Could not make the pictures: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            review = ReviewFile.Read(path);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Status = $"Not a review folder ({ReviewFile.FileName} missing or unreadable): {ex.Message}";
            return;
        }

        folder = path;
        PictureWidth = review.Width;
        PictureHeight = review.Height;
        foreach (var old in Pictures)
        {
            old.CommentChanged -= OnCommentChanged;
        }

        Pictures.Clear();
        foreach (var picture in review.Pictures)
        {
            var item = new PictureItem(picture);
            item.CommentChanged += OnCommentChanged;
            Pictures.Add(item);
        }

        Title = $"Bounding box review: {Path.GetFileName(review.Recording)}";
        Status = "Drag on the picture to draw where a box should be (red). Right-click a red box to remove it. Arrow keys: previous / next. Ctrl+N: nothing to box here. Write what you see in the comment; leave it empty when nothing changed since the picture before.";
        CompareText = string.Empty;
        OnPropertyChanged(nameof(HasReview));
        Selected = Pictures.FirstOrDefault(p => p.Picture.Person is null) ?? Pictures.FirstOrDefault();
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnSelectedChanged()
    {
        drawStart = null;
        Drawing = [];
        if (Selected is not { } item || folder is null)
        {
            Image = null;
            return;
        }

        // The original, without boxes: the boxes are drawn over it, so they can be shown and hidden.
        var (pixels, width, height) = PicturePixels.Load(Path.Combine(folder, ReviewFile.OriginalFolder, item.Picture.File));
        Image = PicturePixels.ToImage(pixels, width, height);
    }

    // A comment is only text in boxes.json: the picture need not be drawn again.
    private void OnCommentChanged(
        object? sender,
        EventArgs e)
    {
        if (folder is not null && review is not null)
        {
            review.Write(folder);
        }
    }

    private void Step(int by)
    {
        if (Pictures.Count == 0)
        {
            return;
        }

        var index = Selected is null ? 0 : Pictures.IndexOf(Selected) + by;
        Selected = Pictures[Math.Clamp(index, 0, Pictures.Count - 1)];
    }

    private void Save(PictureItem item)
    {
        if (folder is null || review is null)
        {
            return;
        }

        review.Write(folder);
        ReviewMaker.Render(folder, review, item.Picture);
        item.Refresh();
    }

    private static bool EnsureEngine()
    {
        if (VideoEngineBootstrap.IsInitialized)
        {
            return true;
        }

        if (FFmpegSetup.EnsureReady() is not { } ffmpeg)
        {
            return false;
        }

        VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = ffmpeg });
        return true;
    }
}