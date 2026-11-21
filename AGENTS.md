# Project instructions for Codex

## Local-only development documentation

- Create internal development documents under `Docs/Development/<Feature>/V<N>/`, for example `Level/V1`, `Level/V2`, `Level/V3`, `Gameplay/V1`, or `Network/V1`. Each feature has an independent version sequence.
- Start a new version only for a major development phase. Preserve earlier versions; never overwrite their contents with a later version's design or results. Within the same phase, update that version's existing documents instead of creating a version for every minor edit. Correct obsolete paths without discarding historical evidence.
- Put tools/documentation spanning multiple versions in `Docs/Development/<Feature>/Shared/`, and state the applicable versions. Do not invent a version or move documents whose purpose is unclear; inspect and report them first.
- Technical designs, implementation plans, task analyses, verification results, debugging records, experiments, Codex reports, generated test logs, and temporary notes are local-only by default. This includes automated tool output: create its ignored destination directory before writing.
- Keep team planning, project run instructions, and required architecture contracts separately in trackable locations, such as `Docs/GameDesign/`, `Docs/Architecture/`, `Docs/Archive/`, and `README.md`. These shared instructions must work on a fresh clone without requiring local-only reports; label optional local evidence explicitly.
- Never stage `Docs/Development/` (including with `git add -f`). Inspect tracking before migration. For an already tracked internal report, preserve local bytes and remove only its index entry; report the resulting shared deletion. Never remove shared contracts merely because they are Markdown.
- When migrating, preserve original bytes and a source-to-destination manifest under the ignored documentation tree. Check Markdown links and code output paths; modify only documentation paths and necessary output-directory creation, preserving generator/gameplay behavior.

## Latest V3 development outputs

- Update relevant shared run instructions and architecture contracts in place. Use the existing versioned development folder for investigation notes, logs, and temporary evidence.
- Use `Builds/V3Playtest/` and `Builds/V3Development/` for release playtests and validation respectively. Build to staging and verify its output before replacing a usable player.
- After a map commit, update the ignored root `Screenshots/` with all 14 V3 views named for that commit. Use the Unity capture command or `Tools/Level/Update-MapScreenshots.ps1`. The local post-commit hook can call the same tool. Publish a verified set from a clean project, then retire previous managed captures.
- Keep final test summaries, relevant errors, and reproducible failure evidence. Preview eligible logs with `Tools/ProjectAudit/clean-development.ps1` after a successful run.
- Reuse established build, replay, and editor tools. Use Git history for tracked files. Preserve team originals and unrelated local settings. Check scene, prefab, serialized GUID, generator, and dynamic load dependencies before retiring generated assets.

## Scope and completion

- Preserve existing level generators, scenes, assets, and gameplay functionality. Do not regenerate scenes/assets or change gameplay as part of documentation maintenance.
- Preserve unrelated working-tree and staged changes. Do not automatically commit or push.
- At the end of every task, recommend an English Conventional Commit message, report the development-document paths and Git tracking/ignore state, and describe verification and limitations.
- For this documentation setup, recommend `chore(docs): establish local-only versioned documentation`. Commit shared policy/configuration only (`.gitignore`, `AGENTS.md`); keep development documents out of commits. Review necessary shared reference/output-path edits and index-only removals separately and disclose their effect on teammates.
