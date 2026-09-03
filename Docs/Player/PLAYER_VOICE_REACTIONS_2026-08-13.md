# Player voice reactions — bounded out-of-sequence pass

Date: 2026-08-13
Locked donor revision: `msc-world-baseline-04a1.1-c3f2f337`
Locked `GAME.unity` SHA-256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`

## Requested outcome

- manual swear input with a baseline binding and normal settings rebind support;
- a voice line while the accepted middle-finger viewmodel gesture plays;
- event reactions, initially insufficient funds;
- automatic swearing at high stress;
- no redesign of the accepted 08A settings presentation and no donor runtime dependency.

## Frozen donor evidence

| Evidence | Observed rule | Transfer |
|---|---|---|
| Setup component `&108669` | `Swear = N`; `Finger = M` | `ConfigurationTransferred` |
| SpeakDatabase Speech FSM `&110839` | `Swear` input and `SWEARING` enter the Swear state; a random value in `[0,16)` selects the line; MasterAudio group `Swearing` plays; `PlayerStress -= 0.5`; the state waits one real second | `BehavioralReference;ConfigurationTransferred` |
| PlayerFunctions FSM `&111301` | `Finger` enters through `MIDDLEFINGER`; a random value in `[0,11)` selects MasterAudio group `Fuck`; animation `middlefinger` plays and nearby NPC logic receives `FINGER` | `BehavioralReference;ConfigurationTransferred` |
| Simulation FSM `&112251` | the stress state emits `SWEARING` when `PlayerStress` reaches `100`; a later comparison to `200` also exists | `BehavioralReference` |
| MasterAudio group `&105622` | exactly 16 variations, backed by `swear01..swear16` | `TemporaryDirectImport` for private Phase 1 presentation |
| MasterAudio group `&105307`; subtitle table `&107451` | exactly 11 separate middle-finger variations, backed by `fuck01..fuck11`, plus 11 English subtitle entries | `TemporaryDirectImport` for private Phase 1 presentation; subtitle text `ConfigurationTransferred` |
| Insufficient-funds branches in locked `GAME.unity` | player-visible rejection branches select MasterAudio group `Swearing` | `BehavioralReference` |

The project-owned stress model is explicitly clamped to `0..100`. The runtime
therefore uses the evidenced `100` threshold and does not invent an unreachable
`200` range. Manual and high-stress swears apply the evidenced `-0.5` through
`IPlayerNeedsEffectSink`. Middle-finger and insufficient-funds reactions are
presentation-only and do not mutate needs.

## Runtime flow

```text
Player/Swear (default N) -------------------+
Player/MiddleFinger (default M) ------------+--> PlayerVoiceReactionController
Economy TransactionRejected/Insufficient ---+          |
PlayerNeedsSnapshot.Stress == 100 ----------+          +--> one-second policy lock
                                                        +--> swear/event/stress: one of 16 `player.swear` IDs
                                                        +--> middle finger: one of 11 `player.finger` IDs
                                                        +--> IAudioBackend / fallback
                                                        +--> existing status/subtitle surface

