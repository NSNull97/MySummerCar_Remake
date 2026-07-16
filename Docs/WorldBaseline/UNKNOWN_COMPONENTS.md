# Неизвестные и неподдерживаемые serialized components

Policy version: `06B1.4`

Source inventory:
`normalized/world/milestone-04a1/WorldUnsupportedObjects.csv`

Source SHA-256:
`8a699d6e669dd4231344bde37c0f888ad048858d270146d4932697c9277916f7`

## Решение

В полном normalized `GAME` inventory обнаружены 37 serialized class IDs, для
которых extractor 04A1 не создавал отдельный geometry processor. Они сохраняются
как числовые IDs и counts для provenance, но все исключаются из canonical
runtime baseline.

Human-readable названия ниже являются только справочной расшифровкой Unity
ClassID. Sanitation принимает решение по explicit whitelist и numeric source
metadata, а не по предполагаемому имени типа.

## Полный список

Counts относятся ко всей 36 045-record source scene, а не только к 3 842
eligible world records.

| Class ID | Source count | Справочная семья | 06B1 disposition |
|---:|---:|---|---|
| 12 | 109 | Legacy particle animator | Excluded / MetadataOnly |
| 15 | 109 | Legacy particle emitter | Excluded / MetadataOnly |
| 20 | 46 | Camera | Excluded / MetadataOnly |
| 26 | 109 | Legacy particle renderer | Excluded / MetadataOnly |
| 45 | 5 | Skybox | Excluded / MetadataOnly |
| 54 | 2 104 | Rigidbody | Excluded / MetadataOnly |
| 59 | 23 | HingeJoint | Excluded / MetadataOnly |
| 81 | 3 | AudioListener | Excluded / MetadataOnly |
| 82 | 1 835 | AudioSource | Excluded / MetadataOnly |
| 92 | 2 | Legacy GUI layer | Excluded / MetadataOnly |
| 102 | 2 160 | TextMesh | Excluded / MetadataOnly |
| 104 | 1 | RenderSettings | Excluded / MetadataOnly |
| 108 | 375 | Light | Excluded / MetadataOnly |
| 111 | 466 | Animation | Excluded / MetadataOnly |
| 114 | 10 288 | MonoBehaviour / donor scripts and FSM owners | Excluded / MetadataOnly |
| 119 | 34 | Projector | Excluded / MetadataOnly |
| 124 | 7 | Flare layer | Excluded / MetadataOnly |
| 127 | 1 | Legacy visual manager; exact type not relied upon | Excluded / MetadataOnly |
| 131 | 2 | Legacy GUI texture | Excluded / MetadataOnly |
| 138 | 96 | FixedJoint | Excluded / MetadataOnly |
| 143 | 1 | CharacterController | Excluded / MetadataOnly |
| 144 | 694 | CharacterJoint | Excluded / MetadataOnly |
| 145 | 4 | SpringJoint | Excluded / MetadataOnly |
| 146 | 2 | WheelCollider | Excluded / MetadataOnly |
| 153 | 15 | ConfigurableJoint | Excluded / MetadataOnly |
| 157 | 1 | LightmapSettings | Excluded / MetadataOnly |
| 164 | 141 | AudioReverbFilter | Excluded / MetadataOnly |
| 165 | 52 | AudioHighPassFilter | Excluded / MetadataOnly |
| 166 | 1 | AudioChorusFilter | Excluded / MetadataOnly |
| 168 | 1 | AudioEchoFilter | Excluded / MetadataOnly |
| 169 | 93 | AudioLowPassFilter | Excluded / MetadataOnly |
| 170 | 816 | AudioDistortionFilter | Excluded / MetadataOnly |
| 183 | 7 | Cloth | Excluded / MetadataOnly |
| 198 | 46 | ParticleSystem | Excluded / MetadataOnly |
| 199 | 46 | ParticleSystemRenderer | Excluded / MetadataOnly |
| 205 | 165 | LODGroup | Excluded / MetadataOnly |
| 215 | 3 | ReflectionProbe | Excluded / MetadataOnly |

## Что встречается в eligible subset

В 3 842 `ReferenceWorldEligible` records присутствуют только следующие
не-whitelist source families:

| Class ID | Eligible records | Решение |
|---:|---:|---|
| 54 Rigidbody | 234 | Excluded |
| 64 MeshCollider | 437 | Excluded; collision deferred |
| 65 BoxCollider | 519 | Excluded; collision deferred |
| 82 AudioSource | 8 | Excluded |
| 102 TextMesh | 117 | Excluded |
| 111 Animation | 10 | Excluded |
| 114 MonoBehaviour | 559 | Excluded |
| 135 SphereCollider | 276 | Excluded; collision deferred |
| 136 CapsuleCollider | 256 | Excluded; collision deferred |
| 137 SkinnedMeshRenderer | 62 | Excluded |
| 138 FixedJoint | 27 | Excluded |
| 144 CharacterJoint | 46 | Excluded |
| 183 Cloth | 7 | Excluded |
| 198 ParticleSystem | 1 | Excluded |
| 199 ParticleSystemRenderer | 1 | Excluded |
| 205 LODGroup | 8 | Excluded |

Class IDs `64`, `65`, `135`, `136` были отдельно нормализованы collider
pipeline и поэтому не входят в 37-row unsupported file, но для runtime
sanitation всё равно являются запрещёнными до 06B2.

## Причины исключения по группам

### Donor code и state machines

Class ID `114` может представлять donor scripts, PlayMaker FSM owners и другие
serialized MonoBehaviour. Ни assembly, ни script reference, ни serialized
payload не переносится. Metadata хранит только numeric component-class
presence.

### Physics и gameplay

Rigidbody, joints, CharacterController и WheelCollider не являются безопасной
статической картой. Их перенос потребовал бы gameplay intent, ownership,
collision-layer и lifecycle audit.

### Camera, lighting, weather и audio

Camera, donor lights, skybox, RenderSettings, reflection probes, AudioListener,
AudioSource и filters исключены. Canonical scene использует только
project-owned neutral development lighting.

### Characters и animation

Animation, CharacterJoint, Cloth и skinned/character hierarchy не входят в
map-only scope. NPC и люди будут реализовываться позднее независимо от donor
runtime components.

### Presentation, которую можно рассмотреть позднее

TextMesh и LODGroup могут содержать полезную presentation-информацию, но они не
разрешены автоматически. Для будущего allowlist update нужны:

- доказанная world-only роль;
- отсутствие donor script dependency;
- deterministic conversion;
- новая policy version;
- component audit и tests;
- ledger/provenance update.

## Acceptance rule

Canonical baseline считается загрязнённым и validation должна завершиться
failure, если в scene появляется любой source component вне explicit whitelist.

Добавление нового разрешённого типа возможно только через отдельное reviewed
изменение policy. Silent allow или blacklist-only filtering запрещены.
