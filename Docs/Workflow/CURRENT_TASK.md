# Current task

**V3-STABILITY-01** · Bridge Support and Carry Diagnosis

**Checkpoint** `READY_TO_COMMIT` · **Base** `862d0dee4644612525ac20c120076841874c9bd8`

Complex · Gameplay, Physics and Network. HEAD, origin/main and live main agreed at entry. Preserve the untracked design PDF, historical bridge runs, published Players and ZIPs, and the 216 protected captures. Both human playtests await reports. V3-INTEGRATION and V3-RELEASE stay deferred.

| Subtask | Worker | State | Evidence or next action |
| --- | --- | --- | --- |
| ST01-A | Explore + Diagnose | `COMPLETED` | Run-015 was headless. Its Host ground flag was followed by a missed support ray at 7→4. PRES-09 023322 failed in zero-based carry 8 after both peers observed installation 8. The bridge pair overlapped a separately logged batch Editor build. |
| ST01-B | Explore | `COMPLETED` | Reviewed the owner CharacterController and input, server carry and collapse, replicated deck and checkpoints, visual grips, and isolated Bridge Relay driver. |
| ST01-C | Implement + Validate | `COMPLETED` | Three unchanged runs passed. Build-Trace-20261128-054012478 adds opt-in capture. Exact Run-015 route passed in TraceD3D-20261128-054256235 and TraceHeadless-20261128-054619580. Both landed on Plank_004. Both passed the 8th and 9th installations. |
| ST01-D | Diagnose | `COMPLETED` | TraceLoaded-20261128-054927100 reproduced release and fall on delivery 8. At server time 59.887, timber z=503.594 and the Client replica z=501.070 exceeded the current 2.5 m grip limit. The owner was still grounded near the grip. TraceEditor-20261128-055053375 passed deliveries 8/9 and exact 7→4. The original delivery 9 trigger and QA-03 remain unconfirmed. |
| ST01-E | Implement | `COMPLETED` | Gate a proposed server carry step with the existing holder validity check. Preserve current-pose reset, clearance, RPCs, owner movement and installation checks. Repeat Driver carries via ordinary cancellation and fresh pairing. |
| ST01-F | Validate + Review | `COMPLETED` | FixNormal-20261128-055735876 and FixLoaded-20261128-060225689 passed 10/10 carries each for 8 and 9 on both peers plus the exact Return route. The corrected guard did not activate in the repeated target windows. FixRotation-20261128-060726988 passed rotation, reconnect and recovery. |
| ST01-G | Finalize | `IN_PROGRESS` | Source and shared contracts reviewed. Preservation-20261128-060805328 verified 216 protected captures, distributed Players and ZIPs, 659 prior evidence files and the untracked PDF. Stage exact paths, commit, push and compare the full hash to live main. |

Local evidence and publication receipt are under ignored `Docs/Development/BridgeStability/V1/`. The only runtime behavior change is the proposed carry-step guard. Official capture publication is skipped for this physics and replay change. Whole match, whole-world regression and full E2E Relay are deferred. [Validation and limits](../Playtest/ValidationSummary.md#v3-stability-01--다리-지지-및-목재-운반)

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
