# Wwise setup

Status: **Installed / AuthoringAndRuntimeAutomatedValidated**  
Audit date: **2026-07-18**

## Installed versions

| Component | Verified version |
|---|---|
| Wwise Authoring and SDK | `2025.1.9.9197` |
| Wwise Unity Integration bundle | `2025.1.9.4241` |
| Unity | `6000.3.11f1` |
| Target | Windows x64 |

The official integration is under `Assets/Wwise`; versions are recorded in
`Assets/Wwise/Version.txt`. The audited local SDK is
`C:\Audiokinetic\Wwise_2025.1.9.9197`; that machine path is setup evidence only
and is not hard-coded in runtime C#. Project code did not modify vendor files.
The repository keeps the official integration source and Windows x64 native
plugins needed by the first supported target. Local help payloads and unrelated
Mac/Windows-x86 native binaries are excluded from Git.

Official release information:
<https://www.audiokinetic.com/en/public-library/2025.1.9_9197/?id=pg_releasenotes_2025_1_9.html&source=Unity>.

## Project and paths

| Purpose | Path | Policy |
|---|---|---|
| Wwise project | `MySummerCar_Remake_WwiseProject/MySummerCar_Remake_WwiseProject.wproj` | project authoring data |
| Generated source banks | `MySummerCar_Remake_WwiseProject/GeneratedSoundBanks/Windows/` | ignored |
| Unity runtime bank copy | `Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/` | ignored |
| Wwise caches/settings | `.cache*`, local `.wsettings`, validation cache | ignored |
| temporary donor originals | `MySummerCar_Remake_WwiseProject/Originals/**/TempDonorPrototype/` | private/local/ignored |

`Assets/WwiseSettings.xml` points the Unity integration at this project and
output. Runtime and private Development-build bank files were hash-compared to
the source generated banks.

## Verified authoring topology

WAAPI/disk verification in `Logs/M08_WwiseAuthoring.log` records:

- 52 distinct events; runtime aliases are not duplicated;
- 32 Game Parameters/RTPCs;
- 4 switch groups;
- 3 state groups;
- 5 user banks plus mandatory Wwise `Init.bnk`;
- 6 mixer buses;
- exactly 6 mixer Volume RTPC curves;
- 7 routed Actor-Mixer roots;
- exact child OutputBus routing `52/52`;
- player footsteps: 10 sounds, 5 random pairs, `9/9` switch assignments and a
  valid Play action;
- wind shelter curve present, reaching `-24 dB`;
- generated output: 30 embedded media objects, 0 loose WEM.

The mixer curves map `MSC_Mixer_Master`, `Vehicle`, `Effects`, `Ambience`,
`Music` and `UI` from normalized `0`/`1` to `-96 dB`/`0 dB`. Vehicle routes to
Vehicle; Weather and World to Ambience; Interaction to Effects; Music to Music;
UI to UI.

## Generated Windows banks

| Bank | Bytes | SHA-256 |
|---|---:|---|
| `Init.bnk` | 1,877 | `86CA0EFC46DC6878DAB0619F8611B76B3BA939980BBF5B566352D1ED90EEFBF9` |
| `MSC_Interaction.bnk` | 1,179,302 | `009D7A6E9EFCA2E3750EA8B07412F10203798256A26860DF3A659CCCED64B5D8` |
| `MSC_UI.bnk` | 145 | `F282D401C047128722E5A902FB3F35CFE3E3A23B65529492FC1A1FD9E074E1E1` |
| `MSC_Vehicle.bnk` | 1,992,969 | `39A777154F4FE0D6C7A2DD5A978A35C32D45EFBF14F3ACF3F1313321350CF2D8` |
| `MSC_Weather.bnk` | 12,554,129 | `1B598E9165D4521718CE8A03D1D2ACD92C4C1A723F6CED11B9DD50ED3D8D44B3` |
| `MSC_World.bnk` | 10,387,529 | `273F47B925EBFF41D86B3CB6594F860469C97136D9485BD0CFB1F2536D530B5E` |
| **Total** | **26,115,951** | per-file hashes above |

Generated banks/caches and temporary originals remain outside Git. `MSC_UI` is
an intentionally empty/reserved 145-byte bank; later UI/menu sounds are a
different newly authored content set.

## Runtime and build evidence

- official Wwise adapter EditMode: **3/3 PASS**;
- official Wwise adapter PlayMode: **3/3 PASS**;
- live official Bootstrap PlayMode with `-wwiseEnableWithNoGraphics`: **7/7
  PASS**, 6 loaded banks and zero missing banks;
- user runtime confirmation on 2026-07-18: backend **Wwise**, banks **6**,
  missing banks **0**;
- official Unity bank synchronization: **PASS**, all six source/runtime
  SHA-256 values match (`Logs/M08_Remediation_BankSync_Final.log`);
- live-Wwise vehicle playtest: **2/2 PASS**;
- post-remediation private Windows x64 Development build: **PASS**, Unity report
  `844,040,197 B`, folder `844,257,747 B`;
- built-player bank hashes match the generated source banks;
- native Development Player boot records `Sound engine initialized
  successfully` in `Logs/M08_Remediation_PlayerSmoke.log`; it is stopped after
  the bounded initialization smoke.

The official adapter is isolated under `Assets/Game/Audio/Wwise`; gameplay
continues to depend only on project contracts. Missing integration/bank tests
exercise explicit failure/fallback behaviour without enabling Enviro audio.

## Regeneration procedure

1. Run `Tools/Audio/ConfigureWwisePrototype.ps1`; it updates the local project,
   validates the topology and generates Windows SoundBanks through WAAPI.
2. Verify the six `.bnk` names, sizes/hashes or consciously update this record.
3. Run `Tools/MSC Remake/Audio/Synchronize Windows SoundBanks` in Unity. The
   project synchronizer delegates to the official Wwise integration and compares
   SHA-256 for all six source/runtime files; never copy banks into a tracked
   folder.
4. Run the Wwise map/authoring validators, live Bootstrap smoke and the M08
   vehicle-audio PlayMode gate before a Development build.

The official Wwise build postprocessor removes its ignored `StreamingAssets`
mirror after packaging. This is expected: the packaged player retains the bank
copies. Run step 3 again after a build when the banks are needed for the next
Editor Play Mode session. The final post-build synchronization and the packaged
player were both hash-checked against the generated source banks.

## Donor content boundary

Each private original-game prototype clip has an individual source hash and
ledger row. Originals/conversions/banks are local ignored
`TemporaryDirectImport`, non-distributable and not architectural dependencies.
They are not final production content. Final gameplay/world/weather audio is
newly authored and mixed; final UI/menu audio is a separate later set.

## Remaining manual evidence

Headless Unity suspends the Wwise output thread. The user accepted the bounded
Milestone 08 baseline on 2026-07-18; final mix judgement, fine-grained zone
tuning, output-device behaviour and Wwise Profiler audio-thread CPU/
virtualization/starvation remain later polishing tasks. Automated authoring,
loading, cleanup, packaging and native initialization are validated.
