# V3 Windows playtest

## Install and connect

Each tester needs a Windows PC, keyboard, mouse and an internet connection. Use the same build on both PCs. On a linked Team_DES Unity project, open Unity **6000.3.27f1** and select **Tools > Twin Edge > Gameplay > Build V3 Windows Playtest Player**. Package the `Builds/V3Playtest/` runtime files together and leave the `*_DoNotShip` debug folder on the build machine. Extract the package to a writable folder and launch `V3Gameplay.exe`. The player starts in V3 and connects to the configured Unity Authentication and Relay project.

Use this command to reproduce the windowed Direct3D 11 setup used for the Windows desktop check.

```powershell
.\V3Gameplay.exe -force-d3d11 -screen-fullscreen 0 -screen-width 960 -screen-height 680
```

1. Host selects **Create room**, selects **Copy** beside the code, then sends it to Client.
2. Client pastes the code and selects **Join room**. When both peers finish loading and their spawn and camera are ready, the server starts the match automatically. Follow the approach trail to the starting summit.
3. Click the world to capture the mouse. Press Esc to open the room menu and release the pointer. The first world click after release captures the mouse. The following click can attack.
4. Select **Leave room** to disconnect or leave after the result. Client can try the same code while Host keeps the session alive. Play waits for a second player. Check the restored position and puzzle state after rejoining. After Host leaves, both players return to the room menu and use a new code.
5. Select **Quit game** from the disconnected menu. If a connection fails, record the on-screen message, whether Host is still connected, and which build each tester used.

## Controls

| Input | Use |
| --- | --- |
| Mouse | Orbit the local character after capture. Direction also aims mountain launches. |
| WASD | Move. While on the pole, pivot moves the pair on the same summit and orbit uses A/D to turn and W/S to raise or lower. |
| Left Shift | Sprint using stamina. Hold with W/S on either raft for a stronger stroke. |
| Space | Jump. |
| E | Use the displayed target. Grab or swap pole roles, collect and install timber, board or leave a docked raft, operate glyphs and switches, use the keypad, channel the seal or pick up the available gem. |
| Left click | Attack in the direction the character faces. The same button operates UI when the mouse is released. |
| F | Grab a nearby opponent, press again to throw, or tap repeatedly to resist when held. Gem carriers cannot grab. |
| R | On a supported return summit, hold to charge and release to launch. W/S adjusts the angle while charging. |
| Esc | Release the mouse. Cancel a pole grip, timber carry, docked raft seat, launch charge, keypad or your seal participation when applicable. |
| Digits and Submit | After opening your castle keypad with E, click its field, type the four digits shown at your number station and select Submit. Press Esc to close it. |

## Route

| Section | Player objective |
| --- | --- |
| Outbound mountain | Approach opposite pole ends and press E. Move together on the summit, swing the orbit player onto the next summit and exchange roles once stable. At Big summit, release the pole and follow the ramp. |
| Outbound bridge and canyon | Use E at the crate, take one timber grip each, carry to the socket and both approve with E. Install all 13 deliveries, cross, then follow the canyon down to the lower dock. |
| Outbound river | Board the linked raft with E. Both players row with W/S and coordinate their strokes. Use the stamina display to pace stronger strokes. Disembark at the upper dock and walk to the castle. |
| Outbound castle | Follow your assigned A or B lane. Read the glyph clue and send it via the shared wall. Enter the three glyphs in order in each stage. Use your four-digit number at your keypad and both cross the basement door. |
| Underground altar | Activate each timed switch for the opposite lane. Cross before the 25-second timer expires. At the altar, both press E at their seal stations and remain nearby until the seal opens. Use E to acquire the gem and start Return. |
| Return underground and castle | Use the permanent stones, then navigate the trap corridor or enter the twelve outbound glyphs in order at the sewer shortcut. Return to the upper dock. |
| Return river and canyon | Board a solo raft each. W/S rows and A/D steers. After the lower dock, climb the canyon path. |
| Return bridge and mountain | Jump gaps and unstable bridge planks. Charge and aim successive R launches from safe summits. An opponent can strike the gem carrier after pickup protection expires, then acquire the drop with E. |
| Finish | Bring the gem inside the four markers at the top of the mountain approach trail and stay grounded. Compare the shared outcome and stopped time on both PCs. |

