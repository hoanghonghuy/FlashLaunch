param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [Parameter(Mandatory = $true)]
    [string]$PluginId,

    [string]$Configuration = "Debug",

    [string]$TargetDir
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path $env:APPDATA "FlashLaunch\plugins-dev"
}

$projectFull = (Resolve-Path $Project).Path

if ($PluginId -notmatch '^[A-Za-z0-9._-]{1,100}$') {
    throw "Invalid PluginId '$PluginId'. Allowed chars: [A-Za-z0-9._-]"
}

if (!(Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir | Out-Null
}

$pluginRoot = Join-Path $TargetDir $PluginId

Write-Host "Publishing dev plugin..."
Write-Host "Project: $projectFull"
Write-Host "PluginId: $PluginId"
Write-Host "Config: $Configuration"
Write-Host "Target: $pluginRoot"

if (Test-Path $pluginRoot) {
    Remove-Item $pluginRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $pluginRoot | Out-Null

dotnet publish $projectFull `
    -c $Configuration `
    -o $pluginRoot

$shared = @(
    "FlashLaunch.Core.dll",
    "FlashLaunch.PluginSdk.dll",
    "FlashLaunch.PluginHostSdk.dll",
    "Microsoft.Extensions.Logging.Abstractions.dll"
)

foreach ($name in $shared) {
    $p = Join-Path $pluginRoot $name
    if (Test-Path $p) {
        Remove-Item $p -Force
    }
}

Write-Host "Done. Copy plugin.json into: $pluginRoot (or ensure it's included in publish output)."
