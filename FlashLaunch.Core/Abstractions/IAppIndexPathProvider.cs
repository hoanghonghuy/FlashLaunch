using System.Collections.Generic;

namespace FlashLaunch.Core.Abstractions;

public interface IAppIndexPathProvider
{
    IEnumerable<string> GetAdditionalDirectories();
}