## Test order

Start with a room lifecycle check. Create, join, start, leave as Client and then rejoin. Verify control recovery after rejoining. Leave as Host and verify both can use a new room. Start the timed course from a fresh room and use the table above. Log elapsed wall-clock time separately from the in-game timer and note retries and pauses. Save a second run for deliberate failures such as a late timed jump, gem loss, a missed launch or a disconnect during a cooperative action.

At each section, check objective clarity, the local camera and framing, input response, camera recovery after Esc, and whether the prompt matches the action when targets overlap. Check PvP grab, throw and resistance in an agreed safe area of the course. Watch for stuck movement after release, respawn or reconnect, one E press affecting two mechanisms, unusable keypad controls, missing controls after a cancelled launch, inaccessible checkpoints, an unrecoverable gem, diverging puzzle state, and either player being unable to finish. Record a blocker as soon as it prevents continuing.

Rate mountain control, timber repetition, raft steering and obstacle avoidance, shared-wall readability, the 25-second crossing window, gem theft fairness and overall duration on separate 1–5 scales. Write down what happened for any low score. The historical 42:25.908 result was an automated replay. Record each tester's actual wall-clock duration here.

### Session sheet

Record the build, date, testers, Host/Client roles, castle slots, PC specs and network locations once per session. Use one copy of the table per tester.

| Section | Started | Ended | Retries or pauses | Rating / notes |
| --- | --- | --- | --- | --- |
| Outbound mountain | | | | |
| Outbound bridge | | | | |
| Canyon descent | | | | |
| Linked raft | | | | |
| Outbound castle | | | | |
| Underground and seal | | | | |
| Return underground and castle | | | | |
| Solo raft | | | | |
| Canyon ascent | | | | |
| Return bridge | | | | |
| Return mountain and finish | | | | |

### Issue sheet

Record the build and date, Host/Client role, section and phase, nearest landmark/checkpoint, whether either player had the gem, and first failing elapsed time. List exact inputs and reproduction steps. Record expected and observed outcomes from both PCs, occurrence count, whether there was a recovery path, and whether play had to stop. Attach both screenshots or video and relevant excerpts from `%USERPROFILE%\AppData\LocalLow\DefaultCompany\MyGame\Player.log`. Copy logs before restarting and redact active room codes.

## Latest map screenshots

After committing a saved V3 map from a clean project, select **Tools > Twin Edge > Capture Latest Map Screenshots** or run `Tools/Level/Update-MapScreenshots.ps1` with the project closed. The tool publishes the seven regions in Top and Perspective to the local `Screenshots/` folder after all 14 PNGs pass validation. The filenames use `v3` and the seven-character HEAD hash. Run the script with `-InstallHook` once to capture after local map commits. If Unity is open, the hook queues the capture until the Editor can read the saved scene.

## Development build and automated Relay checks

Run from the repository root with Unity closed. The two build methods explicitly include V3Gameplay and LevelBlockoutV3 in that order. The Playtest build uses StandaloneWindows64 with BuildOptions.None. The Development build uses BuildOptions.Development and includes the opt-in replay drivers.

For isolated verification, set `TWIN_EDGE_V3_BUILD` to a direct child of `Builds/` before invoking either build method. Clear it to restore the standard output paths. The Relay runner records both executable and managed assembly SHA-256 hashes for each run.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe'
$root = (Get-Location).Path
$evidence = "$root\Docs\Development\Gameplay\V1\RelayRuns"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

function Invoke-UnityCheck($method, $name, [switch]$Graphics) {
    $flags = if ($Graphics) { @('-force-d3d11') } else { @('-nographics') }
    $args = @('-batchmode','-quit','-projectPath',('"' + $root + '"'),
              '-executeMethod',$method,'-logFile',('"' + "$evidence\$name.log" + '"')) + $flags
    $process = Start-Process $unity -ArgumentList $args -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$method failed. See $evidence\$name.log" }
}

