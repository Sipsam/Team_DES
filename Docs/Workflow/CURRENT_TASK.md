# Current task

**V3-PRES-02** · Main Menu, Lobby, Nickname and Loading

**Checkpoint** `READY_TO_COMMIT` · **Base** `fe1db9774fc41ebd85a4db1627aa91dd19bbba11` · **Updated** 2026-11-27 KST

- **Scope** Complex · UI, Network, Rendering. Logical Workers run sequentially in this session. Menu, nickname, Relay presentation, lobby previews, loading and manual rejoin.
- **Inputs** Live main matches HEAD and origin/main. The untracked team UI PDF was reviewed at pp. 4–5 and remains outside the task diff. PRES-01's fix is pushed; its foundation check evidence remains in Presentation/V1.
- **Open changes** Server-approved nickname, replica slot/readiness, session outcomes, V3 uGUI screens, camera and lobby previews, and shared contracts are in the working tree. `Builds/V3Presentation02/` is the isolated Development Player destination.
- **Evidence** Optional ignored `Docs/Development/Presentation/V2/`. `check-03.log` passed the four-size UI and preview lifecycle checks. `build-05.log` and `verified05-run.txt` passed the Windows D3D11 Relay UI scope, including malformed, full and closed rooms, two live portraits and manual slot and cape restoration. The user confirmed Hangul composition, deletion, commit and clipboard paste with a physical keyboard on candidate 04. The nickname field inputs match candidate 05.
- **Next** Commit the reviewed task paths, verify and push to live main, then publish map and six Player views from the verified source. Record capture and remote receipts independently. The second human playtest awaits testers and final integration remains deferred.
## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
