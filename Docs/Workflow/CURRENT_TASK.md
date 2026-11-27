# Current task

**V3-PERF-01** · World and Map Rendering Optimization

**Checkpoint** `READY_TO_COMMIT` · **Base** `a764f1427addeb49ceac092b4a39f365ea326a74`

Complex · Rendering, Level and UI. HEAD, origin/main and live main agreed at entry and precommit fetch. The earlier verified V3-STABILITY-01 result remains closed. Preserve the untracked design PDF, three distributed Players, 216 prior captures, QA-AUDIT and BridgeStability evidence. Both human playtests await reports. V3-INTEGRATION and V3-RELEASE stay deferred.

| Subtask | Worker | State | Evidence or next action |
| --- | --- | --- | --- |
| PERF01-A/B | Explore + Diagnose | `COMPLETED` | Baseline two-world and one-world-with-companion samples, fixed-camera GPU and render counter attribution, and URP 17.3.0 reference review. `Attribution-20261128-063724901` and `Geometry-20261128-065418724` identify opaque, SSAO and depth-normal work and the repeated fixed bridge modules. |
| PERF01-C | Implement + Validate | `COMPLETED` | The full map captures the world frame and restores the camera on close and transitions. `Pair-20261128-070528011` confirmed Host 4.26ms GPU and ~17 draws in the overview. Static region-sheet margins match. |
| PERF01-D/E | Implement + Review | `COMPLETED` | Combine compatible fixed bridge frame parts into three module meshes. 26 spans share them, cutting active frame renderers from 2,392 to 78. Preserve the terrain, shadow paths, materials and collisions. Depth priming, mountain partition, opaque-copy and settings experiments were excluded. |
| PERF01-F | Validate + Review | `COMPLETED` | Both configurations passed three stationary 600-frame repeats. `Focused-20261128-071413330`, `Rematch-20261128-071540133` and `Bridge-20261128-071650766` passed both peers. Baseline and optimized bridge fixtures match visually. `NormalPlayer-20261128-072119343` built and booted in D3D11 with Development fixtures absent. |
| PERF01-G | Finalize | `IN_PROGRESS` | Stage exact scopes, commit and push. Verify the hash against live main. In a clean checkout run the official saved-world capture and commit-qualified map and bridge runtime captures. Record publication and verify historical artifacts independently. |

Local evidence is in ignored `Docs/Development/RenderingOptimization/V1/`. Source paths cover the fixed frame visual builder, full map backdrop and focused Presentation Relay driver, plus relevant shared contracts. The initial profiling runner's read-only registry handle prevented verification of display-preference restoration. The corrected runner saved snapshots and verified restoration for the repeated matrices, focused runs and rematch. A post-run comparison found no preference drift against the pre-Rematch snapshot. Whole-match, whole-world and full E2E Relay are deferred. Actual 1440p was unavailable. [Measured results and checks](../Playtest/ValidationSummary.md#v3-perf-01--월드와-전체-지도-렌더링)

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
