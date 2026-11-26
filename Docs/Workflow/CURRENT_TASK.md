# Current task

**V3-PRESENTATION-AUDIT** · Presentation and Polish Gap Audit

**Checkpoint** `READY_TO_COMMIT` · **Base** `7f17e6c1505af8fb4c3386c22c995dea6faa8468` · **Updated** 2026-11-27 KST

- **Scope** Standard · Explore, Review, Finalize · UI, Gameplay, Rendering. Audit the active V3 Player paths and propose presentation work for user review. Documentation only.
- **Open changes** [Audit](../GameDesign/PresentationAudit.md), [ROADMAP](ROADMAP.md) and this checkpoint.
- **Evidence** Live main matched the clean base. The previous V3-PLAYTEST-02 documentation was published at `7f17e6c`. The audit references the approved PDF, active scene/prefab bindings, prior general Player smoke and existing official views. Optional local L0 record and command are in `Docs/Development/PresentationAudit/V1/`.
- **Validation** PASS L0 links/anchors, enabled scenes, player/controller/clip and fire prefab references, audio inventory, documentation-only scope and diff. No new Unity, Player, Relay or screenshot runs. The second human playtest awaits two testers. Final integration stays deferred.
- **Next** Commit and verify push. Then await second human playtest results and user decisions on Presentation candidates.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
