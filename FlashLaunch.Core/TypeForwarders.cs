using System.Runtime.CompilerServices;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;

[assembly: TypeForwardedTo(typeof(IPlugin))]
[assembly: TypeForwardedTo(typeof(IPluginSelfTest))]
[assembly: TypeForwardedTo(typeof(IPluginIdentity))]
[assembly: TypeForwardedTo(typeof(IPluginHost))]
[assembly: TypeForwardedTo(typeof(IPluginHostAware))]
[assembly: TypeForwardedTo(typeof(PluginKind))]
[assembly: TypeForwardedTo(typeof(SearchResult))]