Invoke-UnityCheck TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlaytestPlayer BuildPlaytest
Invoke-UnityCheck TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildFullMatchPlayer BuildDevelopment
$build = 'Builds/V3Development/V3Gameplay.exe'
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Foundation1 -Case foundation
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Contest1 -Case contest
```

Foundation exercises spawn, owner camera, remote movement, interaction range and respawn. Use the same runner with contestLifecycle for stale requests and pickup/reconnect races, bridgeDisconnect for carry recovery, riverRecovery for dock and raft recovery, castleReconnect for slot restoration, and castleUnderground for normal castle passage, the seal and pickup. Use foundationImpaired for the Unity Transport delay/jitter/loss fixture. Tests may use an initial station placement and expect real Relay. Use a run name that identifies the active validation or issue. Keep failure evidence while investigating. After a passing run, preview eligible raw logs with `Tools/ProjectAudit/clean-development.ps1 -CompletedRun <name>` and add `-Apply` to remove them. The script retains pass files, exit status and result summaries. Diagnostic commands are available via Get-Help and the script parameter list.

The mountain and finish fixtures need route data from the same saved scene. In the same PowerShell session, generate it and run the isolated finish, or select full to replay the complete match.

```powershell
Invoke-UnityCheck TwinEdge.Levels.Editor.MountainPoleTuning.VerifyCurrent PolePlan -Graphics
Invoke-UnityCheck TwinEdge.Gameplay.Editor.MountainReturnValidation.Run ReturnPlan
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Finish1 -Case finish
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Full1 -Case full
```

The finish fixture exercises normal bridge installation, then provides a test gem. It uses production pickup, return traversal and finish eligibility. The full replay starts from normal spawn and uses production inputs, character controllers and RPCs through the joint seal, first pickup, return and finish. It can take more than 42 minutes. Keep Windows awake for the entire run. System sleep interrupts the live Relay connection and can expire replay deadlines. Both peers must exit successfully and produce their pass files. For finish and full, both peers must also report the same winner and stopped timer. Add -Screenshots to render supporting evidence. Generated route plans and run data are local-only under Docs/Development/Gameplay/V1/.

For scene checks use the saved Foundation, Cooperative Pole, River, Castle, Underground and World Materials validators under **Tools > Twin Edge > Gameplay**. Review the mountain load-bearing collider route with **Tools > Twin Edge > V3 Mountain > Analyze Pole Reachability** and the Level Visualization window. The reachability graph proposes the primary branch. Generate the western branch with `TwinEdge.Levels.Editor.MountainPoleContinuousPlan.VerifyAlternative` and run `-Case mountain -PolePlan Docs/Development/Gameplay/V1/MountainPole/AlternativeContinuousPlan.json` to exercise that branch over Relay.

## 산악 단독 편집기 실험

Fortress 검토 도구가 참조하는 `Assets/Game/Level/Testing/MountainTraversalTest.unity`는 단독으로 열어 Play한다. **Tools > Twin Edge > V3 Traversal > Open Test Scene**으로 열 수 있다. WASD/Space는 이동·점프, 우클릭/Esc는 포인터 캡처·해제, Q는 시점 전환, Tab/Enter는 실험 목표 선택·시도, R/Shift+R은 최근 안전 봉우리/Start 복귀, F5는 결과 저장이다.

고정 BaselineConfig와 Sparse/Balanced/Dense 메시를 사용하는 단독 콜라이더 실험이다. 발판 표본, 테스트 포물선과 두 캡슐이 설 수 있는 지점을 실제 MeshCollider에서 조사한다. V3 지형·설정 변경과 별도로 이전 실험의 재현성을 유지한다. 현재 막대·탄성 발사와 Relay 수용 검증에는 위의 V3 회귀를 사용한다. 배치 스모크는 `TwinEdge.Levels.Testing.Editor.TraversalSmoke.Run`을 호출한다. 이 메서드가 직접 에디터를 종료하므로 `-quit`을 생략한다. 결과는 `Docs/Development/Level/V3/TraversalTests/`에 있다.

[검증 요약](ValidationSummary.md) · [게임플레이 시스템](../Architecture/GameplaySystems.md)
