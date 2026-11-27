# Current task

**V3-PRES-08** · Settings and Escape Menu

**Checkpoint** `READY_TO_COMMIT` · **Base** `1ce226337d54547916d1f062f41e238b23cb8786`

HEAD, origin/main and live main matched at entry. V3-PRES-07 source is pushed and all 21 official image hashes match its ignored capture receipt. The pre-existing untracked design PDF remains outside scope.

Local menus and persistence are verified. Evidence is in unique run directories under ignored `Docs/Development/Presentation/V3-PRES-08/`. Run-001 passed Editor D3D11 presentation checks. Runs 007–008 and 017–018 passed Windows display and restart checks, with WASAPI and camera sensitivity in Run-007. Runs 011 and 013 passed rematch and camera-relative movement. Runs 019–020 passed headless launch cancellation and camera collision. Run-022 passed the final D3D11 settings Relay check including keypad and Return intensity. Run-015 passed wood carry Escape and later failed a route landing. The PRES-07 base passed in Run-021, and the isolated final candidate passed the bridge case in Run-023 on D3D11. After exact staged review and verified push, publish map and runtime captures from a clean worktree. Record the commit and capture receipt in ignored evidence. Second human playtest and final integration remain deferred.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
