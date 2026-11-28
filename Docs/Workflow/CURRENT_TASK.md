# Current task

**V3-PERF-02** · CPU, GC, Audio and Resource Lifetime Optimization

**Checkpoint** `READY_TO_COMMIT` · **Base** `323a324a78b171cf602d8f35969f7fafb92327be`

Standard · UI, Gameplay, Audio and Performance. The production diff covers the existing pole overlap filter and changes to lobby and result text updates. Startup reduced measured allocation by 8.00MB with all 29 summit boundary hashes equal. Paired lobby and result samples captured the per-frame savings and targeted UI CPU times.

The candidate passed visible D3D11 Relay UI and audio, ten keypad cycles, same-room rematch, settings and first pole exchange checks. Earlier repeated menu, lobby, Esc and map checks supply lifecycle evidence. The normal Player booted in D3D11 with all six probed fixture types excluded. The repeat launch verified preference restoration after a log-lock failure in the first normal boot run. See [validation, evidence and limits](../Playtest/ValidationSummary.md#v3-perf-02--cpu-gc-오디오와-자원-수명).

Optional local evidence is in ignored `Docs/Development/RuntimeOptimization/V1/`. The protected manifest matches all 240 captures, three deployed executables, two ZIPs and the untracked design PDF. Official screenshot publication is skipped for this scope. The second and third human playtests await reports. Final integration, release and the network baseline retain their roadmap states.

Verify live main, review the exact staged diff, and commit and push. Record the resulting commit and remote check in ignored finalization evidence.


## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
