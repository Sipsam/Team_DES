# Current task

**V3-PRES-01** · uGUI Foundation and Design System

**Checkpoint** `READY_TO_COMMIT` · **Base** `caef441f2d2e33e4aa0865cb57afc6ac0df9ab2c` · **Updated** 2026-11-27 KST

- **Scope** Standard · Explore, Implement, Validate, Review, Finalize · UI and Gameplay input boundary. Approved design and roadmap, common uGUI controls and Editor Showcase.
- **Open changes** The foundation is pushed at `caef441`. Final visual review found the wordmark absent at 2560×1440. Increase its line space and check both bounds and rendered pixels. The untracked team UI PDF remains outside the task diff.
- **Evidence** Local UI design PDF §3 and approved A–G compared with legacy V3 bindings. Optional ignored receipts and render targets in `Docs/Development/Presentation/V1/`. The first pushed UI images are retained in `FirstPushed/`. No Player rebuild.
- **Validation** D3D11 `check-05.log` passed the input, glyph, modality, fade, idle legacy path and four-size layout checks, including the new wordmark bounds and raster assertions. The corrected four layouts passed visual review. [Shared results](../Playtest/ValidationSummary.md#v3-pres-01--ugui-공통-ui). No Player or Relay run. Second human playtest still awaits two testers and final integration is deferred.
- **Next** Recheck the four D3D11 sizes and wordmark raster coverage, then push and publish from the corrected source. Resume V3-PRES-02 from the approved design and the root API on user request.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
