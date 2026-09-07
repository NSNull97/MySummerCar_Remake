@echo off
setlocal
set "SaveMasterExe=%~dp0..\..\Builds\SaveMaster\My Summer Remake Save Master.exe"
if not exist "%SaveMasterExe%" (
  echo Save Master is not built. Run Tools\SaveMaster\Build.ps1 first.
  pause
  exit /b 1
)
start "" "%SaveMasterExe%" %*
