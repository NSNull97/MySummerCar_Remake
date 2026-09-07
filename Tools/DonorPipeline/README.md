# Controlled mesh conversion

`ConvertGlbToObj.py` converts one explicitly reviewed AssetRipper GLB mesh from external
`raw` staging into one OBJ under external `normalized` staging. It does not scan or export
the full donor project.

Install the pinned dependency outside the repository, then expose it through
`PYTHONPATH`:

```powershell
python -m pip install --target <external-tools> -r Tools/DonorPipeline/requirements.txt
$env:PYTHONPATH = '<external-tools>'
python Tools/DonorPipeline/ConvertGlbToObj.py <input.glb> <output.obj> --metadata <output.metadata.json>
```

The generated sidecar records the input/output SHA-256, bounds, mesh counts and tool
versions. Raw and normalized outputs remain outside Git.

## Read-only Unity YAML selection

`Read-UnityYamlObject.ps1` reads 1–32 explicitly selected object IDs using `rg`
byte offsets. It accepts a literal source path, enforces a per-object size limit,
and returns `ObjectId`, `TypeId`, `ByteOffset`, `SourcePath` and `Body` without
writing files or executing donor code. Resolve the source from the ignored
local configuration. Do not commit returned donor bodies or use these IDs as
runtime entity identities. Binary PlayMaker action fields still require separate
review; this tool is a YAML selector, not an FSM runtime or a semantic decoder.

```powershell
$auditPaths = Get-Content Config/DonorPaths.local.json -Raw | ConvertFrom-Json
$auditScene = Join-Path $auditPaths.DonorStagingDirectory 'raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity'
./Tools/DonorPipeline/Read-UnityYamlObject.ps1 -SourcePath $auditScene -ObjectId 94487,95540 |
    Select-Object ObjectId,TypeId,ByteOffset
```
