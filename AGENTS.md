# Twin Edge V3 · agent entry point

Twin Edge is a two-player Unity project. Use the editor version in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) (6000.3.27f1), URP on Windows D3D11, and NGO with Unity Authentication and Relay. [README](README.md) covers setup. [Architecture](Docs/Architecture/GameArchitecture.md) maps the stored scenes and runtime and links the approved game design. [Playtest Guide](Docs/Playtest/PlaytestGuide.md) owns validation selection and commands. [Validation Summary](Docs/Playtest/ValidationSummary.md) owns shared results and their source or build limits.

## Start and resume

1. Read [CURRENT_TASK](Docs/Workflow/CURRENT_TASK.md), then consult [ROADMAP](Docs/Workflow/ROADMAP.md) for priorities, dependencies or final integration. Inspect the branch, HEAD, `origin/main`, and staged, unstaged and untracked changes. Compare the record with Git. Verify actual remote `main` before integrating, pushing or publishing.
2. Identify the task's inputs, scope, exclusions, dependencies and completion conditions. Select the lightest execution path that covers the work and open the affected Domain entry points.
3. Finalize once for the whole request. Update affected shared contracts and task state, and follow the validation and publication rules below. Report the commit title, evidence paths and Git tracking or ignore state, verification and limits. Start another gameplay task only on user request.

At milestones, validation failures, blockers and commit preparation, update CURRENT_TASK with the base, open changes, evidence and next action. After a context reset, usage limit or handoff, compare the saved state with the index and worktree. Reuse evidence whose inputs match and continue from the first unfinished step. If a commit exists locally, resume at remote verification or push. If it is already pushed, resume any pending publication from that commit. Preserve existing edits and investigate any mismatch in the records.

## Task dispatcher

| Path | Use when | Execution |
| --- | --- | --- |
| Fast | Target and effect are clear and localized, such as a number, straightforward fix, small document edit or simple material value. | Direct execution → scoped validation → Finalize. Skip task packets, decomposition and separate handoffs. |
| Standard | A routine feature or related changes need bounded discovery and regression checks. | Explore → Implement → Validate → Review → Finalize. Choose only needed Worker roles and combine overlapping checks. |
| Complex | Physics, networking, terrain generation or cross-system changes have uncertain impact, meaningful dependencies or separate acceptance gates. | Explore → Plan and decompose → assign Workers → Implement → Validate → Review → Finalize. Record scope, dependencies and completion only for subtasks that benefit from being tracked. |

The same Codex performs [Worker roles](Docs/Workflow/WORKERS.md) in the active session. Assign roles and Domains independently, such as Diagnose + Rendering. Keep subtasks sequential by default. Reclassify when new findings change the scope or risk. If a check fails, use Diagnose for that failure before retrying. Required validation and the final diff check apply on every path.

## Domain routing

