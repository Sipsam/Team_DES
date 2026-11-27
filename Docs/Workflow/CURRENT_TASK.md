# Current task

**V3-PRES-06** ? Match Start, Seal and Return Transition

**Checkpoint** `READY_TO_COMMIT` ? **Base** `e49ce54d5e020f7ad7fa4eb69fab58deba5f667b`

Live main matched HEAD at entry. The untracked `Docs/GameDesign/TwinEdge_Level_UI_Design.pdf` predates this work.

| Subtask | State | Scope and next check |
| --- | --- | --- |
| 06-A | COMPLETED | Existing phase, spawn camera, owner movement, readiness and audio contracts reviewed. |
| 06-B | COMPLETED | Server window and request revision gates passed direct Relay probes. |
| 06-C | COMPLETED | Build 05 passed 3-second start, countdown cancellation, timer and input recovery. |
| 06-D | COMPLETED | Build 06 passed real joint seal and in-range pickup rejection. |
| 06-E | COMPLETED | Build 06 passed first pickup, role copy, input deadline and rejoin. Build 05 covered later gem events. |
| 06-F | IN_PROGRESS | Builds 05/06 passed focused Relay and Master output. Confirm capture fixture compile, then commit and push on live main. Publish the official map and runtime views from the verified clean source. |

Evidence is optionally available in ignored `Docs/Development/Presentation/V4/` and scoped `pres06-*` Relay run folders. Initial failed attempts and the diagnosed timing differences are in `RunSummary.md`. The four fixed layouts passed. This desktop confirmed three actual window sizes and limited the 2560×1440 request. Second human playtest and final integration remain deferred.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
