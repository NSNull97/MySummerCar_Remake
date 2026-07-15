# Карта источников reference capture

## Зафиксированный baseline

- Donor build: Steam build ID `20171487`.
- Donor Unity: `5.0.0f4`.
- Установка: локально модифицирована; данные не объявляются чистым stock baseline.
- Доступ: только read-only inspection. Donor files и saves не изменялись.
- Raw captures и donor-derived payload: только во внешнем donor staging/reference storage, вне Git.

## Источники

| Source ID | Логический источник | SHA-256 | Использование |
|---|---|---|---|
| `b4c800a2b1afe2fbffaec16f127cff5b` | `mysummercar_Data/level2` | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` | Scene transforms, player settings, vehicle hierarchy |
| `dd1f1d55df54bc87a1882f364e30ed21` | `mysummercar_Data/sharedassets3.assets` | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` | Garage/world mesh metadata |
| `8b127f80ac601d8cf9b781c234ca4408` | `mysummercar_Data/sharedassets1.assets` | `8f0a0984f4e55229ecaebb57ef931b052f56998b9569a32e013780aa9dc78e02` | Rear drum controlled mesh metadata |
| `95c9fb57bfb3fc7cb95088d2ef83298b` | `mysummercar_Data/sharedassets3.resource` | `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b` | World resource-stream provenance; no payload in Git |
| `dccf28be1d702773b59b4db29a0084d0` | External AssetRipper `GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` | Reviewed static component and transform inspection |
| `e5398e3eeb622a7ee7eea3ddac195ba9` | `Docs/ReferenceCapture` | n/a | Project-owned procedures and explicit missing fixtures |
| `e2548f9e04123f5f1f9f78d2b7012359` | External user video `Videos/Captures/My Summer Car 2026-07-14 17-06-05.mp4` | `adffd52e9dce4171c30b969caf1102bd7ef8af9f0686ce934f2ea0ae675689af` | Diagnostic wrench-11 rejection; `0` valid clean trials |
| `458fd18c1865408b648d642f03550dd3` | External user video `Videos/Captures/My Summer Car 2026-07-14 18-52-48.mp4` | `86ad948bda1450fb8d2cf32583b51d0c2bc55ccef5aad9f428da3bc38e8e3c84` | Wrench-14 runtime progression and three clean rear-drum repetitions |
| `d0f9937c8a5e1b696b104d3c3099e73b` | User runtime attestation report `04B_VEHICLE_ASSEMBLY_BLOCKED_REMOVAL_ATTESTATION_20260714.md` | `380f3ca4183f52e956939a713e01e892b998fa4717ee845d86074fb9b871ca66` | Installed rear-left wheel blocks drum removal; `Medium` confidence |

## Evidence roots

`Project` разрешается относительно repository root. `DonorStaging` разрешается относительно `donorStaging` из `Config/DonorPaths.local.json`. `UserCapture` обозначает предоставленный пользователем external path и намеренно не разрешается через runtime/editor root mapping. Machine-specific absolute path в базе не хранится.

Ключевые внешние evidence:

- `normalized/world/milestone-04a1/WorldObjectPlacements.csv` — 36 045 placement records;
- `normalized/world/milestone-04a1/WorldMeshManifest.csv` — resolved mesh metadata;
- `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity` — read-only serialized representation;
- `normalized/controlled-proof/environment/garage_shed_roof_reference.metadata.json`;
- `normalized/controlled-proof/vehicle/drum_brake_rear_reference.metadata.json`.
- `Videos/Captures/My Summer Car 2026-07-14 17-06-05.mp4` — diagnostic-only external video, SHA-256 зафиксирован в базе; файл не копировался в Git.
- `Videos/Captures/My Summer Car 2026-07-14 18-52-48.mp4` — external runtime video с тремя clean trials, SHA-256 зафиксирован в базе; файл не копировался в Git.
- `Docs/ReferenceCapture/Sessions/04B_VEHICLE_ASSEMBLY_BLOCKED_REMOVAL_ATTESTATION_20260714.md` — durable project record пользовательского runtime confirmation; отдельного blocked-case видео нет.

Проектные evidence:

- `Docs/WorldTransfer/LANDMARK_FIXTURES.csv`;
- `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json`;
- manual guide и семь category checklists в этой директории.

## Ограничения интерпретации

- AssetRipper scene — вспомогательное представление, а не новый production project.
- GameObject name `557kg, 248` не считается измерением массы. Подтверждён только `Rigidbody.mass = 389 kg`, без curb-state claim.
- `datsun_body` AABB — envelope body mesh, не полностью собранного автомобиля.
- `tire_stock` radius — кандидат по имени, не доказанная установленная шина.
- `BoltPM` position сам по себе не доказывает tool size или turn semantics. Serialized player tool-check связывает `localScale.x=1.4` с ключом `14`, а runtime-видео подтверждает три complete progressions. Контракт дискретный `0..8`; physical torque не измерялся и не требуется для воспроизведения donor stage semantics.
- Wheel-installed blocked removal подтверждён user attestation без frame-addressable media; confidence намеренно ограничен `Medium`.
- `CharacterMotor` serialized values не доказывают runtime sprint/crouch state.
- Managed code names и FSM labels не подменяют configuration values или runtime capture.

## Будущие runtime evidence

Каждая capture session получает ID и строку в `CAPTURE_SESSION_LOG.csv`. Видео, screenshots и audio называются по manual guide и остаются вне Git. В базу импортируются только observation, normalized value, uncertainty, hashes/logical paths и provenance. Если состояние или метод неоднозначны, запись остаётся `NeedsReview` или `Missing`.
