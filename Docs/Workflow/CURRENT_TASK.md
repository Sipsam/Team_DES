# Current task

**V3-FEEDBACK-08** ? Castle Environment and Puzzle UX

**Checkpoint** `READY_TO_COMMIT` ? **Base** `009fe65ae54d22e8aabea5bbb263040832a5a0c0` ? **Updated** 2026-11-27 KST

- **Scope** Feedback 14?19. Castle stone floor, stair render seams and enclosure, wall guidance, sewer records and authoritative door pad presentation.
- **Open changes** Fortress generator, derived stair masonry and saved level, castle layout and progress, castle validator and replay, and shared documentation.
- **Validation** Final saved scene and five D3D11 Editor views passed. Isolated Windows D3D11 Relay ran the four castle stages, basement checkpoints, stair descent and ascent, seal, Return trap and sewer routes, and two reconnects with exits 0/0. The final saved scene extends the gallery ceiling and trims the cavern entry render seam. Existing walking Colliders and puzzle rules match the passing Relay inputs. Full-world, full E2E and full match are deferred.
- **Evidence** Optional local `Docs/Development/CastlePuzzle/V1/Verification.md` and `Docs/Development/Gameplay/V1/RelayRuns/Feedback08-CastleVerified/`.
- **Next** Verify actual remote main, commit and push the scoped files, then publish official D3D11 maps and owner-camera images from the verified source.

| Work | Logical Worker and domain | State | Evidence |
| --- | --- | --- | --- |
| Saved flooring and stair enclosure | Implement / Validate ? Level / Rendering | `COMPLETED` | Saved scene validator, retained Collider records, D3D11 chamber and descent views. |
| Wall guidance, pads and sewer record | Validate ? Gameplay / Network / UI | `COMPLETED` | Both peers confirmed accepted caps, wrong input and stage reset, solved records and sewer entry. |
| Commit, push and captures | Review / Finalize ? Workflow | `IN_PROGRESS` | Verify live main and capture the clean pushed source. |

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
