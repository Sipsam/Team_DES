param(
    [Parameter(Mandatory)][string]$Build,
    [Parameter(Mandatory)][string]$Run,
    [ValidateSet('rematch','networkResult','networkCombat','networkClose','networkCloseResult','networkCloseRematch','networkCloseUnreachable','networkCloseClientLoss','presentation','settings','transition','transitionRecovery','foundation','foundationImpaired','cameraControls','poleRecovery','bridge','bridgeContact','bridgeDisconnect','bridgeGuard','river','riverRecovery','castleUnderground','castleReconnect','castleKeypad','cavernHazards','cavernReconnect','contest','contestRegions','contestLifecycle','return','returnInput','returnReconnect','mountain','mountainExchange','mountainFromSpawn','mountainEntry','full','canyon','approach','summit','castle','castleWalk','riverLink','middle','finish')]
    [string]$Case = 'foundation',
    [string]$PolePlan = 'Docs/Development/Gameplay/V1/MountainPole/ContinuousPlan.json',
    [string]$ReturnPlan = 'Docs/Development/Gameplay/V1/MountainReturn/Route.json',
    [switch]$Headless,
    [switch]$Screenshots,
    [switch]$AudioTrace,
    [switch]$PolishTrace,
    [switch]$PoleGripFrames,
    [switch]$PresentationMetrics,
    [ValidateSet('A','B','C','D','E')][string]$NetworkProfile,
    [switch]$NetworkBaseline,
    [ValidateSet(1024,1280,1600,1920,2560)][int]$Width = 1024,
    [string]$EvidenceRoot = 'Docs/Development/Gameplay/V1/RelayRuns',
    [switch]$BridgeTrace,
    [string]$BridgeHazardRoute,
    [ValidateRange(1,20)][int]$BridgeCarryRepeats = 1
)

