# Current task

**V3-FEEDBACK-05** · Player Appearance and PvP

**Checkpoint** `READY_TO_COMMIT` · **Base** `33b7fcda71c85e287d65f3ba5a8172c3cec166ad` · **Updated** 2026-11-26 KST

- **Scope** Feedback 8, 9, 10, 11 and 27. Remove the IdentityBand mesh, assign and restore distinct cape colors, synchronize attack/hit pose timing, restore ordinary carry stride and clear the owner camera. Raise the server's ordinary hit range from 2.650m to 3.445m.
- **Validation** Saved Foundation passed. Unity 6000.3.27f1 Windows Development D3D11 Relay cases `contest`, `contestLifecycle` and `cameraControls` all passed 0/0. Both peers agreed on cape colors and hit/carry state. Reconnect preserved color under a new NGO ID. The 3.425m hit, 3.465m miss, rear and wall checks passed. The server landed 0.004s after the scheduled pose contact. Carrier strides measured 3.500 and 8.400m/s. Resistance, throw, exhaustion, gem and camera regressions passed.
- **Evidence** Optional local `Docs/Development/PlayerAppearancePvP/V1/Verification.md`. Includes the initial compile and capture fixture corrections, replicated-capsule collision and carry-lag diagnosis, and final build/run hashes.
- **Next** Review staged scope, verify live main, commit and push, then generate the official maps and both players' PvP gameplay images from the verified commit. Full integration and full match remain deferred.

| Work | Logical Worker and domain | State | Evidence |
| --- | --- | --- | --- |
| Appearance and session identity | Implement / Validate · Gameplay / Network | `COMPLETED` | Saved prefab, rendered color parity, respawn and rejoin. |
| Attack and hit presentation | Implement / Validate · Gameplay | `COMPLETED` | Server contact timing, both rigs and D3D11 frames. |
| Grab movement and camera | Implement / Validate · Gameplay | `COMPLETED` | Full carry strides, raised camera and wall, resistance and throw. |
| Server attack range and rules | Implement / Validate · Gameplay / Network | `COMPLETED` | Range boundary, aim and obstruction guards, gem rules. |
| Commit, push and captures | Review / Finalize | `IN_PROGRESS` | Isolated validation passed. Commit reviewed scope and publish from live main. |

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
