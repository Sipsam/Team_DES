# Current task

**V3-FEEDBACK-09** · Dungeon and Sewer Puzzle

**Checkpoint** `READY_TO_COMMIT` · **Base** `4af54c83fbaa891de3e176aecd23b5bafa063ce6` · **Updated** 2026-11-27 KST

- **Scope** Feedback 20–23. Remove two optional underground wall slides, fit six landing traps to the playable deck in its local frame, and replicate accepted Return sewer pad presses. Ignore incorrect sewer glyphs while preserving the accepted prefix and the twelve-glyph completion condition.
- **Open changes** Underground and castle runtime, gameplay scene fields, underground installer, player movement cleanup, focused existing Relay fixtures and shared contracts.
- **Validation** Saved underground structure and castle structure/presentation passed. Windows D3D11 live Relay `Feedback09-HazardsMeshFit` and `Feedback09-Candidate` both exited 0/0. Verified six mesh and damage ranges, both peers’ warning and attack states, actual edge hits and recovery, eight cooperative crossings and the physical Return route. The castle run retained outbound pad resets and verified sewer press and release on both peers, harmless wrong inputs, the twelve-glyph unlock, reconnect and bypass traversal.
- **Evidence** Optional local `Docs/Development/DungeonSewer/V1/Verification.md` and `Docs/Development/Gameplay/V1/RelayRuns/`. The candidate PDB and scene blob record match the source inputs.
- **Next** Verify actual remote main, commit and push the scoped files, then capture the official D3D11 map and runtime views. Record source hash and publication in local evidence. All nine feedback tasks have scoped evidence. Separate-PC human replay and final integration remain deferred.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
