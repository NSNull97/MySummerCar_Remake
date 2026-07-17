# Enviro 3 — post-import compatibility blocker

Дата проверки: 2026-07-17

> Примечание 07B: приведённый ниже fingerprint — исторический post-import
> срез 07A. Актуальный canonical baseline, причина контролируемой миграции и
> повторный runtime/preflight proof находятся в
> `ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md` (`538 / 305967931 / 8e376fa2…`).

Статус: **RESOLVED 2026-07-17 — APPROVED URP COMPATIBILITY DEPENDENCY INSTALLED**

## Закрытие blocker

Пользователь явно одобрил безопасную remediation. Официальный Unity package
`com.unity.render-pipelines.universal: 17.3.0` добавлен только как compile-time
dependency для безусловно импортируемых URP-шейдеров Enviro. Активные Graphics
и Quality render pipeline assets остаются HDRP; `ENVIRO_URP` не добавлен.

Повторная batch-компиляция завершилась с кодом `0`: оба missing-include shader
errors исчезли. Для Standalone добавлен `ENVIRO_HDRP`, а
`Enviro.EnviroHDRPRenderer, Enviro3.Runtime` зарегистрирован в HDRP списке
`Before Transparent` (vendor documentation: `After Opaque and Sky`). Vendor
файлы не патчились. Остаются только задокументированные предупреждения
устаревших Unity API внутри локальной поставки Enviro.

> Исторический снимок: разделы ниже фиксируют состояние до согласования и
> выполнения remediation. Они сохранены как evidence и не описывают текущее
> состояние проекта.

## Итог диагностики

Основной Enviro вручную импортирован в ожидаемый vendor path:

`Assets/Enviro 3 - Sky and Weather`

C# assemblies `Enviro3.Runtime` и `Enviro3.Editor` собраны. Импорт автоматически
добавил `ENVIRO_3`, но ещё не добавил `ENVIRO_HDRP`. Bootstrap, Build Settings,
Graphics/Quality pipeline assets, HDRP Global Settings и package manifest не
изменились. Enviro manager в active production scene не создавался.

Unity Console содержит obsolete-API warnings vendor-кода и две блокирующие
shader import errors:

1. `EnviroBlitThroughURP.shader` не находит
   `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`.
2. `EnviroVolumetricsURP.shader` не находит URP `Core.hlsl`/`Lighting.hlsl`.

Оба include безусловно находятся в vendor URP-only shaders. ShaderImporter
обрабатывает их независимо от `ENVIRO_URP`, поэтому добавление только
`ENVIRO_HDRP` ошибки не устраняет.

## Допустимая remediation

При действующих правилах нельзя:

- исправлять, удалять, перемещать или переименовывать vendor shaders;
- менять vendor asmdefs;
- создавать fake package/include files;
- скрывать shader error через build stripping или `.gitignore`.

Единственный подтверждённый локальный способ закрыть missing-include errors —
добавить официальный Unity package:

`com.unity.render-pipelines.universal: 17.3.0`

Точная версия уже входит в установленный Unity `6000.3.11f1` и совпадает с
проектными HDRP/Core RP `17.3.0`. Это compatibility-only dependency:

- текущие Graphics и все Quality tiers продолжают ссылаться на HDRP assets;
- URP asset не назначается;
- `ENVIRO_URP` не добавляется;
- позже включается только `ENVIRO_HDRP`;
- vendor shader stripper исключает URP variants, когда `ENVIRO_URP` отсутствует.

Добавление package dependency требует отдельного явного согласования
пользователя. До него WeatherLab и Enviro adapter не создаются.

## Vendor baseline после импорта

Оплачиваемый vendor root исключён из Git и остаётся локальной dependency.
Project-owned docs, integration code, bindings и hashes будут версионироваться
отдельно.

- file count: `538`;
- total bytes: `305967970`;
- deterministic manifest fingerprint SHA-256:
  `9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3`;
- `version.txt` SHA-256:
  `f4aee13d2017b552da61b5109b46bbbaa2a2592a99f4c03c9bf854af3c92fcc7`;
- runtime asmdef SHA-256:
  `5794678280e876518e20f038024992ef36ceae834c0a0ddf45766736c893cc82`;
- editor asmdef SHA-256:
  `701251f3a2184ca1ca01f06486c49c124cea29f7aff0ff814994c515aa10f686`.

Внешний основной каталог после ручного импорта больше не существует, поэтому
полный pre-import fingerprint недоступен. Совпали исходные и импортированные
file count, total bytes и ранее снятые hashes ключевых файлов; это не заменяет
полный before/after proof. Текущий fingerprint является authoritative baseline
для дальнейшей проверки zero vendor changes внутри 07A.

Additional Weather Pack пока не импортирован и не блокирует базовый 07A.

## Выполненная проверка после согласования

1. URP `17.3.0` закреплён в `Packages/manifest.json` и lock-файле.
2. Package Manager resolve/reimport завершён.
3. Missing-include и новые shader errors отсутствуют.
4. Graphics/Quality остаются HDRP, `ENVIRO_URP` отсутствует.
5. Включён только Enviro HDRP support.
6. `Enviro.EnviroHDRPRenderer` зарегистрирован в HDRP Custom Post Process list.
7. Compile, standalone preflight и vendor fingerprint повторно проверены.
