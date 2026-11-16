# Generated asset lifecycle contract

Commit saved scene/config dependencies with their `.meta` files. `Assets/Game/Level/Data/Generated/Run_*` is not ignored: scenes and drafts require referenced meshes, terrain and materials. Keep every required run and its folder metadata.

Generation creates immutable candidate directories; Undo and unsaved scenes may retain older dependencies. Saving publishes scene references without replacing existing GUIDs. Do not delete or deduplicate assets during the generating session. Failed generation leaves the published tree intact. No duplicate backups or archives are required; retain Git history and protect unique uncommitted work.

After saving all scenes and normally closing Unity, run `Tools/ProjectAudit/clean-generated.ps1`. It launches a fresh Unity batch session and calls `ValleyAssetLifecycleV3.CleanFreshSession`, using AssetDatabase transitive dependencies from **all** non-generated assets, including V1/V2, test scenes, prefabs, presets and Resources. Serialized ownership-directory strings are also retained. Only whole known generator folders containing meshes/materials/meta files qualify. Untracked files are preserved unless the current V3 generator explicitly recorded ownership. Unknown content and local-only historical copies are reported for review. Never run cleanup during generation, Play mode or an interactive Undo session.

Use this cleanup at the end of each generation session to bound persistent storage to published dependencies. Multiple generations in an open session intentionally consume temporary storage until the session ends. Cleanup does not consolidate shared dependencies or rewrite GUIDs. Review Git deletions along with the updated scene and its new dependencies; teammates need the published meshes and meta files in the same change.

Do not overwrite stable published assets during normal generation. Any future promotion workflow must preserve GUIDs and compatible object identity, check shared users, and record the source/config version and seed.

Run `python Tools/ProjectAudit/audit.py` from the repository root and the Unity menu `Tools > Twin Edge > Audit project dependencies (read only)` for current evidence. Reports are local-only under `Docs/Development/Architecture/V1/Audit/`; historical analysis is optionally available at `Docs/Development/Architecture/V1/GeneratedAssetAudit.md`. These files are absent from fresh clones and must not be staged.

Agree on Git LFS patterns before introducing large binary assets. Preserve existing generated dependencies until a reviewed lifecycle change replaces this contract.
