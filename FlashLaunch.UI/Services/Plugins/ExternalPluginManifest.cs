namespace FlashLaunch.UI.Services.Plugins;

public sealed class ExternalPluginManifest
{
    public int ApiVersion { get; set; } = 1;

    public required string Id { get; set; }

    public required string Assembly { get; set; }

    public required string Type { get; set; }
}
