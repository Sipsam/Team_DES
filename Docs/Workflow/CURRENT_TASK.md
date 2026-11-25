# Current task

**V3-FEEDBACK-01** · Checkpoint and Recovery Improvements

**Checkpoint** `READY_TO_COMMIT` · **Base** `97611f3968cc13fa7318d3d1b23c0bd6ed7e6435` · **Updated** 2026-11-26 KST

- **Goal and scope** Feedback 1 only. Add recovery at the outbound pole summit, both approaches to the castle basement threshold, and the Return canyon approach to the bridge.
- **Source** Clean local and live main matched. The 2026-11-22 local standard Player assembly matches the recorded Playtest build. The reported source `3805710` remains a historical reference.
- **Changes** Grounded, phase-gated checkpoints with separate slot poses. Regional recovery uses the new basement and bridge approaches. Actual gem acquisition starts Return recovery at the altar.
- **Validation** Unity 6000.3.27f1 Windows Development builds and six isolated Relay cases passed with both peers exiting 0. Castle verified both phases, actual gem acquisition, trap recovery, reconnect and both exits. Existing Big summit, raft, PvP and gem recovery cases passed. Full E2E Relay remains deferred.
- **Evidence** [Validation Summary](../Playtest/ValidationSummary.md#v3-플레이테스트-피드백-1--체크포인트와-복구) and optional local `Docs/Development/CheckpointRecovery/V1/Verification.md`.
- **Publication** Verify the commit on live `origin/main`. Official screenshots are skipped for the runtime checkpoint change.
- **Resume** Reconcile the commit and live main. Start V3-FEEDBACK-02, Camera-relative Controls and Movement (Feedback 4, 5), only when authorized.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
