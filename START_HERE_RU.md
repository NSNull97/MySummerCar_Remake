# С чего начать

Этот архив — стартовый комплект для приватного donor-assisted ремейка **My Summer Car** на Unity 6 HDRP.

Зафиксированные пути:

```text
Оригинальная игра:
D:\SteamLibrary\steamapps\common\My Summer Car

Unity-проект и содержимое этого кита:
E:\GAYmDev_Studio\MySummerCar_Remake_Game

Внешний staging для извлечённых данных:
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging

Внешняя папка для декомпилированного справочного кода:
E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference
```

## 1. Создай Unity-проект

В Unity Hub создай **Unity 6 LTS, шаблон HDRP** по адресу:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game
```

Не создавай проект внутри папки оригинальной игры. Оригинальная установка должна оставаться read-only.

## 2. Распакуй архив

Распакуй **содержимое** архива прямо в корень Unity-проекта:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game
```

После распаковки рядом с `Assets`, `Packages` и `ProjectSettings` должны лежать:

```text
AGENTS.md
README.md
START_HERE_RU.md
Docs\
Prompts\
Config\
Tools\
Templates\
```

## 3. Проверь окружение

Открой PowerShell в корне проекта и выполни:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Tools\Validate-Environment.ps1
.\Tools\Create-Donor-Staging.ps1
```

Скрипты не изменяют оригинальную игру. Они только проверяют пути и создают внешние рабочие папки.

## 4. Открой корень проекта в Codex

Codex должен видеть именно:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game
```

Первой задачей вставь целиком:

```text
Prompts\00_BOOTSTRAP_AND_DONOR_AUDIT.md
```

Не запускай все prompt-файлы одновременно. Каждый следующий milestone запускается только после проверки результата предыдущего.

## 5. Что переносим, а что пересоздаём

Оригинальную игру используем как источник:

- размеров, координат, pivots и mounting points;
- иерархий и связей объектов;
- конфигураций, коэффициентов и поведения;
- terrain/world layout;
- отдельных чистых алгоритмов и таблиц;
- временных reference-моделей.

Финальная графика создаётся заново:

- новые production-модели;
- новые UV;
- новые PBR-текстуры;
- новые HDRP-материалы;
- новые LOD и collision meshes;
- новая растительность, дороги, освещение и эффекты.

Старую текстуру нельзя просто увеличить нейросетью и назвать ремастером. Это дед в 8K — пикселей больше, эпоха та же.

## 6. Опциональные инструменты

На первом этапе обязательны только Unity, Git и Codex. Позже пригодятся:

- AssetRipper — инвентаризация и экспорт Unity-ресурсов;
- ILSpy/ilspycmd — анализ managed assemblies;
- Blender — пересоздание моделей;
- Wwise — после завершения базовой архитектуры аудио;
- RenderDoc — диагностика рендера;
- Git LFS — для новых больших production-assets.

Не позволяй Codex молча скачивать или устанавливать сторонние программы. Сначала пусть объяснит, зачем они нужны.
