# Current task

**V3-WORKFLOW-V2** · Lightweight task-centric orchestration

**Checkpoint** `READY_TO_COMMIT` · **Base** `ea911d56ee8e92d566f04f6812f882fca29581b2` · **Updated** 2026-11-26 KST

- **Goal and scope** Document single-session task dispatch, logical Workers, Domain routing, compact recovery and cost measurement. Work is limited to repository guidance. Gameplay, assets, tools, builds and full integration are outside this task.
- **Done** Reconciled V1 with local and live main. Verified Rider MCP repository and symbol lookup and checked documented IDE capabilities. Added the dispatcher, Domain routes, Worker registry and scoped recovery rules.
- **Changes** AGENTS and the three Workflow documents.
- **Validation** L0 PASS on 26 relative links and anchors, Domain entry paths, policy, tool interfaces and exact Git scope. All ten document dry runs passed. Unity, Relay, Player builds and capture were not run. Existing final integration deferrals still apply.
- **Evidence** Optional local record at `Docs/Development/Workflow/V2/Verification.md`.
- **Blockers and next action** No known blockers. Commit the scoped document paths, push and verify live main. On resume, reconcile this checkpoint with Git before repeating either operation.
- **Publication** Commit and push pending. Official screenshots skipped for this nonvisual task.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.

| Subtask ID | Worker | Dependencies | Status | Relevant files | Result reference | Next action |
| --- | --- | --- | --- | --- | --- | --- |
| V2-01 | Explore | — | `COMPLETED` | AGENTS, Workflow and Playtest guidance | V1 commit and Rider MCP checks | Use the verified environment limits. |
| V2-02 | Implement | V2-01 | `COMPLETED` | AGENTS and Workflow documents | Working diff | Validate the workflow changes. |
| V2-03 | Validate | V2-02 | `COMPLETED` | Scoped diff and referenced interfaces | Local verification record | Finalize the full request. |
