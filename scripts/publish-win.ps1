# Publishes LabelPrint Pro for Velopack packaging.
# Layout: single-file exe (DLLs inside) + config/ + plugins/ + extensions/
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/publish/vpk-app"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    $outputFull = Join-Path $root $OutputDir
    if (Test-Path $outputFull) {
        try {
            Remove-Item $outputFull -Recurse -Force -ErrorAction Stop
        }
        catch {
            $bak = "$outputFull.lock-$(Get-Date -Format 'yyyyMMddHHmmss')"
            Write-Host "Output locked, renaming to $bak"
            Rename-Item $outputFull $bak
        }
    }

    Write-Host "Publishing self-contained win-x64 single-file + folders for Velopack..."
    dotnet publish src/LabelPrint.UI/LabelPrint.UI.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $outputFull

    # FrontPad Bridge next to the app (not inside single-file)
    $bridgeSrc = Join-Path $root "extensions\frontpad-bridge"
    if (-not (Test-Path $bridgeSrc)) {
        throw "FrontPad Bridge not found: $bridgeSrc"
    }
    $bridgeDst = Join-Path $outputFull "extensions\frontpad-bridge"
    New-Item -ItemType Directory -Path $bridgeDst -Force | Out-Null
    Copy-Item -Path (Join-Path $bridgeSrc "*") -Destination $bridgeDst -Recurse -Force
    Remove-Item (Join-Path $bridgeDst "test-parse.js") -ErrorAction SilentlyContinue

    $guidePath = Join-Path $PSScriptRoot "frontpad-bridge-INSTALL.txt"
    if (Test-Path $guidePath) {
        Copy-Item $guidePath (Join-Path $bridgeDst "INSTALL.txt") -Force
    }

    # Prefer config/appsettings.json (Program.cs); keep root copy as fallback
    $configDir = Join-Path $outputFull "config"
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    $settings = Join-Path $outputFull "appsettings.json"
    if (Test-Path $settings) {
        Copy-Item $settings (Join-Path $configDir "appsettings.json") -Force
        Remove-Item $settings -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $outputFull "plugins") -Force | Out-Null

    # Single-file still emits project XML docs next to the exe — drop them from the package.
    Get-ChildItem $outputFull -Filter "*.xml" -File | Remove-Item -Force

    $required = @(
        (Join-Path $outputFull "LabelPrint.UI.exe"),
        (Join-Path $outputFull "config\appsettings.json"),
        (Join-Path $outputFull "extensions\frontpad-bridge\manifest.json")
    )
    foreach ($path in $required) {
        if (-not (Test-Path $path)) {
            throw "Missing required publish artifact: $path"
        }
    }

    Write-Host "Published layout:"
    Get-ChildItem $outputFull | ForEach-Object {
        if ($_.PSIsContainer) {
            Write-Host ("  {0}/" -f $_.Name)
        } else {
            Write-Host ("  {0}  ({1:N0} bytes)" -f $_.Name, $_.Length)
        }
    }
    Write-Host "Published to $OutputDir"
}
finally {
    Pop-Location
}
