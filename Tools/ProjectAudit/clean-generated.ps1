param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$activeEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object { $_.CommandLine -notmatch '-name.*AssetImport' }
if ($activeEditors) { throw 'Save your scenes and close Unity normally before cleanup. Undo must end before deleting candidates.' }
$reportDirectory = Join-Path $projectRoot 'Docs/Development/Level/V3'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$cleanupProcess = Start-Process -FilePath $Unity -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod', 'TwinEdge.Levels.Editor.ValleyAssetLifecycleV3.CleanFreshSession', '-logFile', ('"' + (Join-Path $reportDirectory 'CleanupUnity.log') + '"')) -WindowStyle Hidden -PassThru -Wait
if ($cleanupProcess.ExitCode -ne 0) { throw "Unity cleanup failed: $($cleanupProcess.ExitCode); inspect CleanupUnity.log" }
