# Current task

**V3-PRES-07** · Result Screen and Rematch

**Checkpoint** `READY_TO_COMMIT` · **Base** `8a80a39cda5083e442659ebbd7ad138576b31308`

HEAD, origin/main and live main matched at entry. V3-PRES-06 is complete. All 21 local official images match the receipt for that commit. The untracked `Docs/GameDesign/TwinEdge_Level_UI_Design.pdf` predates this task.

| Subtask | State | Scope and next check |
| --- | --- | --- |
| 07-A | COMPLETED | Audited finish, winner ID and slot, confirmed timer, scene lifetime, player and course initialization, result audio, UI input, and imported animation assets. |
| 07-B | COMPLETED | Korean uGUI result, input ownership, frozen server time and four fixed D3D11 layouts passed. Actual window requests reached 1280, 1600 and 1920 widths. The desktop limited 2560 to 1920. |
| 07-C | COMPLETED | Both original clips visibly animate the copied rig. Result music and one-shot cues passed per peer, including Finished rejoin. |
| 07-D | COMPLETED | Run-017 passed independent consent, cancellation, duplicate and stale requests, disconnect clearing, and explicit Ready and Start after reset. |
| 07-E | COMPLETED | Run-017 passed two same-room resets, including a peer disconnect during reconstruction and subsequent Outbound rejoin. Round-one and round-two resource totals matched. |
| 07-F | IN_PROGRESS | Run-020 passed final D3D11 Relay result, same-room reset, recovery, and resource checks. Run-021 passed existing real seal and Return recovery. Runs 003, 008 and 015 passed contest, presentation and transition regressions. Commit and push after live main and exact staged scope review. Official map and runtime captures follow from the clean pushed checkout. |

Unique ignored evidence root `Docs/Development/Presentation/V3-PRES-07/`. Run-001 contains the Unity clip import and first build logs. Use a new run directory on each attempt. Second human playtest and final integration remain deferred.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
