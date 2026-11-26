# Current task

**V3-FEEDBACK-03** · Cooperative Pole Traversal

**Checkpoint** `READY_TO_COMMIT` · **Base** `c512ce7d8db7da8fb162d78307554e88038b213e` · **Updated** 2026-11-26 KST

- **Scope** Feedback 3 and 6. Keep DeepGullies, FinalPolish, the 32 m rigid pole and server-owned endpoint snapshots. Full E2E Relay and full match are deferred.
- **Source** Clean local, tracked remote and live main matched. Feedback 2 is pushed and its official captures are recorded.
- **Evidence** Optional local `Docs/Development/CooperativePole/V1/` contains the approved 94-edge baseline and retained route plans.
- **Validation checkpoint** The saved terrain graph has 103 directed candidates and preserves all 94 approved IDs. All nine added connections have sampled shaft-only intersections with saved rock. The structural check confirmed separation at the first crossing and no Start-to-Final walking connection. Four approved and three representative continuous plans passed. The corrected representative Western route follows the approved chain through Peak_25.
- **Peer checkpoint** D3D11 camera controls and pole recovery passed both peers. The initial Eastern replay stalled because the test driver's requested fine steering fell below the ordinary input dead zone. Its driver now keeps fine motion above that threshold. Runtime movement and acceptance rules are unchanged by that correction.
- **Final-build peer checkpoint** Both peers passed camera controls, pole recovery and the Eastern mountain replay under D3D11. The first landing, cross-summit pivot move and role exchange passed while the shaft intersected the midspan fixture. The moved endpoints differed in height by 4.122m, remained 32.000m apart and matched exactly between peers. Both owner views show the landing ribbon. The replay completed 264 actions and nine exchanges, reached Big Summit with both players and passed gravity fall and checkpoint recovery.
- **Next** Verify live main and the exact staged scope, then commit and push. Verify the pushed hash and use both the official map capture and the affected runtime player views. Record the commit and capture provenance in local evidence.

| Work | Logical Worker and domain | State | Dependency and acceptance |
| --- | --- | --- | --- |
| Server pole movement and collision | Implement · Gameplay / Network | `COMPLETED` | Same-summit, airborne-orbit and distinct-summit movement passed. Shaft contact permits landing and exchange. Character capsule failure and checkpoint recovery passed. |
| Summit edge visuals | Implement · Level / Rendering | `COMPLETED` | Production support and capsule predicates drive the 29 render-only boundaries. Both player views passed visual review. |
| Graph, continuous routes and isolated peers | Validate · Level / Network | `COMPLETED` | 94 baseline edges preserved, nine new arcs verified, approved continuous plans passed, and three isolated Relay cases exited 0/0. |
| Review, push and screenshots | Review / Finalize | `IN_PROGRESS` | Scoped review passed. Verify live main, push and capture the official map and runtime views. |

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
