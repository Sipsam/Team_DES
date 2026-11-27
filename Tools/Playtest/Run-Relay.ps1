param(
    [Parameter(Mandatory)][string]$Build,
    [Parameter(Mandatory)][string]$Run,
    [ValidateSet('presentation','foundation','foundationImpaired','cameraControls','poleRecovery','bridge','bridgeContact','bridgeDisconnect','river','riverRecovery','castleUnderground','castleReconnect','cavernHazards','cavernReconnect','contest','contestRegions','contestLifecycle','return','returnInput','returnReconnect','mountain','mountainFromSpawn','mountainEntry','full','canyon','approach','summit','castle','castleWalk','riverLink','middle','finish')]
    [string]$Case = 'foundation',
    [string]$PolePlan = 'Docs/Development/Gameplay/V1/MountainPole/ContinuousPlan.json',
    [string]$ReturnPlan = 'Docs/Development/Gameplay/V1/MountainReturn/Route.json',
    [switch]$Headless,
    [switch]$Screenshots,
    [switch]$AudioTrace
)

$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]*$') { throw 'Use a single directory name for -Run.' }
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $project
$players = @()
try {
    $exe = (Resolve-Path -LiteralPath $Build).Path
    $dll = Join-Path (Split-Path $exe) 'V3Gameplay_Data/Managed/Assembly-CSharp.dll'
    $hash = (Get-FileHash -LiteralPath $dll).Hash
    $mode = 'v3Smoke'
    $evidence = 'v3Evidence'
    $timeout = 240
    $extra = @()
    $captureFlag = $null
    $marker = 'pass'

    switch -Regex ($Case) {
        '^presentation$' {
            $mode = 'presentationSmoke'; $evidence = 'presentationEvidence'; $timeout = 330
            $captureFlag = '-presentationScreenshots'
        }
        '^foundationImpaired$' { $extra = @('-v3ImpairedRelay') }
        '^cameraControls$' { $extra = @('-v3CameraControls'); $captureFlag = '-v3ControlScreenshots' }
        '^poleRecovery$' { $mode = 'poleSmoke'; $evidence = 'poleEvidence' }
        '^bridge' {
            $mode = 'bridgeSmoke'; $evidence = 'bridgeEvidence'; $timeout = 560; $captureFlag = '-bridgeScreenshots'
            if ($Case -eq 'bridgeContact') { $extra = @('-bridgeContactWithoutFall') }
            if ($Case -eq 'bridgeDisconnect') { $extra = @('-bridgeCarryDisconnect'); $marker = 'disconnect-pass' }
        }
        '^river(Recovery)?$' {
            $mode = 'riverReplay'; $evidence = 'riverEvidence'; $timeout = 500; $captureFlag = '-riverScreenshots'
            if ($Case -eq 'riverRecovery') { $extra = @('-riverRecovery') }
        }
        '^(castleUnderground|castleReconnect|cavern)' {
            $mode = 'castleReplay'; $evidence = 'castleEvidence'; $timeout = 1250; $captureFlag = '-castleScreenshots'
            if ($Case -eq 'castleReconnect') { $extra = @('-castleOutboundReconnect') }
            if ($Case -eq 'cavernHazards') { $extra = @('-castleCavernProbe','-castleCavernHazards'); $marker = 'hazards-pass' }
            if ($Case -eq 'cavernReconnect') { $extra = @('-castleCavernProbe','-castleCavernReconnect'); $marker = 'cavern-pass' }
        }
        '^contest' {
            $extra = @('-v3Contest')
            $captureFlag = '-v3ContestScreenshots'
            if ($Case -eq 'contestRegions') { $extra += '-v3ContestRegions' }
            if ($Case -eq 'contestLifecycle') { $extra += '-v3ContestLifecycle' }
        }
        '^return' {
            $mode = 'returnSmoke'; $evidence = 'returnEvidence'; $timeout = 710
            $extra = @('-returnPlan',(Resolve-Path -LiteralPath $ReturnPlan).Path)
            if ($Case -eq 'returnInput') { $extra += '-returnInput'; $marker = 'input-pass'; $captureFlag = '-returnScreenshots' }
            if ($Case -eq 'returnReconnect') { $extra += '-returnRecovery' }
        }
        '^mountain$' {
            $mode = 'mountainPoleRun'; $evidence = 'mountainPoleEvidence'; $timeout = 1700
            $extra = @('-mountainPolePlan',(Resolve-Path -LiteralPath $PolePlan).Path)
        }
        '^(full|mountainFromSpawn|mountainEntry|canyon|approach|summit|castle|castleWalk|riverLink|middle|finish)$' {
            $marker = 'full-match-pass'
            $mode = 'fullMatchRelay'; $evidence = 'fullMatchEvidence'; $timeout = 3900; $captureFlag = '-fullMatchScreenshots'
            if ($Case -ne 'mountainEntry') {
                $extra = @('-mountainPolePlan',(Resolve-Path -LiteralPath $PolePlan).Path,
                           '-returnPlan',(Resolve-Path -LiteralPath $ReturnPlan).Path)
            }
            if ($Case -ne 'full') {
                $link = switch ($Case) {
                    'riverLink' { 'river' }
                    'mountainFromSpawn' { 'mountain' }
                    default { $Case }
                }
                $extra += @('-fullMatchLink',$link)
                $timeout = if ($Case -eq 'mountainEntry') { 240 } else { 1800 }
                $marker = 'isolated-link-pass'
            }
        }
    }

    $output = Join-Path $project "Docs/Development/Gameplay/V1/RelayRuns/$Run"
    if (Test-Path -LiteralPath $output) { throw "Evidence already exists: $output" }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $hash | Set-Content -LiteralPath "$output/assembly-sha256.txt"
    (Get-FileHash -LiteralPath $exe).Hash | Set-Content -LiteralPath "$output/player-sha256.txt"
    $graphics = if ($Headless) { @('-batchmode','-nographics') } else {
        @('-force-d3d11','-screen-fullscreen','0','-screen-width','1024','-screen-height','720')
    }
    if ($Screenshots -and !$Headless -and $captureFlag) { $extra += $captureFlag }
    if ($AudioTrace) { $extra += '-twinAudioTrace' }
    foreach ($role in 'host','client') {
        $arguments = $graphics + @('-runInBackground',"-$mode",$role,"-$evidence",$output,'-logFile',"$output/$role.log") + $extra
        $quoted = $arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
        $players += Start-Process -FilePath $exe -ArgumentList $quoted -PassThru -WindowStyle Hidden
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($timeout)
    while ([DateTime]::UtcNow -lt $deadline) {
        $active = @($players | Where-Object { !$_.HasExited })
        $failed = @($players | Where-Object { $_.HasExited -and $_.ExitCode -ne 0 })
        if (!$active.Count -or $failed.Count) { break }
        Start-Sleep -Seconds 1
    }
    $exitCodes = @($players | ForEach-Object { if ($_.HasExited) { $_.ExitCode } else { 'stopped' } })
    $exitCodes | Set-Content -LiteralPath "$output/exit.txt"
    if (($exitCodes -join ',') -ne '0,0') { throw "$Case failed ($($exitCodes -join ',')). See $output" }
    foreach ($role in 'host','client') {
        if (!(Test-Path -LiteralPath "$output/$role-$marker.txt")) { throw "Missing $role $marker in $output" }
    }
    if ($Case -in 'full','finish') {
        $hostResult = Get-Content -Raw -LiteralPath "$output/host-result.txt"
        $clientResult = Get-Content -Raw -LiteralPath "$output/client-result.txt"
        if ($hostResult -ne $clientResult) { throw "Peers disagree on winner or timer in $output" }
    }
    Write-Output "$Case PASS ($output)"
}
finally {
    $players | Where-Object { !$_.HasExited } | Stop-Process
    Pop-Location
}
