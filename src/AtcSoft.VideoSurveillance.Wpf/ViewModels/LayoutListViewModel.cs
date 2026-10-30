namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// View model for the layout list view with full CRUD and apply operations.
/// </summary>
public partial class LayoutListViewModel : ViewModelBase
{
    private readonly GatewayService gatewayService;

    [ObservableProperty]
    private ObservableCollection<LayoutItemViewModel> layouts = [];

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutListViewModel"/> class.
    /// </summary>
    public LayoutListViewModel(GatewayService gatewayService)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);

        this.gatewayService = gatewayService;
    }

    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var result = await gatewayService
                .GetLayoutsAsync()
                .ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Layouts.Clear();

                if (result is not null)
                {
                    foreach (var layout in result)
                    {
                        Layouts.Add(LayoutItemViewModel.FromLayout(layout));
                    }
                }
            });
        }
        catch (HttpRequestException)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => Layouts.Clear());
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() => IsLoading = false);
        }
    }

    [RelayCommand("AddLayout")]
    private async Task AddLayoutAsync()
    {
        var viewModel = new LayoutEditDialogViewModel();
        var dialog = new LayoutEditDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await gatewayService
                .CreateLayoutAsync(viewModel.BuildCreateRequest())
                .ConfigureAwait(false);

            await LoadAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                UserDialog.ShowError(string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.FailedToCreateLayout1,
                    ex.Message)));
        }
    }

    [RelayCommand("EditLayout")]
    private async Task EditLayoutAsync(LayoutItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // Build a Layout object from the item to pass to the dialog VM
        var layout = new Layout(
            Id: item.Id,
            Name: item.Name,
            Rows: item.Rows,
            Columns: item.Columns,
            Cameras: null!);

        var result = false;
        LayoutEditDialogViewModel? viewModel = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            viewModel = new LayoutEditDialogViewModel(layout);
            var dialog = new LayoutEditDialog(viewModel)
            {
                Owner = Application.Current.MainWindow,
            };

            result = dialog.ShowDialog() == true;
        });

        if (!result || viewModel?.LayoutId is null)
        {
            return;
        }

        try
        {
            await gatewayService
                .UpdateLayoutAsync(viewModel.LayoutId.Value, viewModel.BuildUpdateRequest())
                .ConfigureAwait(false);

            await LoadAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                UserDialog.ShowError(string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.FailedToUpdateLayout1,
                    ex.Message)));
        }
    }

    [RelayCommand("DeleteLayout")]
    private async Task DeleteLayoutAsync(LayoutItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (!UserDialog.Confirm(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.ConfirmDeleteLayout1,
                    item.Name),
                Translations.DeleteLayout))
        {
            return;
        }

        try
        {
            await gatewayService
                .DeleteLayoutAsync(item.Id)
                .ConfigureAwait(false);

            await LoadAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                UserDialog.ShowError(string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.FailedToDeleteLayout1,
                    ex.Message)));
        }
    }

    [RelayCommand("ApplyLayout")]
    private async Task ApplyLayoutAsync(LayoutItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await gatewayService
                .ApplyLayoutAsync(item.Id)
                .ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
                UserDialog.ShowInfo(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Translations.LayoutAppliedSuccessfully1,
                        item.Name),
                    Translations.LayoutApplied));
        }
        catch (HttpRequestException ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                UserDialog.ShowError(string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.FailedToApplyLayout1,
                    ex.Message)));
        }
    }
}