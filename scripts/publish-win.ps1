# Publishes LabelPrint Pro as a self-contained win-x64 deployment.
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/publish/win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    dotnet publish src/LabelPrint.UI/LabelPrint.UI.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $OutputDir

    $pluginsDir = Join-Path $OutputDir "plugins"
    if (-not (Test-Path $pluginsDir)) {
        New-Item -ItemType Directory -Path $pluginsDir | Out-Null
    }

    Write-Host "Published to $OutputDir"
}
finally {
    Pop-Location
}
