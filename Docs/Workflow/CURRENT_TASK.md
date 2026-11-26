# Current task

**V3-PLAYTEST-02** · Second Human Playtest Build

**Checkpoint** `READY_TO_COMMIT` · **Base** `2079709428e58847bd08e9d582e288eef0dc3833` · **Updated** 2026-11-27 KST

- **Scope** Build and smoke-test the normal Windows x64 Player from clean, remote-verified main. Prepare two-player instructions and checklist. Full integration is deferred.
- **Open changes** Shared workflow and playtest documentation. The previous Feedback 09 checkpoint is reconciled with pushed source `2079709`.
- **Evidence** Optional local `Docs/Development/SecondHumanPlaytest/V1/`. The original November 22 Player is in `Builds/V3Playtest/`. Its pre-build hashes are recorded. The new build targets `Builds/V3Playtest-20261127-2079709/`.
- **Validation** Unity Windows x64 normal build succeeded. Packed scenes, approved mountain assets, sprint tuning and exclusion of Development diagnostics verified. Same-PC D3D11 Relay room creation, join, spawn and automatic match start passed. Package fully verified, and all 208 original build files matched their pre-build hashes.
- **Next** Commit and verify push of the shared documentation. Then await the two testers on separate PCs using the packaged build and checklist. Final integration remains deferred.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
