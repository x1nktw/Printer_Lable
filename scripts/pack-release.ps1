# Builds Velopack release assets (+ FrontPad Bridge zip) for GitHub Releases.
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Get-AppVersion {
    param([string]$Fallback)
    if (-not [string]::IsNullOrWhiteSpace($Fallback)) { return $Fallback.Trim() }
    $proj = Get-Content (Join-Path $root "src/LabelPrint.UI/LabelPrint.UI.csproj") -Raw
    if ($proj -match '<Version>([^<]+)</Version>') { return $Matches[1].Trim() }
    throw "Version not found. Pass -Version 1.0.0"
}

function Get-BridgeVersion {
    $manifestPath = Join-Path $root "extensions/frontpad-bridge/manifest.json"
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.version) {
        throw "extensions/frontpad-bridge/manifest.json has no version"
    }
    return [string]$manifest.version
}

function Ensure-Vpk {
    $cmd = Get-Command vpk -ErrorAction SilentlyContinue
    if ($cmd) { return }
    Write-Host "Installing vpk 1.2.0 global tool..."
    dotnet tool update -g vpk --version 1.2.0
    if ($LASTEXITCODE -ne 0) {
        dotnet tool install -g vpk --version 1.2.0
    }
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
        throw "vpk not found after install. Open a new shell or install: dotnet tool install -g vpk --version 1.2.0"
    }
}

Push-Location $root
try {
    $Version = Get-AppVersion -Fallback $Version
    $BridgeVersion = Get-BridgeVersion
    Write-Host "Packing Velopack release app=$Version bridge=$BridgeVersion ..."

    Ensure-Vpk
    & "$PSScriptRoot/publish-win.ps1" -Configuration $Configuration

    $out = Join-Path $root "artifacts/release"
    $vpkOut = Join-Path $out "velopack"
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    if (Test-Path $vpkOut) { Remove-Item $vpkOut -Recurse -Force }
    New-Item -ItemType Directory -Path $vpkOut -Force | Out-Null

    # Remove stale top-level release assets
    Get-ChildItem $out -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "LabelPrintPro-*" -or $_.Name -like "frontpad-bridge-*" } |
        Remove-Item -Force

    $packDir = Join-Path $root "artifacts/publish/vpk-app"
    $icon = Join-Path $root "src/LabelPrint.UI/Assets/app-icon.ico"

    Write-Host "Running vpk pack..."
    & vpk pack `
        --packId "LabelPrintPro" `
        --packVersion $Version `
        --packDir $packDir `
        --mainExe "LabelPrint.UI.exe" `
        --packTitle "LabelPrint Pro" `
        --packAuthors "LabelPrint Pro" `
        --icon $icon `
        --outputDir $vpkOut `
        --channel "win" `
        --shortcuts "Desktop,StartMenuRoot"

    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed (exit $LASTEXITCODE)"
    }

    # Bridge-only ZIP
    $bridgeZip = Join-Path $out "frontpad-bridge-$BridgeVersion.zip"
    $bridgeStaging = Join-Path $root "artifacts/publish/_bridge-zip"
    if (Test-Path $bridgeStaging) { Remove-Item $bridgeStaging -Recurse -Force }
    New-Item -ItemType Directory -Path $bridgeStaging -Force | Out-Null
    Copy-Item (Join-Path $root "extensions/frontpad-bridge\*") $bridgeStaging -Recurse -Force
    Remove-Item (Join-Path $bridgeStaging "test-parse.js") -ErrorAction SilentlyContinue
    $guidePath = Join-Path $PSScriptRoot "frontpad-bridge-INSTALL.txt"
    if (Test-Path $guidePath) {
        Copy-Item $guidePath (Join-Path $bridgeStaging "INSTALL.txt") -Force
    }
    Compress-Archive -Path (Join-Path $bridgeStaging "*") -DestinationPath $bridgeZip -Force
    Remove-Item $bridgeStaging -Recurse -Force

    # Convenience copies at release root (Setup + portable if present)
    Get-ChildItem $vpkOut -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $out $_.Name) -Force
    }

    Write-Host ""
    Write-Host "Release assets:"
    Get-ChildItem $out -Recurse -File | Sort-Object FullName | ForEach-Object {
        $rel = $_.FullName.Substring($out.Length).TrimStart('\')
        Write-Host ("  {0}  ({1:N0} bytes)" -f $rel, $_.Length)
    }
    Write-Host ""
    Write-Host "GitHub:"
    Write-Host "  1. Commit & push main"
    Write-Host "  2. git tag v$Version && git push origin v$Version"
    Write-Host "  3. Upload artifacts/release/* (Setup.exe, *.nupkg, releases*.json, bridge zip)"
    Write-Host "     or: vpk upload github --repoUrl https://github.com/x1nktw/Printer_Lable --outputDir artifacts/release/velopack --publish --token `$env:GITHUB_TOKEN"
}
finally {
    Pop-Location
}
