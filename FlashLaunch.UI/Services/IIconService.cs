using System.Windows.Media;
using FlashLaunch.Core.Models;

namespace FlashLaunch.UI.Services;

public interface IIconService
{
    ImageSource? GetIconForResult(SearchResult result);

    void ClearMemoryCache();
}
