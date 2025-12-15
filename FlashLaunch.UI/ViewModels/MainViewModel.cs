using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Models;
using FlashLaunch.Core.Services;
using FlashLaunch.Core.Utilities;
using FlashLaunch.UI.Configuration;
using FlashLaunch.UI.Services;
using FlashLaunch.UI.Localization;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly IIconService _iconService;
    private readonly AppConfig _config;
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(120);
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    private CancellationTokenSource? _searchCts;
    private string _queryText = string.Empty;
    private SearchResultViewModel? _selectedResult;
    private bool _isLoading;
    private string? _statusMessage;
    private CancellationTokenSource? _statusCts;

    public MainViewModel(
        IQueryDispatcher queryDispatcher,
        ILogger<MainViewModel> logger,
        IConfirmDialogService confirmDialogService,
        IClipboardService clipboardService,
        IIconService iconService,
        AppConfig config)
    {
        _queryDispatcher = queryDispatcher ?? throw new ArgumentNullException(nameof(queryDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confirmDialogService = confirmDialogService ?? throw new ArgumentNullException(nameof(confirmDialogService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Results = new ObservableCollection<SearchResultViewModel>();

        ScheduleSearch();
    }

    public ObservableCollection<SearchResultViewModel> Results { get; } = new();

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetProperty(ref _queryText, value))
            {
                ScheduleSearch();
            }
        }
    }

    public SearchResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string HotkeyHint => string.IsNullOrWhiteSpace(_config.Hotkey)
        ? "Alt + Space"
        : _config.Hotkey.Trim();

    public bool ShowResultIcons => _config.ShowResultIcons;

    public bool ShowPluginBadges => _config.ShowPluginBadges;

    public void RefreshResults() => ScheduleSearch(immediate: true);

    public void ClearResults()
    {
        var previous = Interlocked.Exchange(ref _searchCts, null);
        previous?.Cancel();
        previous?.Dispose();

        Results.Clear();
        SelectedResult = null;
        IsLoading = false;
    }

    public void NotifySettingsApplied()
    {
        var text = LocalizationManager.GetString("Status_SettingsApplied");
        ShowTransientMessage(text);
        OnPropertyChanged(nameof(HotkeyHint));
        OnPropertyChanged(nameof(ShowResultIcons));
        OnPropertyChanged(nameof(ShowPluginBadges));
    }

    public void NotifyStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ShowTransientMessage(message);
    }

    private void ScheduleSearch(bool immediate = false)
    {
        var newCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _searchCts, newCts);
        previous?.Cancel();
        previous?.Dispose();

        _ = SearchAsync(newCts.Token, immediate);
    }

    public async Task ExecuteSelectedAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedResult;
        if (selected is null)
        {
            return;
        }

        if (selected.Result.Plugin.Kind == PluginKind.System)
        {
            var title = LocalizationManager.GetString("Confirm_SystemCommand_Title");
            var template = LocalizationManager.GetString("Confirm_SystemCommand_Message");
            var message = string.Format(template, selected.Title);

            var confirmed = _confirmDialogService.Show(title, message);

            if (!confirmed)
            {
                return;
            }
        }

        var stopwatch = ValueStopwatch.StartNew();
        try
        {
            await selected.Result.Plugin.ExecuteAsync(selected.Result, cancellationToken).ConfigureAwait(false);
            var elapsed = stopwatch.GetElapsedTime();
            _logger.LogInformation("Executed {Item} from plugin {Plugin} in {ElapsedMs}ms",
                selected.Title,
                selected.Result.Plugin.Name,
                elapsed.TotalMilliseconds);
            ShowStatusMessage(selected.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed for {Item} from plugin {Plugin}", selected.Title, selected.Result.Plugin.Name);
            StatusMessage = BuildErrorStatusMessage(selected.Result, ex);
        }
    }

    public void OpenSelectedLocation()
    {
        if (SelectedResult?.Result.Payload is string path && File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
            var template = LocalizationManager.GetString("Status_OpenedLocation");
            StatusMessage = string.Format(template, SelectedResult.Title);
        }
    }

    public void MoveSelectionDown()
    {
        if (Results.Count == 0)
        {
            return;
        }

        if (SelectedResult is null)
        {
            SelectedResult = Results[0];
            return;
        }

        var index = Results.IndexOf(SelectedResult);
        if (index < 0)
        {
            SelectedResult = Results[0];
        }
        else if (index < Results.Count - 1)
        {
            SelectedResult = Results[index + 1];
        }
    }

    public void MoveSelectionUp()
    {
        if (Results.Count == 0)
        {
            return;
        }

        if (SelectedResult is null)
        {
            SelectedResult = Results[Results.Count - 1];
            return;
        }

        var index = Results.IndexOf(SelectedResult);
        if (index <= 0)
        {
            SelectedResult = Results[0];
        }
        else
        {
            SelectedResult = Results[index - 1];
        }
    }

    private async Task SearchAsync(CancellationToken cancellationToken, bool immediate)
    {
        var uiContext = SynchronizationContext.Current;

        try
        {
            await _searchLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            uiContext?.Post(_ => IsLoading = true, null);

            if (!immediate && _debounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(_debounceDelay, cancellationToken).ConfigureAwait(false);
            }

            var query = QueryText;
            var dispatchWatch = ValueStopwatch.StartNew();
            var results = await _queryDispatcher.DispatchAsync(query, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Dispatcher returned {ResultCount} results for query \"{Query}\".",
                results?.Count ?? 0,
                query);
            var elapsed = dispatchWatch.GetElapsedTime();
            _logger.LogDebug("Search pipeline finished in {ElapsedMs}ms for query \"{Query}\"",
                elapsed.TotalMilliseconds,
                query);
            cancellationToken.ThrowIfCancellationRequested();

            uiContext?.Post(_ =>
            {
                UpdateResults(results ?? Array.Empty<SearchResult>());
                _logger.LogDebug("Results collection now has {ResultCount} items for query \"{Query}\".", Results.Count, query);
            }, null);
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellation.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search pipeline failed for query \"{Query}\".", QueryText);
        }
        finally
        {
            if (_searchLock.CurrentCount == 0)
            {
                _searchLock.Release();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                uiContext?.Post(_ => IsLoading = false, null);
            }
        }
    }

    private void UpdateResults(IReadOnlyCollection<SearchResult> results)
    {
        var previous = SelectedResult?.Result;

        Results.Clear();

        var enableIcons = _config.ShowResultIcons;

        foreach (var result in results)
        {
            var vm = SearchResultViewModel.FromModel(result);
            Results.Add(vm);
            vm.BeginLoadIconAsync(_iconService, enableIcons);
        }

        if (previous is not null)
        {
            var match = Results.FirstOrDefault(vm =>
                string.Equals(vm.Result.Title, previous.Title, StringComparison.OrdinalIgnoreCase) &&
                vm.Result.Plugin.Kind == previous.Plugin.Kind);

            if (match is not null)
            {
                SelectedResult = match;
                return;
            }
        }

        SelectedResult = Results.FirstOrDefault();
    }

    private string BuildErrorStatusMessage(SearchResult result, Exception ex)
    {
        var key = "Status_Error_ExecuteFailed";

        if (result.Plugin.Kind == PluginKind.Application)
        {
            if (ex is FileNotFoundException)
            {
                key = "Status_Error_AppNotFound";
            }
        }
        else if (result.Plugin.Kind == PluginKind.Web)
        {
            key = "Status_Error_WebLaunchFailed";
        }
        else if (result.Plugin.Kind == PluginKind.System)
        {
            key = "Status_Error_SystemCommandFailed";
        }

        var template = LocalizationManager.GetString(key);
        return string.Format(template, result.Title);
    }

    private void ShowStatusMessage(SearchResult result)
    {
        if (result is null)
        {
            return;
        }

        if (result.Plugin.Kind == PluginKind.Calculator &&
            result.Payload is string text &&
            !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                _clipboardService.SetText(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy calculator result to clipboard for {Item}", result.Title);
                var errorTemplate = LocalizationManager.GetString("Status_Error_ExecuteFailed");
                StatusMessage = string.Format(errorTemplate, result.Title);
                return;
            }
        }

        var key = result.Plugin.Kind switch
        {
            PluginKind.Calculator => "Status_Calculator_Copied",
            PluginKind.Web => "Status_Web_Launching",
            PluginKind.System => "Status_System_Executed",
            _ => "Status_App_Launched"
        };

        var template = LocalizationManager.GetString(key);
        var message = string.Format(template, result.Title);

        ShowTransientMessage(message);
    }

    private void ShowTransientMessage(string message)
    {
        StatusMessage = message;

        var newCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _statusCts, newCts);
        previous?.Cancel();
        previous?.Dispose();

        _ = ClearStatusLaterAsync(newCts.Token);
    }

    private async Task ClearStatusLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        StatusMessage = null;
    }
}
