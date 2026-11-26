# V3 Windows playtest

## Install and connect

Each tester needs a Windows PC, keyboard, mouse and an internet connection. Use the same build on both PCs. On a linked Team_DES Unity project, open Unity **6000.3.27f1** and select **Tools > Twin Edge > Gameplay > Build V3 Windows Playtest Player**. Package the `Builds/V3Playtest/` runtime files together and leave the `*_DoNotShip` debug folder on the build machine. Extract the package to a writable folder and launch `V3Gameplay.exe`. The player starts in V3 and connects to the configured Unity Authentication and Relay project.

Use this command to reproduce the windowed Direct3D 11 setup used for the Windows desktop check.

```powershell
.\V3Gameplay.exe -force-d3d11 -screen-fullscreen 0 -screen-width 960 -screen-height 680
```

1. Host selects **Create room**, selects **Copy** beside the code, then sends it to Client.
2. Client pastes the code and selects **Join room**. When both peers finish loading and their spawn and camera are ready, the server starts the match automatically. Follow the foothill trail to the starting summit, where the 32 m cooperative pole is stored.
3. Click the world to capture the mouse. Press Esc to open the room menu and release the pointer. The first world click after release captures the mouse. The following click can attack.
4. Select **Leave room** to disconnect or leave after the result. Client can try the same code while Host keeps the session alive. Play waits for a second player. Check the restored position and puzzle state after rejoining. After Host leaves, both players return to the room menu and use a new code.
5. Select **Quit game** from the disconnected menu. If a connection fails, record the on-screen message, whether Host is still connected, and which build each tester used.

## Controls

| Input | Use |
| --- | --- |
| Mouse | Orbit the local character after capture. Direction also aims mountain launches. |
| WASD | Move relative to your camera's horizontal view. The pivot can walk on supported ground while the partner is airborne or on another summit. The orbit player uses A/D to turn relative to their view and W/S to raise or lower the pole. |
| Left Shift | Sprint using stamina, including as the grounded pole pivot. Hold with W/S on either raft for a stronger stroke. |
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
| Outbound mountain | Approach opposite pole ends and press E. Follow the thin ochre boundary on the rock to read the edge of usable footing. Swing the orbit player onto another shelf and exchange roles once stable. The shaft can pass through rock while both players still need clearance. At Big summit, release the pole and follow the ramp. |
| Outbound bridge and canyon | Use E at the crate, take one timber grip each, carry to the socket and both approve with E. Install all 13 deliveries, cross, then descend the straight stone stairway to the lower dock. |
| Outbound river | Board the linked raft with E. Both players row with W/S and coordinate their strokes. Use the stamina display to pace stronger strokes. Disembark at the upper dock and walk to the castle. |
| Outbound castle | Follow your assigned A or B lane. Read the glyph clue and send it via the shared wall. Enter the three glyphs in order in each stage. Use your four-digit number at your keypad and both cross the basement door. |
| Underground altar | Activate each timed switch for the opposite lane. Cross before the 25-second timer expires. At the altar, both press E at their seal stations and remain nearby until the seal opens. Use E to acquire the gem and start Return. |
| Return underground and castle | Use the permanent stones, then navigate the trap corridor or enter the twelve outbound glyphs in order at the sewer shortcut. Return to the upper dock. |
| Return river and canyon | Board a solo raft each. W/S rows and A/D steers. After the lower dock, climb the canyon path. |
| Return bridge and mountain | Take the same stairs up from the lower dock. Orange planks warn briefly as you enter their three-slot range. Jump gaps and unstable planks. Charge and aim successive R launches from safe summits. An opponent can strike the gem carrier after pickup protection expires, then acquire the drop with E. |
| Finish | Bring the gem inside the four markers at the top of the mountain approach trail and stay grounded. Compare the shared outcome and stopped time on both PCs. |

## Full route playtest order

