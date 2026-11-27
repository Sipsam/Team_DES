# Current task

**V3-PRES-04** · Audio Foundation and Core SFX

**Checkpoint** `READY_TO_COMMIT` · **Base** `1b5fb173caa1ba06b25461033f53c88d8775a21a`

- Live remote main matched HEAD at start. The untracked team UI PDF is outside scope.
- V3-PRES-03 source is pushed. This task has no visual publication target. Existing second human playtest artifacts, human testing and final integration remain outside execution.
- Foundation and assets · Implement + Audio. Configure native mixer, catalog, bounded playback and Master storage. Verify imports, routing, listener and signal.
- Gameplay and UI · Implement + Gameplay/Audio/Network. Consume existing replicated events and add server-only notifications for transient events without an event representation. Verify the production UI and representative Relay paths.
- Validation and integration · Validate, Review, Finalize. Run focused D3D11 and isolated Relay, then review exact paths against live main. Raw assets and diagnostic evidence are under ignored `Docs/Development/Audio/V1/`.
- Next · All required scoped checks passed. Build 02 covers the D3D11 Editor, WASAPI signal, presentation, contest, bridge and raft. Build 03 confirms the corrected castle door and first platform cues and passes castle/underground on both peers. Verify remote main, commit and push the reviewed scope. Record the verified hash in ignored evidence. Official screenshot publication is skipped for the audio-only change.
## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
