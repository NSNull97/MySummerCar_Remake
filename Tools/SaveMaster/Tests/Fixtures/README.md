These are project-owned synthetic fixtures generated with the remake's original
`MSC.Save.SaveDocumentCodec`, running inside Unity 6000.3.11f1 on 2026-09-05.
They contain no donor content or real user progress. Player DTO field layouts
come from `PlayerPersistenceState.cs` and `PlayerNeedsState.cs`.

Each `.canonical.json` is the exact Unity `JsonUtility.ToJson` input to SHA-256
after domain sorting and clearing `IntegritySha256`. Paired `.mscsave.json`
files include the resulting original-game-codec checksum. The fixtures cover
pretty and compact JSON, integer/decimal/extreme double metadata, Unicode,
emoji, escaped controls, and unknown optional domain data.

`Verify-UnityCodec.ps1 -Mode All` reproduces a larger 264-fixture corpus under
ignored `Artifacts/SaveMaster/CodecCompatibility`, edits copies with Save Master,
and loads the results through the original project codec in isolated Unity.
This verifies envelope compatibility; it does not substitute for a full gameplay
restore test or assert that synthetic partial-domain saves are playable sessions.
