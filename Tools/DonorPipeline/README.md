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
