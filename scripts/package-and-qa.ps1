param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "..\FlashLaunch.sln"
$uiProject = Join-Path $root "..\FlashLaunch.UI\FlashLaunch.UI.csproj"
$artifacts = Join-Path $root "..\artifacts"
$standaloneDir = Join-Path $artifacts "publish-standalone"
$portableDir = Join-Path $artifacts "publish-portable"
$logsDir = Join-Path $artifacts "logs"

Write-Host "=== FlashLaunch packaging script ==="
Write-Host "Solution: $solution"
Write-Host "UI Project: $uiProject"

# 1. Clean artifacts
if (Test-Path $artifacts) {
    Remove-Item $artifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $standaloneDir | Out-Null
New-Item -ItemType Directory -Path $portableDir | Out-Null
New-Item -ItemType Directory -Path $logsDir | Out-Null

# 2. Restore & publish standalone build (self-contained, single-file)
Write-Host "Publishing standalone build (self-contained)..."
dotnet publish $uiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $standaloneDir

# 2b. Publish portable build (framework-dependent)
Write-Host "Publishing portable build (framework-dependent)..."
dotnet publish $uiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $portableDir

# 3. Capture perf/error logs sample (optional)
$logSource = Join-Path $env:APPDATA "FlashLaunch\logs"
if (Test-Path $logSource) {
    Copy-Item "$logSource\flashlaunch-*.log" $logsDir -ErrorAction SilentlyContinue
}

# 4. Placeholder for MSIX packaging (manual step)
$msixHint = @"
MSIX Packaging:
1. Open MSIX Packaging Tool.
2. Input source: $standaloneDir
3. Follow docs/PackagingQA.md.
"@
$msixHint | Out-File (Join-Path $artifacts "MSIX-INSTRUCTIONS.txt") -Encoding UTF8

# 5. Placeholder for Squirrel packaging
$squirrelCmd = ".\packages\squirrel.windows\tools\Squirrel.exe --releasify FlashLaunch.nuspec --releaseDir artifacts\squirrel"
$squirrelHint = "Run: $squirrelCmd"
$squirrelHint | Out-File (Join-Path $artifacts "SQUIRREL-INSTRUCTIONS.txt") -Encoding UTF8

# 6. QA checklist output
$qaChecklist = Join-Path $artifacts "QA-CHECKLIST.md"
Copy-Item (Join-Path $root "..\docs\PackagingQA.md") $qaChecklist

Write-Host "Artifacts ready in $artifacts"