$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]*$') { throw 'Use a single directory name for -Run.' }
if ($PoleGripFrames -and ($Case -ne 'mountainExchange' -or $Headless)) {
    throw 'Pole grip frame capture requires the D3D11 mountainExchange case.'
}
if (($BridgeTrace -or $BridgeHazardRoute) -and $Case -notin 'bridge','bridgeContact','bridgeDisconnect') {
    throw 'Bridge diagnostics require an isolated bridge case.'
}
if ($BridgeHazardRoute -and $Case -eq 'bridgeDisconnect') { throw 'The disconnect case has no Return route.' }
if ($BridgeCarryRepeats -gt 1 -and $Case -notin 'bridge','bridgeContact') {
    throw 'Carry repeats require a bridge crossing case.'
}
if ($NetworkBaseline -and ($Case -ne 'foundation' -or !$NetworkProfile)) {
    throw 'The network baseline uses the foundation entry point and requires a profile.'
}
if ($NetworkProfile -and ($Case -in 'full','foundationImpaired' -or ($NetworkProfile -eq 'E' -and !$NetworkBaseline))) {
    throw 'Use network profiles with isolated cases. Profile E belongs to the network baseline.'
}
if ($Case -match '^network' -and !$NetworkProfile) { throw 'Network cases require a network profile.' }
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $project
$players = @()
$createdOutput = $false
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
        '^networkCombat$' { $extra = @('-v3NetworkCombat') }
        '^networkClose' {
            $extra = @('-v3NetworkClose', $Case)
            if ($Case -in 'networkCloseResult','networkCloseRematch') { $extra += '-v3NetworkResult' }
            if ($Case -eq 'networkClose') { $extra += '-twinAudioTrace' }
            $timeout = 390
        }
        '^networkResult$' {
            $extra = @('-v3NetworkResult')
            $timeout = 390
        }
        '^rematch$' {
            $extra = @('-v3Rematch')
            $captureFlag = '-v3RematchScreenshots'
            $marker = 'rematch-pass'
            $timeout = 510
        }
        '^(presentation|settings)$' {
            $mode = 'presentationSmoke'; $evidence = 'presentationEvidence'; $timeout = 330
            $captureFlag = '-presentationScreenshots'
            if ($Case -eq 'settings') { $extra = @('-presentationSettings') }
        }
        '^transition(Recovery)?$' {
            $mode = 'castleReplay'; $evidence = 'castleEvidence'; $timeout = 550; $captureFlag = '-castleScreenshots'
            $extra = @('-castleCavernProbe', '-castleTransitionOnly')
            if ($Case -eq 'transitionRecovery') { $extra += '-castleTransitionReconnect' }
            $marker = 'cavern-pass'
        }
        '^foundationImpaired$' { $extra = @('-v3ImpairedRelay') }
        '^cameraControls$' { $extra = @('-v3CameraControls'); $captureFlag = '-v3ControlScreenshots' }
        '^poleRecovery$' { $mode = 'poleSmoke'; $evidence = 'poleEvidence' }
        '^bridge' {
            $mode = 'bridgeSmoke'; $evidence = 'bridgeEvidence'; $timeout = 560 + 30 * ($BridgeCarryRepeats - 1); $captureFlag = '-bridgeScreenshots'
            if ($Case -eq 'bridgeContact') { $extra = @('-bridgeContactWithoutFall') }
            if ($Case -eq 'bridgeDisconnect') { $extra = @('-bridgeCarryDisconnect'); $marker = 'disconnect-pass' }
            if ($Case -eq 'bridgeGuard') { $extra = @('-bridgeCarryGuard'); $marker = 'guard-pass'; $timeout = 240 }
        }
        '^river(Recovery)?$' {
            $mode = 'riverReplay'; $evidence = 'riverEvidence'; $timeout = 500; $captureFlag = '-riverScreenshots'
            if ($Case -eq 'riverRecovery') { $extra = @('-riverRecovery') }
        }
        '^(castleUnderground|castleReconnect|castleKeypad|cavern)' {
            $mode = 'castleReplay'; $evidence = 'castleEvidence'; $timeout = 1250; $captureFlag = '-castleScreenshots'
            if ($Case -eq 'castleReconnect') { $extra = @('-castleOutboundReconnect') }
            if ($Case -eq 'castleKeypad') { $extra = @('-castleKeypadOnly') }
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
        '^mountain(Exchange)?$' {
            $mode = 'mountainPoleRun'; $evidence = 'mountainPoleEvidence'; $timeout = 1700
            $extra = @('-mountainPolePlan',(Resolve-Path -LiteralPath $PolePlan).Path)
            if ($Case -eq 'mountainExchange') { $extra += '-mountainFirstExchange'; $marker = 'exchange-pass'; $timeout = 420 }
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

    $output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Join-Path $EvidenceRoot $Run))
    if (Test-Path -LiteralPath $output) { throw "Evidence already exists: $output" }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $createdOutput = $true
    $hash | Set-Content -LiteralPath "$output/assembly-sha256.txt"
    (Get-FileHash -LiteralPath $exe).Hash | Set-Content -LiteralPath "$output/player-sha256.txt"
    $graphics = if ($Headless) { @('-batchmode','-nographics') } else {
        $height = if ($Width -eq 1024) { 720 } else { [int]($Width * 9 / 16) }
        @('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$height")
    }
    if ($Screenshots -and !$Headless -and $captureFlag) { $extra += $captureFlag }
    if ($AudioTrace) { $extra += '-twinAudioTrace' }
    if ($PresentationMetrics) { $extra += '-v3PresentationMetrics' }
    if ($NetworkBaseline) {
        $extra += '-v3NetworkBaseline'
        if ($NetworkProfile -eq 'E') { $extra += '-v3NetworkDisconnect' }
    }
    if ($PolishTrace) { $extra += '-v3PolishTrace' }
    if ($PoleGripFrames) { $extra += '-mountainGripFrames' }
    if ($BridgeTrace) { $extra += '-bridgeTrace' }
    if ($BridgeCarryRepeats -gt 1) { $extra += @('-bridgeCarryRepeats',"$BridgeCarryRepeats") }
    if ($BridgeHazardRoute) {
        $route = (Resolve-Path -LiteralPath $BridgeHazardRoute).Path
        Copy-Item -LiteralPath $route -Destination "$output/replay-route-input.txt"
        $extra += @('-bridgeHazardRoute',"$output/replay-route-input.txt")
    }
    foreach ($role in 'host','client') {
        $arguments = $graphics + @('-runInBackground',"-$mode",$role,"-$evidence",$output,'-logFile',"$output/$role.log") + $extra
        if ($NetworkProfile) { $arguments += @('-relayNetworkProbe', $output, $role, $NetworkProfile) }
        $quoted = $arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
        $windowStyle = if ($NetworkProfile -and !$Headless) { 'Normal' } else { 'Hidden' }
        $players += Start-Process -FilePath $exe -ArgumentList $quoted -PassThru -WindowStyle $windowStyle
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($timeout)
    $failureDeadline = $null
    $windowRecorded = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        $active = @($players | Where-Object { !$_.HasExited })
        $failed = @($players | Where-Object { $_.HasExited -and $_.ExitCode -ne 0 })
        if ($NetworkProfile -and !$Headless -and !$windowRecorded -and $active.Count -eq 2) {
            $players | ForEach-Object { $_.Refresh() }
            if ($players[0].MainWindowHandle -ne 0 -and $players[1].MainWindowHandle -ne 0) {
                "host=$($players[0].MainWindowHandle) client=$($players[1].MainWindowHandle)" |
                    Set-Content -LiteralPath "$output/visible-windows.txt"
                $windowRecorded = $true
            }
        }
        if (!$active.Count) { break }
        if ($failed.Count) {
            if (!$BridgeTrace) { break }
            if (!$failureDeadline) { $failureDeadline = [DateTime]::UtcNow.AddSeconds(4) }
            if ([DateTime]::UtcNow -ge $failureDeadline) { break }
        }
        Start-Sleep -Seconds 1
    }
    $exitCodes = @($players | ForEach-Object { if ($_.HasExited) { $_.ExitCode } else { 'stopped' } })
    $exitCodes | Set-Content -LiteralPath "$output/exit.txt"
    if (($exitCodes -join ',') -ne '0,0') { throw "$Case failed ($($exitCodes -join ',')). See $output" }
    foreach ($role in 'host','client') {
        if (!(Test-Path -LiteralPath "$output/$role-$marker.txt")) { throw "Missing $role $marker in $output" }
        if ($Case -in 'transition','transitionRecovery' -and $Screenshots -and !$Headless) {
            foreach ($label in 'seal','first-pickup') {
                if (!(Test-Path -LiteralPath "$output/$role-$label-1600x900.png")) {
                    throw "$role completed gameplay but missed the $label capture. See $output/$role.log"
                }
            }
        }
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
    if (($Case -in 'settings','castleKeypad' -or $BridgeTrace -or $BridgeHazardRoute -or $BridgeCarryRepeats -gt 1 -or $NetworkProfile) -and $createdOutput) {
        foreach ($name in 'room.txt', 'room.tmp') {
            $codeFile = Join-Path $output $name
            if (Test-Path -LiteralPath $codeFile) { Remove-Item -LiteralPath $codeFile }
        }
    }
    if (($Case -eq 'rematch' -or $NetworkProfile) -and $createdOutput) {
        foreach ($name in 'join-code.txt', 'join-code.tmp', 'replacement-code.txt', 'replacement-code.tmp') {
            $codeFile = Join-Path $output $name
            if (Test-Path -LiteralPath $codeFile) { Remove-Item -LiteralPath $codeFile }
        }
    }
    Pop-Location
}
