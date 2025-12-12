using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using FlashLaunch.Core.Models;
using FlashLaunch.Core.Utilities;
using FlashLaunch.UI.Configuration;

namespace FlashLaunch.UI.Services;

public sealed class ShellIconService : IIconService
{
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppConfig _config;
    private readonly string _iconCacheDirectory;

    public ShellIconService(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _iconCacheDirectory = AppDataPaths.IconCacheDirectory;
    }

    public void ClearMemoryCache() => _cache.Clear();

    public ImageSource? GetIconForResult(SearchResult result)
    {
        if (result is null)
        {
            return null;
        }

        if (result.Plugin.Kind != PluginKind.Application)
        {
            return GetPluginIcon(result.Plugin.Kind);
        }

        var keyPath = GetKeyPath(result);
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return null;
        }

        var usePersistentCache = _config.PersistentIconCacheEnabled;

        // Ưu tiên cache trong RAM nếu đã có.
        if (_cache.TryGetValue(keyPath, out var cached) && cached is not null)
        {
            return cached;
        }

        ImageSource? icon = null;

        // Nếu bật cache bền, thử đọc icon từ đĩa trước.
        if (usePersistentCache)
        {
            icon = TryLoadFromDiskCache(keyPath);
            if (icon is not null)
            {
                _cache[keyPath] = icon;
                return icon;
            }
        }

        // Không có trong cache đĩa, load icon từ shell/file như bình thường.
        icon = LoadIcon(keyPath);
        if (icon is null)
        {
            return null;
        }

        _cache[keyPath] = icon;

        if (usePersistentCache)
        {
            SaveToDiskCache(keyPath, icon);
        }

        return icon;
    }

    private ImageSource? TryLoadFromDiskCache(string keyPath)
    {
        try
        {
            var filePath = GetCacheFilePath(keyPath);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void SaveToDiskCache(string keyPath, ImageSource icon)
    {
        if (icon is not BitmapSource bitmap)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_iconCacheDirectory);
            var filePath = GetCacheFilePath(keyPath);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }
        catch
        {
            // Bỏ qua lỗi IO cho cache icon để không ảnh hưởng tới chức năng chính.
        }
    }

    private string GetCacheFilePath(string keyPath)
    {
        var hash = ComputeHash(keyPath);
        return Path.Combine(_iconCacheDirectory, hash + ".png");
    }

    private static string ComputeHash(string input)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha1.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty, StringComparison.Ordinal);
    }

    private static string? GetKeyPath(SearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.IconPath))
        {
            return result.IconPath;
        }

        if (result.Payload is string path && !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return null;
    }

    private static ImageSource? GetPluginIcon(PluginKind kind)
    {
        return kind switch
        {
            PluginKind.Calculator => CalculatorIcon,
            PluginKind.Web => WebIcon,
            PluginKind.System => SystemIcon,
            PluginKind.Utility => UtilityIcon,
            _ => null
        };
    }

    private static readonly ImageSource CalculatorIcon = CreateCircleIcon(System.Windows.Media.Color.FromRgb(76, 175, 80));
    private static readonly ImageSource WebIcon = CreateCircleIcon(System.Windows.Media.Color.FromRgb(33, 150, 243));
    private static readonly ImageSource SystemIcon = CreateCircleIcon(System.Windows.Media.Color.FromRgb(255, 87, 34));
    private static readonly ImageSource UtilityIcon = CreateCircleIcon(System.Windows.Media.Color.FromRgb(156, 39, 176));

    private static ImageSource CreateCircleIcon(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var geometry = new EllipseGeometry(new System.Windows.Point(16, 16), 10, 10);
        var drawing = new GeometryDrawing(brush, null, geometry);
        drawing.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(drawing);
        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            // Nếu là file ảnh thật, load trực tiếp bằng BitmapImage.
            var ext = Path.GetExtension(path);
            if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }

            // Ngược lại, dùng icon gắn với file (exe/dll/lnk...) qua SHGetFileInfo + Imaging.CreateBitmapSourceFromHIcon.
            var hIcon = GetSmallIconHandle(path);
            if (hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                return source;
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            // Nếu shell hoặc imaging lỗi, coi như không có icon.
            return null;
        }
    }

    private static IntPtr GetSmallIconHandle(string path)
    {
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const uint SHGFI_ICON = 0x000000100;
        const uint SHGFI_SMALLICON = 0x000000001;
        const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        var flags = SHGFI_ICON | SHGFI_SMALLICON;
        uint attributes = 0;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            attributes = FILE_ATTRIBUTE_NORMAL;
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        var shfi = new SHFILEINFO();
        var result = SHGetFileInfo(path, attributes, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
        if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return shfi.hIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
