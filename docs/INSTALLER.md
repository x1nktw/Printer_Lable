# Installer & distribution

LabelPrint Pro **0.8.0** — self-contained .NET 8 Windows x64.  
FrontPad Bridge **1.3.4** (версия в `extensions/frontpad-bridge/manifest.json`).

## GitHub Releases (recommended)

1. Commit and push `main`.
2. Tag and push (triggers [.github/workflows/release.yml](../.github/workflows/release.yml)):

```powershell
git tag v0.8.0
git push origin v0.8.0
```

3. Assets on the release page:

| Asset | Purpose |
|-------|---------|
| `LabelPrintPro-0.8.0-win-x64-setup.exe` | **Установщик** (Inno Setup) — Program Files, ярлыки, uninstall |
| `LabelPrintPro-0.8.0-win-x64.zip` | **Portable** — распаковать и запустить `LabelPrint.UI.exe` |
| `frontpad-bridge-1.3.4.zip` | Только Chrome/Edge расширение (не путать с версией приложения) |

Имена: app = `<Version>` из `LabelPrint.UI.csproj`; Bridge = `version` из `manifest.json`.

Local pack (publish + zip + setup):

```powershell
./scripts/pack-release.ps1
# → artifacts/release/
```

Если Inno Setup не установлен, скрипт попытается поставить его через winget / скачать.  
Только zip без установщика: `./scripts/pack-release.ps1 -SkipInstaller`

Скрипт установщика: [installer/LabelPrintPro.iss](../installer/LabelPrintPro.iss).

## Build publish folder

```powershell
./scripts/publish-win.ps1
```

Output: `artifacts/publish/LabelPrintPro/`

```
LabelPrintPro/
  LabelPrint.UI.exe          # self-contained single-file (~50 MB)
  config/appsettings.json
  plugins/                   # optional plugin DLLs
  extensions/
    frontpad-bridge/         # Bridge 1.3.4 — см. INSTALL.txt
```

## Manual / portable install

1. Распакуйте zip или скопируйте publish-папку (или запустите `*-setup.exe`).
2. Запустите `LabelPrint.UI.exe`.
3. Optional: drop plugin DLLs into `plugins/` (see [README](../README.md#plugins)).

### FrontPad Bridge 1.3.4 (Chrome / Edge)

Расширение уже в сборке — отдельно скачивать не обязательно (zip на релизе — для обновления только Bridge).

1. Запустите LabelPrint Pro, в **Настройки → Общие** проверьте webhook (например `http://127.0.0.1:8765/`), сохраните и перезапустите приложение.
2. Chrome: `chrome://extensions` или Edge: `edge://extensions` → **Режим разработчика**.
3. **Загрузить распакованное** → `extensions\frontpad-bridge` из каталога установки.
4. Откройте FrontPad заново; в popup — «Хук на странице FrontPad активен».

Краткая памятка: `extensions\frontpad-bridge\INSTALL.txt`. Подробнее: [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md).

Data defaults:

- Database: `%LocalAppData%\LabelPrintPro\labelprint.db`
- Logs: `%LocalAppData%\LabelPrintPro\logs\`
- Exports: `%LocalAppData%\LabelPrintPro\exports\`
- Backups: `%LocalAppData%\LabelPrintPro\backups\` (override in **Настройки**)

## winget (planned)

```yaml
PackageIdentifier: LabelPrintPro.LabelPrintPro
PackageVersion: 0.8.0
Installers:
  - Architecture: x64
    InstallerType: inno
    InstallerUrl: https://github.com/x1nktw/Printer_Lable/releases/download/v0.8.0/LabelPrintPro-0.8.0-win-x64-setup.exe
    InstallerSha256: <sha256>
```

## Code signing

Sign `LabelPrintPro-*-setup.exe` (and ideally the app exe) with Authenticode before wide distribution to avoid SmartScreen warnings.