Use the full route below during the explicitly started final integration stage. For a scoped task, select only the relevant checks from [Scoped validation](#scoped-validation).

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

## Official screenshots

For visible changes to terrain, environments, materials, characters, props, UI, effects or gameplay, push the validated commit to `origin/main` first. Confirm `git rev-parse HEAD` matches `git ls-remote --exit-code origin refs/heads/main` and use a clean checkout with saved scenes. Skip official capture for nonvisual work.

Run `./Tools/Level/Update-MapScreenshots.ps1` with Unity closed. The script checks live remote main and worktree cleanliness before starting Unity with D3D11. If Unity is already open, perform those Git checks yourself, confirm D3D11, exit Play mode and use **Tools > Twin Edge > Capture Latest Map Screenshots**. The Editor menu checks committed inputs and saved scenes. Use explicit post-push execution. Legacy `-AfterCommit` calls return without capture and `-InstallHook` is retired.

The existing tool stages and validates a full set before replacing managed images in ignored `Screenshots/`. Expect 1800×1200 Top and Perspective views for World, Mountain, Bridge, Canyon, River, Castle and Underground with the suffix `_v3_<seven-character-commit>.png`. Retain the capture camera and warm-up settings, inspect the set for blank frames, missing materials and rendering artifacts, and record the full hash and capture environment. For a UI or other visible change outside the saved map camera views, also capture the affected scene or screen from the same pushed checkout using the relevant game or editor view and record its provenance alongside the map set. Use commit-qualified filenames for those views. Keep pre-commit review and Before/After images in ignored development evidence and label their actual source and settings.

If capture is blocked by unrelated edits, use a clean worktree at the verified commit. If publication failed and remote main has since moved, verify that the original commit is still in actual remote main's history and use the Editor capture command in a clean checkout of that commit. Verify the recovered set before publishing it and preserve any newer official set. Leave a failure pending against the original hash and inspect any tracked changes caused by Unity. The capture step needs neither a new source commit nor a full gameplay or Relay run.

## Scoped validation

Use the levels relevant to the change. Select direct regressions and record unused levels only when they matter to the task.

| Level | Coverage |
| --- | --- |
| L0 | Diff, structure, static analysis, paths and serialized references |
| L1 | Compilation and affected unit or EditMode tests |
| L2 | Feature-specific PlayMode, physics and gameplay checks |
| L3 | Isolated Host/Client or live Relay checks |
| L4 | Direct regressions across affected systems |
| L5 | Whole-world, full E2E Relay and full-match integration |

| Change | Select checks |
| --- | --- |
| Documentation or development workflow | L0 links, actual tool interfaces, policy and diff. Skip Unity and Relay. |
| Shader or material | Shader compilation, references, shared shader impact, D3D11 render and proportionate performance checks. Skip route physics and Relay when terrain, Collider and network behavior are unchanged. |
| Terrain or Collider | Compare render and collision geometry, landing supports and affected route plans or physics. Use isolated Relay only for directly impacted multiplayer behavior. |
| Gameplay or interaction | Compile and run applicable feature tests or isolated gameplay, plus directly affected regressions. Select Host/Client checks for network effects. |
| NGO or Relay | Exercise the affected connection, readiness, reconnect or state sync in isolated peers. |
| Final integration | After all major development and explicit user initiation, run world and system regressions, full E2E Relay/full match, final Windows Player checks, and validate the release build. |

Record each relevant result as `PASS`, `FAIL`, `BLOCKED`, `NOT_RUN` or `DEFERRED`, with its target and reason. Reuse prior results and Players only after comparing relevant code, saved mesh/scene, config, dependencies and build provenance. Distinguish static checks, recorded Collider playback and live Relay. Distinguish Editor and Windows Player rendering. Record commands, environment, version, scope, and optional local evidence as needed. Investigate the first failure and rerun the affected checks after a fix. Keep fixture placement and state overrides separate from production rules.

## Development build and automated Relay checks

Run from the repository root with Unity closed. The two build methods explicitly include V3Gameplay and LevelBlockoutV3 in that order. The Playtest build uses StandaloneWindows64 with BuildOptions.None. The Development build uses BuildOptions.Development and includes the opt-in replay drivers.

Reuse a Player if its saved scenes, assets, code and build settings match the check. If a rebuild is required, choose only the build method for the required edition. Use the Development edition for the automated runner.

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

Use `-Case contest -Screenshots` for the isolated attack and carry presentation, hit range boundary, rear and wall rejection, carry walk/sprint and camera collision, resistance, throw, gem ownership and finish checks from staged stations. Use `-Case contestLifecycle` for the separate respawn and reconnect check, including the original cape color after the guest receives a new NGO ID. Both cases compare the displayed cape colors on the two peers.

Use `-Case cameraControls` for the two-peer WASD yaw/pitch matrix, walking and sprinting distance, sprint stamina, jump, a sprint into a thin wall, Run playback and final position replication. The case uses a local flat fixture for ordinary movement and the saved Start summit for camera-relative pole carry, walk and sprint pace, orbital control and carry while the orbit endpoint is airborne. Add `-Screenshots` for each peer's Run frame and pole HUD.

Use `-Case poleRecovery` for rod-only obstruction, character collision, cancel and disconnect recovery. Use `-Case mountain -PolePlan <plan>` to replay an isolated mountain route from the grips. At the first landing, both peers verify a supported pivot step with the orbit player on another summit at a different elevation, then finish the exchange and route. The replay saves owner-camera and offscreen views of the first landing.

After generating the saved mountain Return plan below, use `-Case return -ReturnPlan <plan>` to measure preview-to-landing distance over the full mountain segment in both peers. Use `-Case returnInput -ReturnPlan <plan> -Screenshots` to exercise concurrent owner previews, charge and aim updates, supported and risky contacts, unclear trajectories, release, cancel and respawn. The fixture drives the Input System and records managed allocation samples for the warmed prediction calculation.

Use `-Case bridge` to verify all 13 timber deliveries, the outbound crossing, proximity boundaries at four, three, two and zero plank slots, replicated warning and removal, Canyon bridge approach recovery and both players’ Return crossings. The legacy `bridgeContact` case runs the same proximity and crossing checks without the dedicated fall station. The probes stage each owner at separate slots before traversing the deck by camera-relative input. `-Screenshots` records the outbound deck, warning and collapse. Repeat with distinct run names for different server-selected hazards. Check both peers’ proximity and Return pass files and exit codes.

The mountain and finish fixtures need route data from the same saved scene. In the same PowerShell session, generate it and run the isolated finish when that path is in scope.

```powershell
Invoke-UnityCheck TwinEdge.Levels.Editor.MountainPoleTuning.VerifyCurrent PolePlan -Graphics
Invoke-UnityCheck TwinEdge.Gameplay.Editor.MountainReturnValidation.Run ReturnPlan
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Finish1 -Case finish
```

The finish fixture exercises normal bridge installation, then provides a test gem. It uses production pickup, return traversal and finish eligibility. Both peers must exit successfully and produce their pass files and agree on the winner and stopped timer. Add `-Screenshots` for local replay evidence. Generated route plans and run data are local-only under `Docs/Development/Gameplay/V1/`.

After generating the route data, use `-Case castleWalk` for the castle gate, puzzle rooms, keypads and basement threshold. This fixture starts outside the gate and sets the host and client cameras to different headings. Use `-Case castle` to continue through the actual seal, gem pickup and castle exit. Use `-Case canyon` for the straight stairway and both joints. Host walks down in Outbound and sprints up in Return. Client sprints down and walks up. Add `-Screenshots` for mid-flight views. **Tools > Twin Edge > Gameplay > Validate Canyon Stairway** checks the saved stair mesh, parapets and wet bank, and traverses both joints with the prefab’s CharacterController at 30, 60 and 120 fps. A stalled castle waypoint records its target, camera axes, stick, controller velocity and collision evidence.

Use `-Case mountainFromSpawn -PolePlan <plan> -Screenshots` for an isolated Windows Relay run from the ordinary spawns through the mountain checkpoint. To prepare a named plan from the saved graph, set `TWIN_EDGE_POLE_ROUTE_FILE` to a local text file with a plan name followed by its ordered summit IDs on each line. Use `Start` and `Final` as the first and last IDs. Run `TwinEdge.Levels.Editor.MountainPoleContinuousPlan.VerifyNamedRoutes` and pass the resulting `<name>Plan.json` from the mountain evidence directory to the runner.

Use `-Case mountainEntry -Screenshots` to verify the ordinary spawns, initial camera and foothill inputs through Trail Entrance. This case ends before the pole grips and records the bound camera's position, forward vector and vertical FOV. The approved DeepGullies and FinalPolish combination still needs a Windows Player check at final integration. See the [mountain validation summary](ValidationSummary.md#승인된-deepgullies--finalpolish-산악).

For scene checks use the saved Foundation, Cooperative Pole, River, Castle, Underground and World Materials validators under **Tools > Twin Edge > Gameplay**. Inspect candidate mountain connections with **Tools > Twin Edge > V3 Mountain > Analyze Pole Reachability** and the Level Visualization window. After `MountainPoleTuning.VerifyCurrent` rebuilds the saved graph, run `TwinEdge.Levels.Editor.MountainPoleContinuousPlan.VerifyRepresentativeRoutes` to generate `ContinuousPlan.json`, `AlternativeContinuousPlan.json` and `EasternContinuousPlan.json`. These plans cover different starts, intermediate shelves and entries to Big summit. Replay them against a Development Player built from the same saved level.

Use **Tools > Twin Edge > V3 World > Refresh Environmental Geology** to rebuild the saved visual geology and apply the V3 fortress materials. Use `TwinEdge.Levels.Editor.LatestMapScreenshots.CaptureWorkingMap` to review an uncommitted scene under `Docs/Development/Level/V3/WorldWorking/`.

## Final integration only

Run the following only after the [roadmap](../Workflow/ROADMAP.md) prerequisites are met and the user explicitly starts final integration. Use the matching Development Player and route data prepared above.

```powershell
.\Tools\Playtest\Run-Relay.ps1 -Build $build -Run Full1 -Case full
```

The full replay starts at normal spawn and uses production inputs, character controllers and RPCs through the seal, first pickup, return and finish. It can take over 42 minutes. Keep Windows awake and verify that both peers exit successfully, write pass files and report the same winner and stopped timer. Also verify the final saved world, affected system boundaries, and the Windows Player that packs both DeepGullies and FinalPolish. Check D3D11 rendering and collision in that Player and validate the distribution build. Use the [validation summary](ValidationSummary.md) to identify remaining human and platform coverage.

## 산악 단독 편집기 실험

Fortress 검토 도구가 참조하는 `Assets/Game/Level/Testing/MountainTraversalTest.unity`는 단독으로 열어 Play한다. **Tools > Twin Edge > V3 Traversal > Open Test Scene**으로 열 수 있다. WASD/Space는 이동·점프, 우클릭/Esc는 포인터 캡처·해제, Q는 시점 전환, Tab/Enter는 실험 목표 선택·시도, R/Shift+R은 최근 안전 봉우리/Start 복귀, F5는 결과 저장이다.

고정 BaselineConfig와 Sparse/Balanced/Dense 메시를 사용하는 단독 콜라이더 실험이다. 발판 표본, 테스트 포물선과 두 캡슐이 설 수 있는 지점을 실제 MeshCollider에서 조사한다. V3 지형·설정 변경과 별도로 이전 실험의 재현성을 유지한다. 현재 막대·탄성 발사와 Relay 수용 검증에는 위의 V3 회귀를 사용한다. 배치 스모크는 `TwinEdge.Levels.Testing.Editor.TraversalSmoke.Run`을 호출한다. 이 메서드가 직접 에디터를 종료하므로 `-quit`을 생략한다. 결과는 `Docs/Development/Level/V3/TraversalTests/`에 있다.

[검증 요약](ValidationSummary.md) · [게임플레이 시스템](../Architecture/GameplaySystems.md)
