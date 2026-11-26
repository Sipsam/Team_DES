# Current task

**V3-FEEDBACK-02** · Camera-relative Controls and Movement

**Checkpoint** `READY_TO_COMMIT` · **Base** `7f16d5629f6d20ccaa8e8d8311ed774cb1a6ca8f` · **Updated** 2026-11-26 KST

- **Goal and scope** Feedback 4 and 5. Camera-relative pole controls and 1.5× ordinary sprint. Pole-centric movement, grab slowdown changes and full integration are follow-up work.
- **Source** Clean local HEAD, origin/main and live main matched. Feedback 1 was already committed and pushed.
- **Changes** Reuse owner camera axes for pole carry and orbit yaw. Keep orbit elevation independent. Scale normal sprint and V3 Run playback, preserving separate movement paths and server validation. Add scoped camera controls Relay case.
- **Validation** Unity 6000.3.27f1 Windows Development build passed. D3D11 `cameraControls`, `poleRecovery` and `bridgeDisconnect` all passed both Relay peers. Measured 3.500m/s walk and 8.400m/s sprint. Two initial fixture probes encountered capsule settling before measurement, resolved by warming up contact. Full E2E Relay and full match remain deferred.
- **Evidence** [Validation Summary](../Playtest/ValidationSummary.md) and optional local `Docs/Development/CameraControls/V1/Verification.md`.
- **Publication** Verify the pushed hash on live main, then capture the official map set and affected runtime views. Record their provenance in the local evidence.
- **Resume** Reconcile this checkpoint with Git and the capture record. Begin V3-FEEDBACK-03 only on user request.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
