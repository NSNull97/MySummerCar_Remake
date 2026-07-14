# Журнал извлечения 04A1

## Выполненный pipeline

```text
donor read-only files
  -> AssetRipper 1.3.14 external Unity-project export
  -> WorldTransferExtractor 1.0.0 streaming YAML normalization
  -> versioned JSON/CSV world database
  -> Unity WorldPartitionBuilder reference proxies
```

## AssetRipper

- Источник: `D:\SteamLibrary\steamapps\common\My Summer Car`.
- Выход: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\raw\world\milestone-04a1\assetripper-unity-project`.
- Результат: 17 247 файлов, 1 754 855 811 байт, 5 сцен, 1 939 mesh-like assets, 986 materials, 1 320 textures.
- `GAME.unity`: 234 261 975 байт, SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Известное предупреждение: одна `Texture2D` в `sharedassets3.assets` не прочитана. Экспорт сцены, mesh metadata и geometry references завершён.
- Измеренное время полного экспорта: около 29,6 с на текущей машине.

## Нормализация

Extractor собран `dotnet 8` без внешних NuGet-пакетов. Release build: 0 warnings, 0 errors. Финальный запуск занял 3,295 с и завершился code 0.

Получено:

- 36 045 GameObject;
- 36 045 Transform;
- 13 509 geometric entities;
- 1 362 unique referenced mesh GUID;
- 5 001 colliders;
- 2 007 bounds-review records;
- 37 unsupported class IDs;
- 49 spatial cells;
- 29 landmark representatives.

## Выходы

Normalized и manifest copies находятся вне Git:

- `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\normalized\world\milestone-04a1`;
- `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\manifests\world\milestone-04a1`;
- `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\logs\world\milestone-04a1`.

Там сохранены все 12 требуемых `World*Manifest`/CSV файлов и tool/Unity logs. Raw donor payload в tracked `Assets` не копировался.

## Команда нормализации

Использовался Release DLL с аргументами `--scene`, `--mesh-root`, `--external-output`, `--project-output`, source hashes, `--rules Config/WorldTransferRules.example.json`, audited origin `(-169.98,-1.611,1040.625)` и `--cell-size 512`. Абсолютные пути передавались только в локальную команду, не hard-coded в runtime source.
