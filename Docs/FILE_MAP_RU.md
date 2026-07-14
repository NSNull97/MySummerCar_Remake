# Карта файлов

## В корне

- `AGENTS.md` — постоянные правила для Codex. Не вставляется каждый раз: Codex должен читать его из корня репозитория.
- `START_HERE_RU.md` — инструкция для первого запуска.
- `README.md` — краткое описание проекта.
- `.gitignore` — не даёт случайно закоммитить Unity-мусор и донорские файлы.
- `.gitattributes` — базовые правила Git LFS для новых тяжёлых assets.
- `.editorconfig` — стиль C# и текстовых файлов.

## Config

- `DonorPaths.local.json` — рабочие пути на твоём компьютере; Git его игнорирует.
- `DonorPaths.example.json` — пример для репозитория.

## Docs

- `PROJECT.md` — что именно строим.
- `ARCHITECTURE.md` — модульная схема проекта.
- `PORTING_GUIDE.md` — что переносить напрямую, что переписывать и что пересоздавать.
- `REVERSE_ENGINEERING.md` — безопасный порядок анализа оригинала.
- `ART_GUIDE.md` — новая графика, модели, PBR и HDRP.
- `AUDIO_GUIDE.md` — Wwise и временный Unity backend.
- `VEHICLE_SYSTEM.md` — сборка и симуляция автомобиля.
- `PLAYER_INTERACTION.md` — игрок и физические взаимодействия.
- `WORLD_WEATHER.md` — мир, дороги, streaming и погода.
- `SAVE_SYSTEM.md` — stable IDs, версии и миграции.
- `TESTING_AND_VALIDATION.md` — проверки и тесты.
- `PERFORMANCE_BUDGET.md` — стартовые бюджеты производительности.
- `ROADMAP.md` — порядок milestones и критерии выхода.
- `REFERENCE_CAPTURE_CHECKLIST.md` — что измерить в оригинальной игре.

## Prompts

Открывай по одному и вставляй в Codex целиком. Не запускай следующий milestone, пока предыдущий не компилируется и не имеет отчёта.

## Tools

PowerShell-скрипты для проверки путей, создания staging и установки кита.

## Templates

Шаблоны ledger, инвентаризации, milestone-отчёта, behavioral capture и ADR.
