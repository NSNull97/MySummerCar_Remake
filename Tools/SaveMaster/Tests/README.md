# Save Master regression harness

No test framework, NuGet package, Unity runtime or original donor game is needed
for the normal suite. It runs with the .NET 9 SDK:

```powershell
dotnet run --project Tools/SaveMaster/Tests/SaveMaster.Tests.csproj --configuration Release
```

The 57 regression scenarios cover the Unity-produced checksum fixtures, strict
JSON parsing, read-only unsupported versions, typed edits, semantic validators,
unknown-data preservation, snapshots, undo/redo, conflict refusal, byte-exact
backups, collision-free save-as, vehicle fasteners/group latches, wiring,
transaction-consistent balance adjustments, and epoch-consistent time edits.
The harness creates a unique temporary directory and only deletes that directory
after checking its resolved location.

To reproduce native codec compatibility with the locally installed pinned Unity
editor (read from ignored `Config/DonorPaths.local.json`, or pass `-UnityEditor`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/SaveMaster/Tests/Verify-UnityCodec.ps1 -Mode All
```

This generates 264 synthetic fixtures in an isolated Unity project under ignored
`Artifacts/SaveMaster/CodecCompatibility`, edits copies with Save Master, then
loads and serializes the copies through the original project-owned
`MSC.Save.SaveDocumentCodec`. The main Unity project, game Assets, actual user
save directories, and donor installation are not opened for modification.
The probe project has only the four project-owned save-codec source files and a
small Editor entry point; the only Unity package requested is the built-in JSON
serialization module.

Optional commands for local real-save verification:

```powershell
dotnet run --project Tools/SaveMaster/Tests/SaveMaster.Tests.csproj --configuration Release -- --inspect-directory SAVE_DIRECTORY
dotnet run --project Tools/SaveMaster/Tests/SaveMaster.Tests.csproj --configuration Release -- --roundtrip-native SAVE_DIRECTORY ARTIFACT_COPY_DIRECTORY
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/SaveMaster/Tests/Verify-UnityCodec.ps1 -Mode Verify -InputDirectory ARTIFACT_COPY_DIRECTORY
```

`--inspect-directory` only reads files. `--roundtrip-native` changes the player X
coordinate by 0.125 in newly named copies and verifies the originals remain
byte-identical. Use an ignored artifact directory for copies of real progress.
Neither check loads game scenes or substitutes for a gameplay restore test.

Executed on 2026-09-05: .NET regression 56/56; Unity 6000.3.11f1 synthetic edited
fixture round-trip 264/264; three local v17/v17/v16 slots read without modification,
validated with zero editor errors, and their edited copies passed original Unity
codec verification 3/3. No donor content or real user progress is stored in these
tracked fixtures.
