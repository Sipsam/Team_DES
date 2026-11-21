[CmdletBinding(SupportsShouldProcess)]
param([switch]$Apply, [string]$CompletedRun)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $root
try {
    function Assert-GeneratedPath([string]$relative) {
        $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
        $base = $root.TrimEnd('\') + '\'
        if (!$path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes the project: $relative" }
        for ($current = $path; $current -ne $root; $current = Split-Path -Parent $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path: $current" }
        }
        git check-ignore -q -- $relative
        if ($LASTEXITCODE -ne 0 -or (git ls-files -- $relative)) { throw "Path is not ignored and untracked: $relative" }
        return $path
    }

    $legacyBuilds = @('Builds/V3Gameplay','Builds/FullMatch')
    foreach ($relative in $legacyBuilds) {
        if (!(Test-Path -LiteralPath $relative)) { continue }
        $path = Assert-GeneratedPath $relative
        if (!(Test-Path -LiteralPath 'Builds/V3Development/V3Gameplay.exe')) {
            Write-Output "Keep $relative until the replacement Development player succeeds."
            continue
        }
        Write-Output "Legacy build candidate: $relative"
        # Historical folders can contain the only failing player. Review them before removal.
    }

    if ($CompletedRun) {
        if ($CompletedRun -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]*$') { throw 'Use one Relay run directory name.' }
        $run = "Docs/Development/Gameplay/V1/RelayRuns/$CompletedRun"
        $path = Assert-GeneratedPath $run
        if ((Get-Content -LiteralPath (Join-Path $path 'exit.txt')) -join ',' -ne '0,0') {
            throw 'Only a successfully completed run can have its raw logs removed.'
        }
        foreach ($peer in 'host','client') {
            if (!(Get-ChildItem -LiteralPath $path -File -Filter "$peer-*pass.txt")) { throw "Missing $peer verification evidence." }
        }
        $temporaryPeerFile = '^(host|client)(\.log|-step-\d+\.txt|-.*-ready\.txt|-.*\.ready)$'
        foreach ($item in Get-ChildItem -LiteralPath $path -File) {
            if ($item.Name -notmatch $temporaryPeerFile -and $item.Name -notin 'room.txt','join-code.txt') { continue }
            $relative = "$run/$($item.Name)"
            $file = Assert-GeneratedPath $relative
            Write-Output "Completed temporary file: $relative"
            if ($Apply -and $PSCmdlet.ShouldProcess($file,'Remove completed temporary file')) { Remove-Item -LiteralPath $file }
        }
    }
}
finally { Pop-Location }
