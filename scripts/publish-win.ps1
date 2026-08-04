# Publishes LabelPrint Pro for Velopack packaging (self-contained win-x64 folder).
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
        Remove-Item $outputFull -Recurse -Force
    }

    Write-Host "Publishing self-contained win-x64 (folder, not single-file) for Velopack..."
    dotnet publish src/LabelPrint.UI/LabelPrint.UI.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $outputFull

    # FrontPad Bridge next to the app
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

    # Ensure config layout expected by Program.cs (config/appsettings.json preferred)
    $configDir = Join-Path $outputFull "config"
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    $settings = Join-Path $outputFull "appsettings.json"
    if (Test-Path $settings) {
        Copy-Item $settings (Join-Path $configDir "appsettings.json") -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $outputFull "plugins") -Force | Out-Null

    Write-Host "Published to $OutputDir"
}
finally {
    Pop-Location
}
