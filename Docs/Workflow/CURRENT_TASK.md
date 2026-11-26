# Current task

**V3-FEEDBACK-07** · River and Raft Gameplay

**Checkpoint** `READY_TO_COMMIT` · **Base** `510972478c9afe7dc4750b69cf7b6d5d0132f20d` · **Updated** 2026-11-26 KST

- **Scope** Feedback 12, 13 and 24. Server-authorized raft hit presentation, 10 to 15 shared course hazards in each phase, and verification of downstream physics and water scroll.
- **Open changes** River runtime, raft HUD, isolated river replay and editor validation, and shared documentation. The saved river, docks and Canyon stairs retain their inputs.
- **Validation** Saved river and Canyon approach passed. Unity 6000.3.27f1 Windows Development D3D11 Relay passed both crossings and damage/recovery, 0/0 per case. Full-world, full E2E and full match remain deferred.
- **Evidence** Optional local `Docs/Development/RiverRaft/V1/Verification.md`. Raw evidence `Feedback07-CrossingSteering` and `Feedback07-RecoveryVerified` under ignored `Docs/Development/Gameplay/V1/RelayRuns/`.
- **Next** Verify live main, commit and push the scoped changes, and capture the official D3D11 map and runtime images. Record publication against the verified hash in local evidence.

| Work | Logical Worker and domain | State | Evidence |
| --- | --- | --- | --- |
| Damage flash and rider HUD | Implement / Validate · Gameplay / Network / UI | `COMPLETED` | Both-peer hull and HUD assertions, cooldown contact, 5/3-hit recovery and reconnect. |
| Course density and downstream flow | Implement / Validate · Gameplay / Level / Rendering | `COMPLETED` | 15 hazards per phase, ribbon UVs, dock and stair access, upstream and downstream crossings. |
| Commit, push and captures | Review / Finalize · Workflow | `IN_PROGRESS` | Verify live main and capture official D3D11 images. |

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
