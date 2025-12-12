using System;
using System.IO;

namespace FlashLaunch.Core.Utilities;

public static class AppDataPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlashLaunch");

    public static string GetRoot() => Root;

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static string UsageCachePath => Path.Combine(Root, "usage-apps.json");

    public static string IconCacheDirectory => Path.Combine(Root, "icon-cache");
}
