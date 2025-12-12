using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using FlashLaunch.Core.Models;
using FlashLaunch.UI.Services;

namespace FlashLaunch.UI.ViewModels;

public sealed class SearchResultViewModel : ObservableObject
{
    private string _title = string.Empty;
    private string? _subtitle;
    private string _pluginBadge = string.Empty;
    private double _score;
    private ImageSource? _icon;
    private bool _hasIcon;

    private SearchResultViewModel(SearchResult result)
    {
        Result = result;
        _title = result.Title;
        _subtitle = result.Subtitle;
        _pluginBadge = result.PluginName;
        _score = result.Score;
        _icon = null;
        _hasIcon = false;
    }

    public SearchResult Result { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string? Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string PluginBadge
    {
        get => _pluginBadge;
        set => SetProperty(ref _pluginBadge, value);
    }

    public double Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool HasIcon
    {
        get => _hasIcon;
        set => SetProperty(ref _hasIcon, value);
    }

    public void BeginLoadIconAsync(IIconService iconService, bool enableIcons)
    {
        if (!enableIcons || iconService is null || _hasIcon)
        {
            return;
        }

        var uiContext = SynchronizationContext.Current;

        _ = Task.Run(() =>
        {
            var icon = iconService.GetIconForResult(Result);
            if (icon is null)
            {
                return;
            }

            if (uiContext is not null)
            {
                uiContext.Post(_ =>
                {
                    Icon = icon;
                    HasIcon = true;
                }, null);
            }
            else
            {
                Icon = icon;
                HasIcon = true;
            }
        });
    }

    public static SearchResultViewModel FromModel(SearchResult result) => new(result);
}
