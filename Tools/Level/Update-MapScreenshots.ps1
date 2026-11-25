param(
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe',
    [switch]$AfterCommit,
    [switch]$InstallHook
)

$ErrorActionPreference = 'Stop'
if ($InstallHook) { throw 'Official captures run after push. Invoke this script directly after verifying origin/main.' }
if ($AfterCommit) { return } # Keep existing local post-commit hooks harmless.

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $root
try {
    $head = git rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve HEAD.' }
    $remote = git ls-remote --exit-code origin refs/heads/main
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the actual remote main.' }
    if (($remote -split '\s+')[0] -ne $head) { throw 'Official capture requires HEAD to match actual remote main.' }
    $changes = git status --porcelain=v1 --untracked-files=all
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the capture worktree.' }
    if ($changes) { throw 'Use a clean checkout of the pushed commit for official capture.' }

    $editor = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine -notmatch '-name.*AssetImport' -and
        $_.CommandLine.Replace('\','/').IndexOf($root.Replace('\','/'), [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    if ($editor) {
        throw 'Close Unity and rerun, or use Tools > Twin Edge > Capture Latest Map Screenshots after checking the open Editor is clean.'
    }
    $evidence = Join-Path $root 'Docs/Development/Level/V3'
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    $arguments = @('-batchmode','-force-d3d11','-quit','-projectPath',('"' + $root + '"'),
        '-executeMethod','TwinEdge.Levels.Editor.LatestMapScreenshots.Capture','-logFile',('"' + (Join-Path $evidence 'LatestScreenshotCapture.log') + '"'))
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Screenshot capture failed. See Docs/Development/Level/V3/LatestScreenshotCapture.log.' }
}
finally {
    Pop-Location
}
