using System;
using System.Reflection;
using System.Runtime.Loader;

namespace FlashLaunch.UI.Services.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginMainAssemblyPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
        {
            return null;
        }

        if (string.Equals(assemblyName.Name, "FlashLaunch.Core", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(assemblyName.Name, "FlashLaunch.PluginSdk", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(assemblyName.Name, "FlashLaunch.PluginHostSdk", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(assemblyName.Name, "Microsoft.Extensions.Logging.Abstractions", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(assemblyName.Name, "PresentationFramework", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "PresentationCore", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "WindowsBase", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "System.Xaml", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "WindowsFormsIntegration", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "System.Windows.Forms", StringComparison.Ordinal) ||
            string.Equals(assemblyName.Name, "System.Drawing.Common", StringComparison.Ordinal))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
