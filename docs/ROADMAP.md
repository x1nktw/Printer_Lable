# Roadmap

Порядок из MASTER_SPEC v2.0 + аудит FrontPad.

| Этап | Статус | Содержание |
|------|--------|------------|
| 0 | Done | Solution topology, Domain, Plugins.Abstractions, arch tests |
| 1 | Done | Application ports, Product/Category services, unit tests |
| 2 | Done | EF Core + SQLite, migrations, repos, backup-on-migrate |
| 3 | Done | Avalonia shell + Каталог + Настройки |
| 4 | Done | Редактор шаблонов: undo/redo, grid/guides, multi-select, group/align/rotate, preview |
| 5 | Done | Печать каталога: Virtual → Windows → TSPL |
| 6 | Done | Очередь, multi-printer, история, reprint |
| 7 | Done (dev adapter) | FrontPad: inbox JSON + webhook placeholder; live API после спайка |
| 8 | Done | CPCL, ESC/POS stub, plugins loader, export, load tests, installer docs |

**Load-test target:** `LabelPrint.LoadTests` seeds ~1k products and asserts search &lt; 2s. Full **100k** catalog benchmark is a future hardening goal (indexes + keyset already in place).

Правила этапа: build + tests + ArchitectureTests перед переходом дальше; неготовые UI-фичи скрыты/disabled, без имитации успеха.