manual or high-stress decision --> IPlayerNeedsEffectSink(stress: -0.5)
middle-finger input ------------> existing FirstPersonLifeActionPresenter gesture
```

`PlayerVoiceReactionPolicy` is pure and owns only the transient one-second lock,
high-stress rearm and non-repeating presentation selection. The authoritative
wallet and needs state remain in `EconomyRuntime` and `PlayerNeedsRuntime`.

The optional `IEconomyTransactionFeedbackSource` does not change
`IEconomyTransactionService`, its consumers or the save schema. Rejected
transactions publish a receipt after validation; the player controller reacts
only to `InsufficientFunds`.

## Controls and settings

`M4_Player.inputactions` adds `Player/Swear` with the donor-evidenced default
`<Keyboard>/n`. No speculative gamepad default was assigned because every
existing D-pad direction already has an accepted action.

The accepted Controls panel dimensions, glass, columns and standard actions are
unchanged. Its binding rows now live in a clipped vertical `ScrollRect`, allowing
the existing saved rebind transaction to expose the active walking, interaction,
gesture, swear, pause and vehicle bindings. Reset still removes overrides and
restores the canonical action assets.

## Temporary private audio

`Phase1PlayerVoiceManifest.json` records both independent donor catalogs
(`swear01..swear16` and `fuck01..fuck11`) and, for every variant:

- donor variation component, GameObject and AudioSource file IDs;
- donor AudioClip GUID;
- metadata and PCM-resource relative paths and SHA-256 hashes;
- donor serialized length and measured PCM duration;
- project-owned stable audio event ID;
- deterministic generated GUID and Phase 2 replacement key.

`Phase1PlayerVoiceImporter` verifies the locked scene hash, both file hashes,
AudioClip GUID/metadata, PCM container format (`mono`, `22050 Hz`, `16-bit`) and
duration before copying only the WAV resource into ignored
`RuntimeBaseline/Audio/PlayerVoice`. It creates a 27-event 2D Unity fallback
library under
`Resources/Phase1PlayerVoice`. Donor AudioClip assets, AudioSources, MasterAudio
objects, FSMs and controllers are never runtime content.

The fallback library is loaded through the optional
`IAudioSupplementalContentBackend`; gameplay continues to post only stable
`AudioEventId` values. Preferred Wwise mappings can replace it without changing
the reactions.

## Save and compatibility

- no public Player, Economy, Needs, UI or Save API was removed or renamed;
- the new economy feedback interface is optional and additive;
- canonical input action/binding GUIDs are stable and settings override JSON
  continues to target GUIDs;
- stress relief is captured by the existing `player.needs` domain;
- rejected purchases still produce no ledger record;
- line selection and the one-second presentation lock are deliberately
  non-persistent because they carry no donor-evidenced durable state;
- the accepted middle-finger viewmodel is reused unchanged.

## Executed validation

| Run | Result |
|---|---|
| Previous Swearing-only baseline: Unity 6000.3.11f1 script compilation | Passed, exit `0` |
| Previous Swearing-only baseline: private voice build | Passed, 16 clips / 16 events, exit `0`; superseded by the corrected 27-event manifest |
| Corrected donor source audit | Passed: locked scene; exact ordered `Swearing` 16 / `Fuck` 11 / Finger subtitle 11 component contents; 27 metadata and 27 PCM hashes; all PCM mono / 22050 Hz / 16-bit |
| Corrected JSON/CSV structural validation | Passed: 27 unique events/GUIDs; parity matrix 19 columns and ledger 11 columns with zero malformed rows |
| Corrected runtime/importer and focused test-source compilation | Passed with zero errors against the current Unity-generated/source assemblies and installed Unity 6000.3.11f1 assemblies |
| Corrected standalone policy/catalog run | `5/5` passed: 16 Swearing variants remain separate from 11 exact Finger variants |
| Previous focused Unity automation | Policy/economy/input EditMode `10/10`, controller PlayMode `2/2`, Controls PlayMode `5/5`, Bootstrap `1/1` (`44.03 s`) |
| Corrected 27-clip importer and focused Unity rerun | Pending while the interactive Unity Editor is in Play Mode; do not run a second Unity process against the project |

An earlier broad UI source-contract run passed all affected assertions but
reported one unrelated pre-existing logo-size failure (`expected 600`). The
focused canonical-input rerun passed and this pass did not modify the logo.

The previous first Bootstrap test attempt requested gameplay preparation before the
installer's first `Start` frame and failed its lifecycle precondition. The test
was corrected to wait one frame; the complete rerun reached ready state, found
the initialized reaction controller and resolved a real private fallback event.

## Manual acceptance still required

1. Start a prepared gameplay session from `Assets/Game/Bootstrap/Bootstrap.unity`.
2. Press `N`; confirm one Finnish voice line, a localized subtitle/status line,
   and a `0.5` stress decrease.
3. Press `M`; confirm the accepted middle-finger animation and one of the
   dedicated `fuck01..fuck11` lines, with no `swear01..swear16` line and no
   stress decrease.
4. Attempt a purchase above the live balance; confirm the service failure remains
   truthful and one voice reaction plays without a ledger mutation.
5. Set stress just below `100`, let it reach `100`, and confirm one automatic
   reaction; verify it rearms only after stress drops below the threshold.
6. Rebind Swear in Settings > Controls, apply, restart, verify persistence, then
   Reset and confirm `N` returns.

## Open limitations

- production Wwise event/name mappings and bank media are not authored yet;
- the temporary donor voice clips are private Phase 1 presentation and must be
  replaced under `presentation.audio.player-voice.phase2`;
- the donor's 11 English middle-finger subtitle entries are locked exactly;
  the project Russian fallback and final voice/subtitle mix need manual acceptance;
- full stress-rate/modifier parity and every insufficient-funds interaction path
  remain open, so related matrix rows stay `PartiallyImplemented`.
