# Installer & distribution

LabelPrint Pro **0.9.1** — self-contained .NET 8 Windows x64.  
FrontPad Bridge **1.3.4** (версия в `extensions/frontpad-bridge/manifest.json`).

## GitHub Releases (recommended)

1. Commit and push `main`.
2. Tag and push (triggers [.github/workflows/release.yml](../.github/workflows/release.yml)):

```powershell
git tag v0.9.1
git push origin v0.9.1
```

3. Assets on the release page:

| Asset | Purpose |
|-------|---------|
| `LabelPrintPro-win-Setup.exe` | **Velopack Setup** — первая установка и база для автообновлений |
| `LabelPrintPro-win-Portable.zip` | **Portable** — распаковать и запустить установщик/ярлык Velopack |
| `LabelPrintPro-0.9.1-full.nupkg` + `releases.win.json` | Канал автообновлений Velopack |
| `frontpad-bridge-1.3.4.zip` | Только Chrome/Edge расширение (не путать с версией приложения) |

Имена: app = `<Version>` из `LabelPrint.UI.csproj`; Bridge = `version` из `manifest.json`.

Local pack:

```powershell
./scripts/pack-release.ps1
# → artifacts/release/
```

Скрипт сам ставит `vpk 1.2.0`, если его нет.  
Результат: `artifacts/release/` и `artifacts/release/velopack/`.

## Build publish folder

```powershell
./scripts/publish-win.ps1
```

Output: `artifacts/publish/vpk-app/`

```
vpk-app/
  LabelPrint.UI.exe          # single-file (DLL внутри exe)
  config/appsettings.json
  plugins/                   # optional plugin DLLs
  extensions/
    frontpad-bridge/         # Bridge 1.3.4 — см. INSTALL.txt
```

После Velopack Setup приложение лежит в:

```
%LocalAppData%\LabelPrintPro\
  current\                   # рабочая копия (exe + config/ + plugins/ + extensions/)
  Update.exe
  labelprint.db / logs / …   # данные пользователя
```

Смотрите папки в **`current\`**, не в корне `%LocalAppData%\LabelPrintPro\`.

## Install / portable

1. Для нормальной установки и автообновлений запустите `LabelPrintPro-win-Setup.exe`.
2. Для portable-режима распакуйте `LabelPrintPro-win-Portable.zip` и запустите ярлык/exe Velopack.
3. Bridge: `%LocalAppData%\LabelPrintPro\current\extensions\frontpad-bridge`.
4. Optional: drop plugin DLLs into `current\plugins\` (see [README](../README.md#plugins)).

### FrontPad Bridge 1.3.4 (Chrome / Edge)

Расширение уже в сборке — отдельно скачивать не обязательно (zip на релизе — для обновления только Bridge).

1. Запустите LabelPrint Pro, в **Настройки → Общие** проверьте webhook (например `http://127.0.0.1:8765/`), сохраните и перезапустите приложение.
2. Chrome: `chrome://extensions` или Edge: `edge://extensions` → **Режим разработчика**.
3. **Загрузить распакованное** → `extensions\frontpad-bridge` из каталога **`current\`**.
4. Откройте FrontPad заново; в popup — «Хук на странице FrontPad активен».

Краткая памятка: `extensions\frontpad-bridge\INSTALL.txt`. Подробнее: [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md).

## Автообновление (из приложения)

Начиная с **0.9.0** приложение использует **Velopack** и проверяет [GitHub Releases](https://github.com/x1nktw/Printer_Lable/releases):

1. Один раз установите приложение через **LabelPrintPro-win-Setup.exe**.
2. Дальше: **Настройки → Общие → Система** → **Обновить**.
3. Приложение скачает пакет, применит обновление и перезапустится автоматически.
4. На **Главной** при старте один раз проверяется наличие обновления (с таймаутом, без блокировки UI).

Конфигурация в `config/appsettings.json` (или рядом с exe):

```json
"Updates": {
  "Enabled": true,
  "RepoUrl": "https://github.com/x1nktw/Printer_Lable",
  "IncludePrerelease": false
}
```

Важно:

- In-app обновление работает только для копий, установленных через **Velopack Setup**.
- Пользователям **0.8.x** на Inno Setup нужно один раз поставить новый `LabelPrintPro-win-Setup.exe`.
- После этого обновления ставятся в один клик, даже если папка приложения была перемещена.

Data defaults:

- Database: `%LocalAppData%\LabelPrintPro\labelprint.db`
- Logs: `%LocalAppData%\LabelPrintPro\logs\`
- Exports: `%LocalAppData%\LabelPrintPro\exports\`
- Backups: `%LocalAppData%\LabelPrintPro\backups\` (override in **Настройки**)

## winget (planned)

```yaml
PackageIdentifier: LabelPrintPro.LabelPrintPro
PackageVersion: 0.9.1
Installers:
  - Architecture: x64
    InstallerType: exe
    InstallerUrl: https://github.com/x1nktw/Printer_Lable/releases/download/v0.9.1/LabelPrintPro-win-Setup.exe
    InstallerSha256: <sha256>
```

## Code signing

Sign `LabelPrintPro-win-Setup.exe` (and ideally app binaries / packages) with Authenticode before wide distribution to avoid SmartScreen warnings.
