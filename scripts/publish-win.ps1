# Publishes LabelPrint Pro as a self-contained, single-file win-x64 app
# with a clean folder layout (exe + config/ + plugins/ + extensions/).
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/publish/LabelPrintPro"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $root "artifacts/publish/_staging-win-x64"

Push-Location $root
try {
    if (Test-Path $staging) {
        Remove-Item $staging -Recurse -Force
    }

    $outputFull = Join-Path $root $OutputDir
    if (Test-Path $outputFull) {
        Remove-Item $outputFull -Recurse -Force
    }

    Write-Host "Publishing single-file self-contained build..."
    dotnet publish src/LabelPrint.UI/LabelPrint.UI.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $staging

    New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $outputFull "config") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $outputFull "plugins") -Force | Out-Null

    $exe = Join-Path $staging "LabelPrint.UI.exe"
    if (-not (Test-Path $exe)) {
        throw "Expected executable not found: $exe"
    }

    Copy-Item $exe (Join-Path $outputFull "LabelPrint.UI.exe") -Force

    $settings = Join-Path $staging "appsettings.json"
    if (Test-Path $settings) {
        Copy-Item $settings (Join-Path $outputFull "config\appsettings.json") -Force
    }
    else {
        throw "appsettings.json missing from publish output"
    }

    # FrontPad Bridge (unpacked Chrome/Edge extension)
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

    Remove-Item $staging -Recurse -Force

    Write-Host ""
    Write-Host "Published layout:"
    Write-Host "  $OutputDir\LabelPrint.UI.exe"
    Write-Host "  $OutputDir\config\appsettings.json"
    Write-Host "  $OutputDir\plugins\"
    Write-Host "  $OutputDir\extensions\frontpad-bridge\  (see INSTALL.txt)"
}
finally {
    Pop-Location
}
