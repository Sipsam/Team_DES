# Current task

**V3-WORKFLOW** · Minimal repository-native development orchestration

**Checkpoint** `READY_TO_COMMIT` · **Base** `6498cd4c7bcf6f043c00a675f451fb783985bbb5` · **Updated** 2026-11-26 06:50 KST

- **Goal and scope** Add repository-based routing, task state, scoped validation, Git publication and session recovery. Reuse AGENTS, README, Playtest Guide, Validation Summary and the existing map capture script. Exclude Unity runtime, scenes, assets, packages, Player builds and gameplay tests from this task.
- **Done** Mountain commit already pushed. Official D3D11 PNGs republished from the clean pushed commit. Workflow entry point, roadmap, scoped playtest policies and post-push capture guard implemented.
- **Changes** AGENTS, README, both Playtest documents, both Workflow documents and `Tools/Level/Update-MapScreenshots.ps1`. See Git diff against the base. Local evidence, screenshots and builds are ignored. No approvals are required for this task's verified commit and push.
- **Validation** L0 PASS on the workflow diff against the base, including repository links, command interfaces, all eight workflow dry runs, the capture guard and Unity tree preservation. Unity compilation, builds and gameplay tests are NOT_RUN for this workflow task. L5 and the combined mountain Player check remain DEFERRED.
- **Evidence** [Validation Summary](../Playtest/ValidationSummary.md) for game validation. Optional local workflow record at `Docs/Development/Workflow/V1/Verification.md`. Optional mountain publication manifest at `Docs/Development/Level/V3/MountainRedesign/Finalization/PublishedScreenshots.csv`.
- **Blockers and next action** No known blockers. Commit the exact task paths and verify the push. On resume, check Git and the local record before repeating either operation.
- **Publication** Implementation and validation complete. Commit and push pending at this checkpoint. Official screenshots skipped for this nonvisual task.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.
