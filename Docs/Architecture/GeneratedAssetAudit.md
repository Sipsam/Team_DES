# Generated asset lifecycle contract

Commit saved scene/config dependencies with their `.meta` files. `Assets/Game/Level/Data/Generated/Run_*` is not ignored: scenes and drafts require referenced meshes, terrain and materials. Keep every required run and its folder metadata.

Generation creates fresh Run directories; Undo and unsaved scenes may retain older dependencies. Do not delete or deduplicate generated assets solely because hashes match or a saved-scene audit finds no users. Review saved and unsaved references, preserve backups, and validate regeneration, Undo, reopen and serialization in a disposable copy before a separately authorized cleanup.

Do not overwrite stable published assets during normal generation. Any future promotion workflow must preserve GUIDs and compatible object identity, check shared users, and record the source/config version and seed.

Run `python Tools/ProjectAudit/audit.py` from the repository root and the Unity menu `Tools > Twin Edge > Audit project dependencies (read only)` for current evidence. Reports are local-only under `Docs/Development/Architecture/V1/Audit/`; historical analysis is optionally available at `Docs/Development/Architecture/V1/GeneratedAssetAudit.md`. These files are absent from fresh clones and must not be staged.

Agree on Git LFS patterns before introducing large binary assets. Preserve existing generated dependencies until a reviewed lifecycle change replaces this contract.
