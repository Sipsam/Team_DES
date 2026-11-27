# Current task

**V3-PRES-05** · Dynamic Music, Ambience and Character Audio

**Checkpoint** `READY_TO_COMMIT` · **Base** `32c51dda151a4b4f4629d884e923700094461e6a`

- Build 06 D3D11 Development source and asset inputs verified. Managed-code comparison against Build 05 found only the 24 → 48 m lava range change. Packed audio data was unchanged.
- Reused Build 05 WASAPI, Master persistence and hard mute, source limits, presentation, local zone choice, movement and session cleanup checks. Build 01 approved-action traces cover PvP, timber, bridge warnings, rowing and launch. Existing 37 cue entries and WAV files match the base.
- Build 06 cavern-hazard and castle/underground Relay passed on both peers. Stair landing traces fell from 57/54 to 4/3. Joint seal, approved first pickup, trap and puzzle cues, rejoin, lava and fire playback passed. Both peers recorded one cue at the first approved pole exchange.
- The incomplete Build 05 castle and Build 06 compilation logs are retained as shutdown records. Optional isolated finish traversal stopped when contest covered server outcomes. Human listening, DSP CPU capture, the second human playtest, full match, full-world and full E2E Relay remain deferred.
- Evidence is optional locally under ignored `Docs/Development/Audio/V2/` and `Docs/Development/Gameplay/V1/RelayRuns/pres05-*`. The distribution playtest build and ZIP are outside these paths. Official screenshot publication is skipped.
- Next · Verify live main, commit and push. Record the verified remote hash and `COMPLETED` receipt in ignored `Audio/V2/RunSummary.md`. Next gameplay work is V3-PRES-06 on user request.

## State and handoff

Use `PLANNED` for queued work, `IN_PROGRESS` for implementation, `VALIDATING` for scoped checks, `BLOCKED` for a prerequisite or unresolved failure, and `AWAITING_APPROVAL` for an explicit user gate. Once required checks and review pass, use `READY_TO_COMMIT`. Use `COMPLETED` after verifying commit and push, and `DEFERRED` for intentionally postponed work.

Track implementation, test, commit, push and official screenshot status independently. A capture failure leaves publication pending with its verified source hash. Record the next unresolved action. Before committing, record the checkpoint, scope, base, validation targets and evidence, failure or gate, and related paths. After committing, record the actual hash and remote and publication outcomes in ignored evidence and the task report. Reconcile the committed checkpoint with Git on resume. When the next authorized task starts, carry the closed result to ROADMAP and replace the active checkpoint as part of that task. Avoid a status-only follow-up commit for the current commit's hash.

For Complex work, record only the subtask rows needed for recovery. Use `PLANNED`, `IN_PROGRESS`, `COMPLETED`, `BLOCKED` or `DEFERRED` for each row. A completed row means its own criteria passed. Its dependents and the top-level task retain their own status. Update rows at meaningful checkpoints, including a failure that changes the next action. On resume, compare the relevant files and evidence inputs against Git. Reopen affected rows if their inputs changed and continue at the first unfinished row whose dependencies are satisfied. Keep the [packet and result fields](WORKERS.md#task-packet-and-result) inline only where those fields add recovery information.