| Domain | Open first |
| --- | --- |
| Level | [Game Architecture](Docs/Architecture/GameArchitecture.md#레벨-편집과-생성물), relevant `Assets/Game/Level/` config, generator, saved scene and Collider references. Follow affected gameplay bindings in `Assets/Game/Gameplay/`. |
| Gameplay | [Gameplay Systems](Docs/Architecture/GameplaySystems.md), then [PlayerMovement](Assets/_Project/Scripts/Player/PlayerMovement.cs) or [ThirdPersonCamera](Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs) for movement or camera, or the relevant `Assets/Game/Gameplay/Runtime/`, `Assets/_Project/Scripts/Interaction/` and `Assets/Game/Gameplay/Testing/` paths for pole, PvP and interaction. Follow spawn and respawn references when affected and add Network for authority or synchronization changes. |
| Network | [Multiplayer](Docs/Architecture/Multiplayer.md), `Assets/_Project/Scripts/Networking/`, affected gameplay state and test code, and `Tools/Playtest/Run-Relay.ps1` for isolated peer checks. |
| Rendering | [World materials](Docs/Architecture/GameArchitecture.md#v3-월드-재질), `Assets/Game/Level/Data/V3WorldMaterials/`, actual renderer, affected references and `Assets/Settings/`. Use the D3D11 checks in the Playtest Guide. |
| UI | Approved design linked from [Game Architecture](Docs/Architecture/GameArchitecture.md), then the relevant `V3Gameplay.unity` scene or prefabs and, for runtime UI, `NetworkDebugUI.cs` or `V3GameplayBootstrap.cs`. |
| Workflow | Changed guidance or tool and its authoritative contract. For builds or validation, start from the Playtest Guide and referenced tools. For recovery use CURRENT_TASK, Git and referenced evidence. For final integration use ROADMAP, Validation Summary and the Playtest Guide. |

Read the smallest relevant diff, source and evidence excerpts. Prefer Rider MCP for symbols, references and IDE inspections, with scoped local search as fallback. Use local Git for source and remote state, Unity for scene or runtime checks, and existing PowerShell tools for builds, replay and capture. Use context7 for version-sensitive official API details and codex_apps for connected sources when local tools cannot supply the answer. Use one authoritative tool per lookup.

## Validate and publish

- Follow the scoped checks in Playtest Guide. Keep full E2E Relay, full automated matches and full-world integration for the final stage after major development is complete and the user explicitly starts final integration. Report deferred tests separately from failures or blockers.
- For validated work on `main`, commit and push to `origin/main` without a routine approval round. Honor explicit design approval, manual-review, commit or push holds. Unresolved required checks block completion. Intentional final integration deferrals can ship with their limits recorded.
- Before commit, fetch or compare actual remote `main` and check ancestry, scope, staged/unstaged/untracked files, required evidence, sensitive data, generated outputs and team-owned assets. Run `git diff --check`, stage exact task paths and review the staged diff with `git diff --cached --check`. If the index contains unrelated work, preserve it and isolate the commit or stop if the scopes cannot be separated safely.
- Use English Conventional Commits, `<type>(<scope>): <imperative summary>`, with English bullets describing the actual work and checks. Common types are `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `build`, `ci` and `chore`. Keep tasks in independent commits. Push normally and compare the full local hash with `git ls-remote --exit-code origin refs/heads/main`.
- On remote advancement inspect the incoming diff and integrate only when safe. Revalidate affected inputs. Preserve local commits and index contents after failure. Respect branch protections. Force pushes, destructive resets, unauthorized history rewriting, discarding or stashing user work, and deciding conflicts on a teammate's behalf are prohibited.
- For visual work, verify the pushed commit and use the [official capture procedure](Docs/Playtest/PlaytestGuide.md#official-screenshots). Track publication independently. For nonvisual work, report it as skipped. A failed publication resumes from the pushed source without a new commit or full replay.

## Evidence and assets

- Keep team workflow state, project instructions and architecture contracts tracked and usable from a fresh clone. Put internal notes, raw logs, debug records, experiments and temporary analysis in ignored `Docs/Development/<Feature>/V<N>/`. Version each feature independently at major phases and preserve earlier versions. Use `Shared/` for evidence or tools spanning specified versions. Create ignored output directories before writing.
- Never stage `Docs/Development/`. Check tracking before a migration, preserve original bytes and an ignored path manifest, remove only the index entry for a tracked internal report, and disclose the shared deletion. Check links and output paths. Preserve shared contracts and historical evidence.
- Record enough to identify the source commit or working diff, relevant artifact version or hash, environment, command, validation scope, result and evidence location. Label local-only paths optional in shared summaries. Keep reproducible failure evidence and redact room codes, tokens, keys and credentials. Preview completed-run cleanup with `Tools/ProjectAudit/clean-development.ps1` before applying it.
- Keep normal and Development Players in ignored `Builds/V3Playtest/` and `Builds/V3Development/`. Build into staging and verify before replacing a usable player. Preserve local settings, team originals and unrelated staged and unstaged changes. Check scenes, prefabs, GUIDs, generators and dynamic loading before retiring generated assets. Keep documentation work free of scene, asset and gameplay changes.
