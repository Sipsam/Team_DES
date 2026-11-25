# Twin Edge V3 · agent entry point

Twin Edge is a two-player Unity project. Use the editor version in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) (6000.3.27f1), URP on Windows D3D11, and NGO with Unity Authentication and Relay. [README](README.md) covers setup. [Architecture](Docs/Architecture/GameArchitecture.md) maps the stored scenes and runtime and links the approved game design. [Playtest Guide](Docs/Playtest/PlaytestGuide.md) owns validation selection and commands. [Validation Summary](Docs/Playtest/ValidationSummary.md) owns shared results and their source or build limits.

## Start and resume

1. Read [CURRENT_TASK](Docs/Workflow/CURRENT_TASK.md), then consult [ROADMAP](Docs/Workflow/ROADMAP.md) for priorities, dependencies or final integration. Inspect the branch, HEAD, `origin/main`, and staged, unstaged and untracked changes. Compare the record with Git. Verify actual remote `main` before integrating, pushing or publishing.
2. Identify the task's inputs, scope, exclusions, dependencies and completion conditions. Follow the applicable route below. Read the smallest relevant diff, source and evidence excerpts. Reuse implementations, tools, results and builds after verifying that their inputs still match. Diagnose failures before retrying and make the smallest fix that addresses the cause.
3. Implement, run scoped validation, review the final diff and update affected shared contracts and task state. Commit, push, verify the remote and publish official evidence when applicable. Report the commit title, development evidence paths and Git tracking or ignore state, verification and limits. Start another gameplay task only on user request.

At milestones, validation failures, blockers and commit preparation, update CURRENT_TASK with the base, open changes, evidence and next action. After a context reset, usage limit or handoff, compare the saved state with the index and worktree. Reuse evidence whose inputs match and continue from the first unfinished step. If a commit exists locally, resume at remote verification or push. If it is already pushed, resume any pending publication from that commit. Preserve existing edits and investigate any mismatch in the records.

## Context routing

| Task | Open first |
| --- | --- |
| Level design and colliders | [Game Architecture](Docs/Architecture/GameArchitecture.md), `Assets/Game/Level/` config, generator, saved scene and Collider references. Check affected gameplay bindings in `Assets/Game/Gameplay/`. |
| Movement and camera | [Gameplay Systems](Docs/Architecture/GameplaySystems.md), `Assets/_Project/Scripts/Player/PlayerMovement.cs` and `Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs`. Follow spawn, respawn or network references only as affected. |
| Pole, PvP and interaction | [Gameplay Systems](Docs/Architecture/GameplaySystems.md) and [Multiplayer](Docs/Architecture/Multiplayer.md), relevant `Assets/Game/Gameplay/Runtime/`, `Assets/_Project/Scripts/Interaction/` or `Player/` code and `Assets/Game/Gameplay/Testing/` fixtures. |
| NGO and Relay | [Multiplayer](Docs/Architecture/Multiplayer.md), `Assets/_Project/Scripts/Networking/`, affected gameplay state and testing code, and `Tools/Playtest/Run-Relay.ps1`. |
| UI | Approved design linked from [Game Architecture](Docs/Architecture/GameArchitecture.md), the `V3Gameplay.unity` scene and affected prefabs, `NetworkDebugUI.cs` or `V3GameplayBootstrap.cs` for runtime UI. |
| Shader or material | [World materials](Docs/Architecture/GameArchitecture.md#v3-월드-재질), `Assets/Game/Level/Data/V3WorldMaterials/`, actual renderer and references, `Assets/Settings/` and the D3D11 checks in the playtest guide. |
| Build or validation | [Playtest Guide](Docs/Playtest/PlaytestGuide.md), then the referenced existing Unity and PowerShell tools. |
| Documentation | Changed document and its authoritative contracts, settings or tool implementation. |
| Session recovery | CURRENT_TASK, Git diff and the referenced evidence. |
| Final integration | ROADMAP, Validation Summary and the final integration procedures in Playtest Guide. |

Prefer Rider MCP for symbols, references and IDE inspections when available. Use local Git for source and remote state, Unity for scene or runtime checks, and existing PowerShell tools for builds, replay and capture. Use context7 for version-sensitive official API details and codex_apps for GitHub or connected services when local tools cannot supply the answer. Use one authoritative tool per lookup. The workflow also works via Markdown and local Git without Rider or extra MCP setup.

One Codex can cover the logical roles in order. The **Orchestrator** selects scope and dependencies, the **Explorer** finds relevant code and evidence, the **Planner** selects the smallest change and checks, the **Implementer** makes it, the **Validator** checks it and investigates failures, the **Reviewer** checks scope and regressions, and the **State Manager** keeps handoff and publication records. Claim independent review only if it actually occurred.

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
