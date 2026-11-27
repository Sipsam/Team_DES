# Current task

**V3-PRES-10** · Presentation Integration and Legacy UI Cleanup

**Checkpoint** `READY_TO_COMMIT` · **Base** `04b92d13dfa7040aed0e3fff37ec84c1e341455e`

HEAD, origin/main and live main matched at entry. PRES-09 is pushed and 38 official images match its publication manifest. Keep the rejected first map and concurrent transition attempts. The pre-existing untracked design PDF is outside scope.

Complex · UI, Gameplay, Network, Audio and Rendering · Lightweight Task-Centric Orchestration V2. Use isolated evidence and builds, preserve the second human playtest Player and ZIP, and leave V3-INTEGRATION deferred.

| Scope | Worker and dependency | State and exit |
| --- | --- | --- |
| 10-A | Explore and Plan · active UI, GUIDs, approval and historical evidence | COMPLETED · V3 keypad was the remaining player-facing IMGUI, with F3 and old prototype components retained |
| 10-B | Implement and Validate · server open and submit plus root modal | COMPLETED · keypad input, server approval, door and reconnect passed |
| 10-C | Implement and Review · active V3 legacy callers | COMPLETED · keypad IMGUI removed and F3 method development-gated |
| 10-D | Validate · 10-B/C and previous evidence | COMPLETED · Relay, normal smoke and three Editor checks passed |
| 10-E | Validate · isolated representative windows and lifecycle | COMPLETED · six sampled views and keypad/rematch counts, unavailable counters recorded |
| 10-F | Finalize · required focused checks | IN_PROGRESS · exact stage review, verified push and official capture |

Ignored evidence is under `Docs/Development/Presentation/V3-PRES-10/`. `Run-Audit-20261128-034248` records prior official image hashes, protected distribution hashes and serialized GUID references. `Run-KeypadCandidate-20261128-035011` reached the actual keypad through all four stages and passed Escape, map and close checks. It failed the combined digit assertion after fixed-size capture. Preserve that run. The retry lets the window canvas layout settle before the next pointer event and checks mouse and keyboard separately.

Development Player 035102 passed `presentation`, `settings`, `transition` and `rematch`. The retry build 035812 changed the keypad test capture path and passed both peers in `Run-KeypadVerified-20261128-040656`. The four fixed-size captures preserve readable text and mask the private HUD number. Normal Player `Run-NormalSmoke-20261128-035856` passed actual 1280×720 menu, settings and F3/non-inclusion checks. Foundation, saved Castle and Presentation checks all exited 0 in `Run-UnityChecks-20261128-040212`. Representative measurements and limits are in [Validation Summary](../Playtest/ValidationSummary.md#v3-pres-10--프레젠테이션-통합과-성채-키패드).

The official checkout is `D:/TwinEdge-Pres10-Capture`, currently at the base. After exact stage review and push, pin it to the verified commit for D3D11 shader warm-up, map publication and keypad replay. Record the resulting hash and capture receipt in ignored PRES-10 evidence. The original 198 screenshots, 208 second playtest Player files and ZIP match the preservation manifest.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
