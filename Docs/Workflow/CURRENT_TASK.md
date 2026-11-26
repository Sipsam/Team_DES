# Current task

**V3-FEEDBACK-06** · Collapsing Bridge and Stairway

**Checkpoint** `READY_TO_COMMIT` · **Base** `263d821b4eaf799ac6cd517ea194d87fd7110749` · **Updated** 2026-11-26 KST

- **Scope** Feedback 2 and 25. Server-side Return plank proximity and the straight Canyon stairs between the existing bridge approach and Lower dock.
- **Validation** Saved Foundation, bridge hazard geometry and 12 stairway CharacterController traversals passed. The saved wet bank and both stair joints passed. Unity 6000.3.27f1 Windows Development Player built. D3D11 bridge and Canyon Relay passed 0/0 each. Both players completed the bridge and stair crossings. Full-world, full E2E and full match deferred.
- **Evidence** Optional local `Docs/Development/BridgeStairway/V1/Verification.md`. Raw Relay results are under ignored `Docs/Development/Gameplay/V1/RelayRuns/Feedback06-*/`.
- **Next** Verify live main, commit and push the reviewed scope. Capture the official Editor maps and runtime bridge/stone stairway views from the verified source.

| Work | Logical Worker and domain | State | Evidence |
| --- | --- | --- | --- |
| Return proximity and persistence | Implement / Validate · Gameplay / Network | `COMPLETED` | Ordered reach, warning and collapse on both peers, jumps, fall and recovery, original timber deliveries. |
| Straight stairway | Implement / Validate · Level / Rendering | `COMPLETED` | Saved meshes, end joints, walking and sprinting in both directions. |
| Commit, push and captures | Review / Finalize | `IN_PROGRESS` | Verified live main and official D3D11 images. |

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
