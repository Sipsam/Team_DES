# Current task

**V3-PRES-09** · Animation and VFX Polish

**Checkpoint** `READY_TO_COMMIT` · **Base** `97ce4fc5ac0592e96c3c1b4e54af786641aded72`

HEAD, origin/main and live main matched at entry. PRES-08 is pushed and all 22 official images match its publication manifest. PRES-08 Run-015 landing failure and subsequent isolated pass remain historical evidence. The pre-existing untracked design PDF is outside scope.

Complex · Gameplay, Animation and Rendering · Logical Workers V2. Preserve the normal Player, second human playtest ZIP, prior captures and isolated evidence. Full integration and PRES-10 remain deferred.

| Scope | Worker and dependency | State and exit |
| --- | --- | --- |
| 09-A | Explore · live prefab, rig and approval events | COMPLETED · imported rig/clip bindings and oar bounds |
| 09-B | Implement + Validate · character motion | COMPLETED · D3D11 canyon and ordinary jump rechecks |
| 09-C | Implement + Validate · pole, timber, raft and station poses | COMPLETED · high-angle reach and first role exchange rechecks |
| 09-D/E/F | Implement + Validate · approved effects, gem and local intensity | COMPLETED · focused contest, bridge, river, settings, transition and gem runs |
| 09-G | Validate, Review and Finalize · depends on B–F | IN_PROGRESS · exact stage review, verified push and official capture |

Ignored evidence is under `Docs/Development/Presentation/V3-PRES-09/`. Run-Audit-20261128-021005 confirmed the Generic rig, 240 bindings per inspected clip and root motion disabled. Run-CarryClip-20261128-021134 generated the native carry derivative. Run-Structure-20261128-025924 passed the saved seven-clip, root-motion, AirPose and marker checks.

The 022255 series passed settings, cameraControls, contest, bridge, river, returnInput, transition, poleRecovery and rematch. The 023322 series repeated the affected material and event paths after correcting timber hand side and visual sync, pole anchors and oar immersion. The concurrent bridge failed at outbound delivery 8 and remains evidence. Run-isolated-bridge-20261128-024351 passed 13 deliveries and 35 Return jumps per peer with no route teleport or respawn. The isolated first pole exchange/release and castleWalk passed. The long scheduled mountain run was replaced at startup by the first-exchange fixture.

The 025924 pole build passed the saved first-exchange route and camera controls. Moving orbit grip error was below the 1mm logging precision on both peers. A short landing-to-idle transition had a 0.174m maximum residual. Run-Build-20261128-030353 added a short ground-support probe for visual stair traversal. The 030353 D3D11 cases passed canyon traversal and regular ground/air, including no cosmetic landings on stairs and the ordinary jump and landing cues.

The clean publication worktree is `D:/TwinEdge-Pres09-Capture` at the verified base with warmed imports. Official map and runtime capture are pending against the verified pushed commit. Complete exact staged review and verify live main, then build and capture from the clean checkout. Preserve all prior screenshots and the verified second playtest package.
## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
