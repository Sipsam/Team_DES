# Current task

**V3-PRES-01** · uGUI Foundation and Design System

**Checkpoint** `READY_TO_COMMIT` · **Base** `a6d1e15cc04887b7bf7ca56b3cebc5867114857d` · **Updated** 2026-11-27 KST

- **Scope** Standard · Explore, Implement, Validate, Review, Finalize · UI and Gameplay input boundary. Approved design and roadmap, common uGUI controls and Editor Showcase.
- **Open changes** `Assets/Game/UI/`, V3 bootstrap, gameplay input and camera gates, design and shared workflow links. The untracked team UI PDF is a local input and remains outside the task diff.
- **Evidence** Local UI design PDF §3 and approved A–G compared with legacy V3 bindings. Optional ignored receipts and render targets in `Docs/Development/Presentation/V1/`. No Player rebuild.
- **Validation** D3D11 `check-04.log` passed after the initial virtual pointer focus failure was corrected in the Editor test setup. Four layout sizes, glyphs, controls, modality, fade, idle legacy path and direct input regression passed. [Shared results](../Playtest/ValidationSummary.md#v3-pres-01--ugui-공통-ui). No Player or Relay run. Second human playtest still awaits two testers and final integration is deferred.
- **Next** Verify exact staged scope and live main, commit and push. Capture UI first from the pushed checkout, then run the official map procedure in the same checkout. Resume V3-PRES-02 from the approved design and the root API on user request.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
