# Enviro 3 — installation blocker перед Milestone 07A

Дата проверки: 2026-07-17

Статус: **RESOLVED 2026-07-17 — основной пакет импортирован вручную**

Последующий post-import compatibility blocker был зафиксирован в
`Docs/Weather/ENVIRO3_COMPATIBILITY_BLOCKER.md` и также закрыт.

> Исторический снимок: разделы ниже фиксируют состояние до ручного импорта.
> Они сохранены как evidence и не описывают текущее состояние завершённого 07A.

## Причина остановки

`07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md` запрещает автоматически устанавливать
или подменять отсутствующий Enviro 3. Внешние распакованные каталоги доступны,
но они ещё не импортированы в Unity-проект и потому не являются установленной
dependency.

До ручного импорта нельзя честно создавать Enviro integration assembly,
WeatherLab, bindings, smoke states или заявлять совместимость пакета с текущими
Unity/HDRP.

## Проверенное состояние проекта

- Git был чистым на `e4130be` (`world: complete milestone 06B3`).
- Unity: `6000.3.11f1`.
- HDRP: `17.3.0`.
- `Packages/manifest.json` не содержит Enviro.
- В `Assets` нет `Enviro 3 - Sky and Weather`, Enviro types или vendor asmdefs.
- В project-owned C#/asmdefs/scenes/prefabs/assets нет ссылок на Enviro API.
- Scripting defines не содержат `ENVIRO_3` или `ENVIRO_HDRP`.
- HDRP Custom Post Process lists не содержат Enviro renderer.
- Dedicated Enviro integration assembly и WeatherLab отсутствуют.
- `DonorWorldBaseline-v001` остаётся `Frozen`, active profile —
  `donor-feature-parity-06b2`.

## Доступный внешний vendor payload

Проверены два переданных пользователем read-only источника вне проекта:

1. `E:\GAYmDev_Studio\Additional\Enviro 3 - Sky and Weather`
   - 538 файлов, 305 967 970 байт;
   - полный Unity asset tree с `.meta`, prefab, profiles, scripts и HDRP sample;
   - ожидаемый самим vendor кодом project path:
     `Assets/Enviro 3 - Sky and Weather`.
2. `E:\GAYmDev_Studio\Additional\Enviro 3 - Additional Weather Pack`
   - 135 файлов, 226 809 050 байт;
   - требует Enviro `3.0.7a+` по `HowToUse.txt`.

Vendor GUID-дубликаты внутри payload и пересечения с текущим `Assets` не
обнаружены. Исходные vendor-файлы не изменялись.

## Версия и compatibility risk

Точную версию пока нельзя заявить уверенно:

- первая строка `version.txt` сообщает `Enviro 3.0.0`;
- changelog в том же файле заканчивается на `v3.0.8`;
- explicit compatibility с Unity 6 / HDRP 17.3 в локальной поставке не указана.

Дополнительный риск: `Enviro3.Runtime.asmdef` безусловно ссылается на URP
Runtime, Core RP Runtime и HDRP Runtime. В текущем проекте установлен HDRP, но
URP отсутствует. После ручного импорта возможна missing-assembly/compile ошибка.
Нельзя автоматически добавлять URP или редактировать vendor asmdef: сначала
нужен фактический Unity import/compile log.

## Требуемое ручное действие

Предпочтительный путь — импорт лицензированного пакета через Unity Package
Manager (`My Assets`) по инструкции
`Prompts/ENVIRO3_MANUAL_SETUP_RU.md`.

Если используются именно переданные распакованные каталоги, пользователь должен
вручную импортировать основной каталог целиком, сохранив имя и destination:

`Assets/Enviro 3 - Sky and Weather`

Не перемещать, не переименовывать, не объединять и не редактировать vendor
файлы. Не добавлять sample scenes в Build Settings и не включать Enviro в
Bootstrap до аудита.

Сначала требуется импортировать только основной пакет, дождаться полного
reimport/compile и сохранить Unity Console errors/warnings. Additional Weather
Pack не активировать до подтверждения, что установленная base version совместима
с требованием `3.0.7a+`.

После ручного импорта нужно передать Codex:

- подтверждение фактического пути vendor root внутри `Assets`;
- полный результат компиляции и ошибки/предупреждения Unity Console;
- подтверждение, что sample scenes не добавлены в Build Settings;
- подтверждение, что vendor source не редактировался.

После этого Milestone 07A был возобновлён и завершён с exact installation/API audit.

## Что намеренно не выполнялось на момент исторического стопа

- автоматическое копирование или установка Enviro;
- изменение vendor source, shaders, asmdefs или `.meta`;
- установка URP ради vendor dependency;
- создание Enviro adapter, WeatherLab или smoke states;
- изменение Bootstrap environment owners;
- запуск 07B/07C.
