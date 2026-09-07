# Tools

Run PowerShell from the Unity project root.

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Tools\Validate-Environment.ps1
.\Tools\Create-Donor-Staging.ps1
```

`Install-StarterKit.ps1` is useful when the archive was extracted somewhere other than the Unity project root.

The scripts do not modify the original game. Review any script before execution, especially after future edits.

`Tools/DonorPipeline/ConvertGlbToObj.py` is the pinned, single-file controlled mesh converter used by Milestone 2. Its dependency and operation are documented in `Tools/DonorPipeline/README.md`; raw and normalized outputs stay in external donor staging.

## My Summer Remake Save Master

Standalone Windows native-save editor: `Tools/SaveMaster/Build.ps1` builds and tests it into `Builds/SaveMaster`; run `My Summer Remake Save Master.exe` from that folder. See `Tools/SaveMaster/README_RU.md` for usage and `Docs/Save/SAVE_MASTER.md` for validation evidence. The editor has no runtime dependency on Unity or the donor and does not alter the game's save schema.
