# Current task

**V3-PRES-03** ? Gameplay HUD, Minimap, Onboarding and Lobby Readiness

**Checkpoint** `READY_TO_COMMIT` ? **Base** `70584ece32f614ea0b296f29b3498f4041c47a6e` ? **Updated** 2026-11-27 KST

- **Scope** Complex ? UI, Gameplay, Network, Level, Rendering. Sequential Worker roles in this session. The local team UI PDF remains outside the task.
- **Validated paths** Production server Ready/Start gate, SessionPresentation, HUD, tutorial and map, saved public atlas, scoped Relay drivers, and shared contracts. 03-A through 03-F passed their focused acceptance checks.
- **Evidence** Optional ignored `Docs/Development/Presentation/V3/` and `Docs/Development/Gameplay/V1/RelayRuns/`. `bake-01.log` and `build-07.log` passed. Build 07 Relay `pres03-relay-05` and `pres03-raft-02` passed on both peers. Build 06 control and PvP evidence `pres03-controls-02` and `pres03-contest-01` still matches the tested inputs. Resolution checks use fixed D3D11 targets. Region and gem setup views record fixture placement and authoritative events.
- **Next** Complete 03-G by verifying remote main, committing and pushing the reviewed paths, then capturing the official map and UI from the verified source. Detailed result and publication receipts belong in ignored evidence.
- **Deferrals** Second human playtest and final integration remain pending. Existing second human playtest Player and ZIP are outside this task. V3-PRES-04 awaits user initiation.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
