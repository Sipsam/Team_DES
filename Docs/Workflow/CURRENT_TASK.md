# Current task

**V3-NET-BASELINE** · Network traffic and resilience baseline

**Checkpoint** `READY_TO_COMMIT` · **Base** `bda29df0e645ecc4a2bc60d78552762119e0e654`

Standard · Network, Gameplay and Performance. Local HEAD, origin/main and live main matched at entry. The pre-existing untracked design PDF is outside the task. V3-PERF-02 is published in the base commit.

NET-A confirmed the installed NGO profiling hooks and UTP driver and connection counters. The scope includes opt-in Development telemetry, the lobby and movement matrix, genuine input at isolated bridge and contest spawn stations, and the isolated result setup with live pickup and finish approval. NET-B/C exploratory A and C collected lobby and movement samples but used hidden windows and missed the marker lookup. Their after-restore motion check sampled one half of an input cycle. D opened visible windows and its reconnect barrier expired before the transport's disconnect timeout. The candidate discovers registered profiler handles, samples both input directions across frames and allows the actual disconnect deadline.

Visible A and C passed lobby, motion and Outbound rejoin. Both A receivers got all 300 calibration messages. C missed 3 at Host and 2 at Client. Guard-A recorded all four expected branches. Result-C approved the finish on both peers but its driver waited for an interior coordinate after Finished had stopped movement. The next candidate uses the actual finish collider. The prior recorder reset returned only its first marker sample. The next candidate reads each retained sample once.

The retained recorder candidate collects per-frame markers. Guard-C passed all four branches and resumed after a fresh remote transform. Result-C passed live pickup, both stale revision gates, grounded finish, Finished rejoin and same-room scene rebuild. Host closed and made a new room. Client released the original driver at roughly 30 seconds without reaching the explicit HostLeft branch. A follow-up measurement distinguishes the closed-room classification from gameplay recovery. The combat steps passed on both peers before its early cleanup deadline. The candidate restores the link setup before combat cleanup.

The retained-marker A, B and D runs and the earlier C transport run all passed the two Runtime Ready checks, movement and Outbound rejoin. Profile D observed 9/5 missing diagnostic messages, 443/444 ms movement RTT medians and a confirmed return to zero settings. C and D both showed increased reliable retransmission during the roughly 30-second shutdown detection window. The shared draft tracks this separately from steady traffic.

E verified pre-start Ready reset, lobby and Outbound rejoin, explicit HostLeft on the normal link and creation of a fresh room. Combat and guard passed in A and C. The A result passed and the C classification rerun confirmed Finished rejoin, scene rebuild, clean shutdown and a new Host room. C reproduced a 30-second generic Connection classification. The shared summary records NET-01 and the separate follow-up candidate.

The isolated normal Player built and opened an actual D3D11 window with Development test flags. Assembly inspection and the boot output confirmed fixture exclusion. Optional local evidence is in ignored `Docs/Development/NetworkBaseline/V1/`. The `latest-matrix.txt`, `latest-post-matrix.txt` and `latest-normal-boot.txt` pointers identify the run and exclusion receipts.

Review the exact staged diff, compare live main, commit and push, and retain the verified remote hash in local finalization evidence. Official screenshot publication is skipped for this nonvisual scope. QA-03 and the original ninth timber carry remain intermittent. Human playtests, final integration and release keep their roadmap states.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
