# Roadmap

Порядок из MASTER_SPEC v2.0 + аудит FrontPad.

**Текущий релиз: LabelPrint Pro 0.9.0 · FrontPad Bridge 1.3.4**

| Этап | Статус | Содержание |
|------|--------|------------|
| 0 | Done | Solution topology, Domain, Plugins.Abstractions, arch tests |
| 1 | Done | Application ports, Product/Category services, unit tests |
| 2 | Done | EF Core + SQLite, migrations, repos, backup-on-migrate |
| 3 | Done | Avalonia shell + Каталог + Настройки |
| 4 | Done | Редактор шаблонов: undo/redo, grid/guides, multi-select, group/align/rotate, preview |
| 5 | Done | Печать: Virtual → Windows → TSPL (+ CPCL / ESC/POS stub) |
| 6 | Done | Очередь, multi-printer, история, reprint |
| 7 | Done | FrontPad Bridge → webhook + inbox JSON (без shop API) |
| 8 | Done | Plugins loader, export, load tests, installer docs |
| 9 | Done (0.8.0) | Маркировка (корни/подкатегории, температура), статус системы, тема/акцент, Inno Setup + single-file publish, CI/CD |
| 10 | Done (0.9.0) | Переход на Velopack 1.2.0, one-click update, portable/setup release channel, обновлённые release docs |

**Load-test target:** `LabelPrint.LoadTests` seeds ~1k products and asserts search &lt; 2s. Full **100k** catalog benchmark is a future hardening goal (indexes + keyset already in place).

Правила этапа: build + tests + ArchitectureTests перед переходом дальше; неготовые UI-фичи скрыты/disabled, без имитации успеха.

Дальше (идеи): MSI/winget, code signing, маппинг артикулов FrontPad ↔ каталог, hardening 100k.

Автообновление через Velopack + GitHub Releases (проверка, скачивание пакета, авто-apply/restart) — сделано в 0.9.0.
