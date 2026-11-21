param(
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe',
    [switch]$AfterCommit,
    [switch]$InstallHook
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $root
try {
    if ($InstallHook) {
        $configuredHookPath = git config --get core.hooksPath
        if ($configuredHookPath) { throw 'Use the existing Git hooks path to call this script with -AfterCommit.' }
        $hooks = git rev-parse --path-format=absolute --git-path hooks
        if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the local Git hooks directory.' }
        $hook = Join-Path $hooks 'post-commit'
        New-Item -ItemType Directory -Force -Path $hooks | Out-Null
        $content = '#!/bin/sh' + [char]10 +
            'powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Level/Update-MapScreenshots.ps1 -AfterCommit' + [char]10
        if (Test-Path -LiteralPath $hook) {
            if ([IO.File]::ReadAllText($hook) -eq $content) { return }
            throw 'The local post-commit hook already exists. Add the screenshot script call to it.'
        }
        [IO.File]::WriteAllText($hook, $content, [Text.UTF8Encoding]::new($false))
        return
    }

    if ($AfterCommit) {
        $mapPaths = @('Assets/Game/Level','Assets/Game/Gameplay/Scenes/V3Gameplay.unity','Assets/Settings','ProjectSettings',
            'Assets/Terrain1_Test','Assets/Terrain2_Test','Assets/Terrain3_Test','Assets/Terrain4_Test')
        $changed = git diff-tree --root -m --no-commit-id --name-only -r HEAD -- @mapPaths
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the latest Git commit.' }
        if (!$changed -and (Test-Path -LiteralPath 'Screenshots')) {
            # A map commit may have deferred capture while a following documentation commit was pending.
            $last = Get-ChildItem -LiteralPath 'Screenshots' -File -Filter 'World_top_v3_*.png' |
                Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            if ($last.Name -match '^World_top_v3_([0-9a-f]{7})\.png$') {
                $changed = git diff --name-only $Matches[1] HEAD -- @mapPaths
                if ($LASTEXITCODE -eq 0 -and !$changed) { return }
            }
        }
    }

    $editor = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine -notmatch '-name.*AssetImport' -and
        $_.CommandLine.Replace('\','/').IndexOf($root.Replace('\','/'), [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    $evidence = Join-Path $root 'Docs/Development/Level/V3'
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    if ($editor) {
        if (!$AfterCommit) { throw 'Use Tools > Twin Edge > Capture Latest Map Screenshots in the open Editor.' }
        $commit = git rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the latest Git commit.' }
        Set-Content -LiteralPath (Join-Path $evidence 'LatestScreenshots.request') -Encoding ascii -Value $commit
        Write-Output 'Queued the committed map capture in the open Unity Editor.'
        return
    }
    $arguments = @('-batchmode','-force-d3d11','-quit','-projectPath',('"' + $root + '"'),
        '-executeMethod','TwinEdge.Levels.Editor.LatestMapScreenshots.Capture','-logFile',('"' + (Join-Path $evidence 'LatestScreenshotCapture.log') + '"'))
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Screenshot capture failed. See Docs/Development/Level/V3/LatestScreenshotCapture.log.' }
}
finally {
    Pop-Location
}
