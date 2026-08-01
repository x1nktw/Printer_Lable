# Installer & distribution

LabelPrint Pro ships as a self-contained .NET 8 Windows x64 application.

## Build publish folder

From the repository root:

```powershell
./scripts/publish-win.ps1
```

Output: `artifacts/publish/win-x64/` (executable `LabelPrint.UI.exe`, dependencies, empty `plugins/` folder).

Options:

- `-Configuration Release` (default)
- `-OutputDir artifacts/publish/win-x64`

## Manual install

1. Copy the publish folder to `%ProgramFiles%\LabelPrint Pro\` (or any path).
2. Create a desktop shortcut to `LabelPrint.UI.exe`.
3. Optional: drop plugin DLLs into `plugins/` (see [README](../README.md#plugins)).

Data defaults:

- Database: `%LocalAppData%\LabelPrintPro\labelprint.db`
- Logs: `%LocalAppData%\LabelPrintPro\logs\`
- Exports: `%LocalAppData%\LabelPrintPro\exports\`
- Backups: `%LocalAppData%\LabelPrintPro\backups\` (override in **Настройки**)

## MSI (planned)

For commercial rollout, wrap the publish output with WiX Toolset or equivalent:

- Per-machine install under `Program Files`
- Start menu + optional desktop shortcut
- Upgrade code / product code for in-place upgrades
- Registry entries for `winget` ARP metadata

## winget (planned)

Example manifest sketch (adjust version/hash after build):

```yaml
PackageIdentifier: LabelPrintPro.LabelPrintPro
PackageVersion: 0.8.0
Installers:
  - Architecture: x64
    InstallerType: msi
    InstallerUrl: https://releases.example.com/LabelPrintPro-0.8.0-x64.msi
    InstallerSha256: <sha256>
```

Submit to [winget-pkgs](https://github.com/microsoft/winget-pkgs) once MSI and signing are in place.

## Code signing

Sign `LabelPrint.UI.exe` and the MSI with your Authenticode certificate before wide distribution to avoid SmartScreen warnings.
