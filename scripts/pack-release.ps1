# Builds GitHub Release assets: portable ZIP + Setup.exe (+ optional Bridge ZIP).
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "",
    [string]$Configuration = "Release",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Get-AppVersion {
    param([string]$Fallback)
    if (-not [string]::IsNullOrWhiteSpace($Fallback)) { return $Fallback.Trim() }
    $proj = Get-Content (Join-Path $root "src/LabelPrint.UI/LabelPrint.UI.csproj") -Raw
    if ($proj -match '<Version>([^<]+)</Version>') { return $Matches[1].Trim() }
    throw "Version not found. Pass -Version 0.8.0"
}

function Get-BridgeVersion {
    $manifestPath = Join-Path $root "extensions/frontpad-bridge/manifest.json"
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.version) {
        throw "extensions/frontpad-bridge/manifest.json has no version"
    }
    return [string]$manifest.version
}

function Find-ISCC {
    $candidates = @(
        ${env:INNO_SETUP_PATH},
        (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $root "tools\InnoSetup6\ISCC.exe")
    ) | Where-Object { $_ }
    foreach ($p in $candidates) {
        if (Test-Path $p) { return (Resolve-Path $p).Path }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Ensure-InnoSetup {
    $existing = Find-ISCC
    if ($existing) { return $existing }

    # Prefer winget when available (no broken download.php HTML page).
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "Installing Inno Setup via winget..."
        & winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements --disable-interactivity
        $existing = Find-ISCC
        if ($existing) { return $existing }
    }

    Write-Host "Downloading Inno Setup 6 from files.jrsoftware.org..."
    $cache = Join-Path $root "artifacts\cache"
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $installer = Join-Path $cache "innosetup-6.7.3.exe"
    # Direct binary URL (download.php returns HTML without a browser).
    $uri = "https://files.jrsoftware.org/is/6/innosetup-6.7.3.exe"
    Invoke-WebRequest -Uri $uri -OutFile $installer -UseBasicParsing
    if ((Get-Item $installer).Length -lt 1MB) {
        throw "Inno Setup download looks invalid ($((Get-Item $installer).Length) bytes). Install manually: https://jrsoftware.org/isinfo.php"
    }

    $dest = Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6"
    $args = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CURRENTUSER",
        "/DIR=`"$dest`""
    )
    $proc = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
    if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 1) {
        Write-Warning "Inno Setup installer exit code: $($proc.ExitCode)"
    }

    $iscc = Find-ISCC
    if (-not $iscc) {
        throw "ISCC.exe still not found after Inno Setup install. Install from https://jrsoftware.org/isinfo.php and re-run."
    }
    return $iscc
}

Push-Location $root
try {
    $Version = Get-AppVersion -Fallback $Version
    $BridgeVersion = Get-BridgeVersion
    Write-Host "Packing release app=$Version bridge=$BridgeVersion ..."

    & "$PSScriptRoot/publish-win.ps1" -Configuration $Configuration

    $out = Join-Path $root "artifacts/release"
    New-Item -ItemType Directory -Path $out -Force | Out-Null

    # Remove stale assets from previous packs
    Get-ChildItem $out -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "LabelPrintPro-*" -or $_.Name -like "frontpad-bridge-*" } |
        Remove-Item -Force

    # --- Portable ZIP ---
    $appZip = Join-Path $out "LabelPrintPro-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $root "artifacts/publish/LabelPrintPro\*") `
        -DestinationPath $appZip -Force

    # --- Bridge-only ZIP (version from extension manifest.json) ---
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
    # --- Setup.exe (Inno Setup) ---
    if (-not $SkipInstaller) {
        $iscc = Ensure-InnoSetup
        Write-Host "Building installer with: $iscc"
        $iss = Join-Path $root "installer\LabelPrintPro.iss"
        $publishAbs = ((Join-Path $root "artifacts\publish\LabelPrintPro") -replace '\\', '/')
        $outAbs = ($out -replace '\\', '/')
        & $iscc `
            "/DMyAppVersion=$Version" `
            "/DPublishDir=$publishAbs" `
            "/DOutputDir=$outAbs" `
            $iss
        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed (exit $LASTEXITCODE)"
        }
    }
    else {
        Write-Host "SkipInstaller: setup.exe not built."
    }

    Write-Host ""
    Write-Host "Release assets:"
    Get-ChildItem $out | Sort-Object Name | ForEach-Object {
        Write-Host ("  {0}  ({1:N0} bytes)" -f $_.Name, $_.Length)
    }
    Write-Host ""
    Write-Host "GitHub release:"
    Write-Host "  git tag v$Version && git push origin v$Version"
    Write-Host "  # or: gh release create v$Version artifacts/release/*"
}
finally {
    Pop-Location
}
