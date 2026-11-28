# Current task

**V3-NET-CLOSE-01** · Graceful session shutdown and disconnect recovery

**Checkpoint** `READY_TO_COMMIT` · **Base** `3ee464cbc8c8c7c456d12092b051f8c4b1ee3de6`

Complex · Network, Gameplay and UI · Task-Centric Orchestration V2. Local HEAD, origin/main and live main matched. The untracked design PDF and historical Players, captures and evidence are outside the task. Optional ignored evidence is under `Docs/Development/NetworkClose/V1/`.

| Work | Worker | State | Scope and next check |
| --- | --- | --- | --- |
| CLOSE01-A | Explore + Plan | COMPLETED | Session manager, presentation, installed NGO/UTP/MPS teardown and NET-01 history reviewed. |
| CLOSE01-B | Validate | COMPLETED | `Base-C-20261128-174554694` reproduced Connection under C. `Trace-Base-C-20261128-175153098` reproduced it 30.271s after the Host leave request. |
| CLOSE01-C | Diagnose | COMPLETED | Trace baseline requested the existing reason before MPS leave and closed Host UTP about 0.13s after the request. Client did not receive the reason. Exact queue state and Relay wire delivery remain unmeasured. |
| CLOSE01-D | Implement | COMPLETED | Session manager sends a reliable notice before teardown and waits for the current peer's departure. Spawn approval rejects new peers while the Host closes. Fixture changes are Development-only. |
| CLOSE01-E | Validate + Review | COMPLETED | A and three consecutive C/D runs per profile passed. Outbound and Finished re-entry, rematch, D unreachable Host closure and C/D Client loss passed. The ordinary D3D11 pair passed with Development probes and simulator setup excluded. |
| CLOSE01-F | Finalize | IN_PROGRESS | Scope and evidence reviewed. Compare and stage the exact paths, then commit, push and record the verified live hash in ignored evidence. |

Candidate `Build-Dev-20261128-175207804` is traced by `compiled-fixture.txt`, `development-pass-receipts.csv` and the per-run assembly hashes. The baseline and candidate runs use separate evidence folders. See [NET-01 validation](../Playtest/ValidationSummary.md#v3-net-close-01--graceful-session-shutdown) for scope, counters and limits.

Normal run `NormalRelay-20261128-180753815` stopped before room creation when the Windows launcher could not focus a Player. With focus and registry restoration corrected in the local launcher, `NormalRelay-20261128-180903346` passed using the same `Build-Normal-20261128-180515878` artifacts. Official publication is skipped for nonvisual scope. Human playtests, integration and release retain their roadmap states.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
