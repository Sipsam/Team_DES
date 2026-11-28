# Current task

**V3-PRES-FOLLOWUP** · Pole grip alignment and transition capture timing

**Checkpoint** `READY_TO_COMMIT` · **Base** `611b3a8059b16681b9837f9d08368898ca4261e5`

Standard · Animation, Gameplay, UI and Testing · Lightweight Task-Centric Orchestration V2. Local HEAD, origin/main and live main matched. The preceding Network Close result is already recorded in ROADMAP. Preserve the untracked design PDF, prior builds, captures and evidence.

| Work | Worker | State | Scope and next check |
| --- | --- | --- | --- |
| FOLLOWUP-A | Explore | COMPLETED | Reconciled Git and historical PRES-09 receipts. |
| FOLLOWUP-B | Diagnose | COMPLETED | Saved D3D11 first landing reproduced 0.17395m for Orbit on both peers. The Landed snapshot removed reach correction while the evaluated rig still held AirPose. |
| FOLLOWUP-C | Implement | COMPLETED | Continue the existing capped pose-based correction while Orbit is Landed. |
| FOLLOWUP-D | Diagnose + Implement | COMPLETED | The archived concurrent capture stopped at Seal sequence 2. The fixture now captures the active banner for the same sequence at end of frame. |
| FOLLOWUP-E | Validate + Review | COMPLETED | Three saved-route pairs per build, poleRecovery, cameraControls, isolated 0/100% and concurrent transition, recovery retry and both peer grip frames passed. |
| FOLLOWUP-F | Finalize | IN_PROGRESS | Stage and review the exact task scope, commit, push and verify live main. Publish warmed map and runtime images from clean pushed source. |

Optional ignored evidence is under `Docs/Development/Presentation/V3-PRES-FOLLOWUP/`. The first diagnostic build picked up an in-progress test source change during player compilation and failed with CS0117. Its log is preserved. The baseline retry held the capture patch separately until the build exited. Baseline `Build-Dev-20261128-183321799` and candidate `Build-Dev-20261128-183932385` have distinct input receipts. The re-entry retry used `Build-Dev-20261128-190651092` after a supplemental camera capture failed on the first recovery run. See [validation](../Playtest/ValidationSummary.md#v3-pres-followup) and optional `grip-metrics.txt`, `root-continuity.txt` and `transition-timeline.txt`. Record commit, push and publication receipts independently in ignored evidence. The historical Client logged Seal sequence 2 first-display at 150.736, then input release at 153.176 after the 151.085 deadline. The exception at the Seal capture aborted the original run before a Return observation. The same archived build reached Return sequence 3 on both peers in isolation.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
