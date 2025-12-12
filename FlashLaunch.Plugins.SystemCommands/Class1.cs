using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;

namespace FlashLaunch.Plugins.SystemCommands;

public sealed class SystemCommandsPlugin : IPlugin
{
    private readonly IStringLocalizer _localizer;
    private readonly ISystemCommandState _state;
    private readonly IReadOnlyDictionary<string, SystemCommand> _commands;

    public SystemCommandsPlugin(IStringLocalizer localizer, ISystemCommandState state)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        _commands = new Dictionary<string, SystemCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["shutdown"] = new SystemCommand(
                _localizer["Plugin_System_Shutdown_Title"],
                _localizer["Plugin_System_Shutdown_Description"],
                () => Process.Start("shutdown", "/s /t 0")),
            ["restart"] = new SystemCommand(
                _localizer["Plugin_System_Restart_Title"],
                _localizer["Plugin_System_Restart_Description"],
                () => Process.Start("shutdown", "/r /t 0")),
            ["sleep"] = new SystemCommand(
                _localizer["Plugin_System_Sleep_Title"],
                _localizer["Plugin_System_Sleep_Description"],
                () => Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0")),
            ["lock"] = new SystemCommand(
                _localizer["Plugin_System_Lock_Title"],
                _localizer["Plugin_System_Lock_Description"],
                () => Process.Start("rundll32.exe", "user32.dll,LockWorkStation")),
            ["empty trash"] = new SystemCommand(
                _localizer["Plugin_System_EmptyRecycleBin_Title"],
                _localizer["Plugin_System_EmptyRecycleBin_Description"],
                () => Process.Start("PowerShell", "-Command Clear-RecycleBin -Force"))
        };
    }

    public string Name => _localizer["Plugin_System_Name"];

    public string Description => _localizer["Plugin_System_Description"];

    public PluginKind Kind => PluginKind.System;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var trimmed = searchQuery.Trim();
        if (!trimmed.StartsWith('>'))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        // Bỏ ký tự '>' và khoảng trắng ngay sau nó để chấp nhận cả "> shutdown" và ">shutdown".
        var commandText = trimmed[1..].TrimStart();
        if (string.IsNullOrEmpty(commandText))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var matches = new List<SearchResult>();
        foreach (var kvp in _commands)
        {
            if (!_state.IsCommandEnabled(kvp.Key))
            {
                continue;
            }

            if (kvp.Key.StartsWith(commandText, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new SearchResult
                {
                    Title = kvp.Value.Title,
                    Subtitle = kvp.Value.Description,
                    Score = 0.8,
                    Payload = kvp.Value,
                    Plugin = this
                });
            }
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(matches);
    }

    public Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default)
    {
        if (result.Payload is not SystemCommand command)
        {
            throw new InvalidOperationException("Invalid system command payload.");
        }

        command.Action();

        return Task.CompletedTask;
    }

    private sealed record SystemCommand(string Title, string Description, Action Action);
}
