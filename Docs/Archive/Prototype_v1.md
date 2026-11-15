# Prototype v1 archive and recovery

## Verified historical reference

The existing local tag **`prototype`** resolves directly to commit **`327c6a2aa730a2c0dd3e5cb3f69342014778e31e`**, `feat(level): build complete round-trip prototype map` (2026-10-28). The tree contains both `Assets/_Project/Scenes/MainPrototype.unity` and `Prototype_01.unity`, their metadata, all prototype scripts, prefabs/materials, Packages and ProjectSettings. It contains no `Assets/Game` or current top-level `Docs`. This is the appropriate complete prototype baseline. No tag was created, changed or pushed. `prototype-v1` would be an optional descriptive alias of this exact commit after review; an additional archive branch is unnecessary.

The historical prototype implements host/client spawning, ownership and transform synchronization, Relay session/join-code flow, movement/camera, push/grab/hit interaction, bridge switches, cooperative raft/moving-platform carrying, asymmetric door puzzle, jump handoffs, phase changes, individual checkpoints/respawn and first-arrival winner. Main scene and prefab references, builder code, Git history and the historical integration guide establish this classification; folder names alone do not.

Key sources: `_Project/Scripts/Networking` (MultiplayerSessionManager, NetworkSpawnCoordinator, OwnerNetworkTransform, NetworkDebugUI), Player (PlayerMovement, PlayerCombat), Interaction (PlayerInteraction, IInteractable), Camera (ThirdPersonCamera), Game (GameStateManager, PrototypeLevelFlow, RespawnManager, Checkpoint, TurnPoint, Goal, KillZone), Gimmicks (Bridge, Raft, DoorPuzzle, JumpPuzzle). Builders are in `_Project/Editor`; opt-in development smoke driver is `_Project/Tests/Runtime/IntegratedCourseSmoke.cs`. Original guides and executed historical tests are in `_Project/Docs`.

## Recovery without disturbing current work

```powershell
git rev-parse 'prototype^{commit}'
git ls-tree -r --name-only prototype -- Assets/_Project Packages ProjectSettings
git worktree add --detach ../Team_DES-prototype 327c6a2aa730a2c0dd3e5cb3f69342014778e31e
```

Open that separate project folder in Unity 6000.3.27f1, allow package import, then open MainPrototype. Do not use restore/reset over the active dirty checkout. For a fresh clone where the tag has not been fetched, first verify the remote tag and fetch it explicitly. Remote tag availability was not asserted by this audit. The commit is reachable from the current local main/origin-main histories.

Dependencies retained: NGO 2.13.3, Multiplayer Services 2.4.1, Input System 1.20.1, URP 17.3.0, UGUI, Unity Transport/Services transitive dependencies in packages-lock. Actual Relay use requires valid Unity Services access. The global `Assets/DefaultNetworkPrefabs.asset` refers to NetworkPlayer and Greybox/MovingPlatform; both must be preserved before any legacy code exclusion. Rendering and input configuration remain shared.

## Differences and reuse boundaries

The latest integrated eight-page design is authoritative. It requires gem possession at the summit to win, joint seal release, independent gem/player respawn, stamina, carry/throw/resistance, rotating poles and constructed/collapsing bridge, cooperative/split rowing, four parallel room stages converting to a trap corridor, and timed/permanent underground platforms. The old first-arrival winner, push action, simple bridge switches and older raft/door/jump implementations do not establish compliance. The earlier 8–15 minute target is superseded by the design's provisional 30–60 minutes.

Candidate reusable infrastructure includes session/ownership plumbing, interaction contracts, camera/movement reference dimensions and checkpoint patterns. Migrate these only after API, authority and latest-rule review. V2 uses numeric player dimensions as design references; this does not require the old controller in its scenes.

## In-project status and local preservation

`Assets/_Project` remains a clearly documented legacy compatibility area. No scripts or scenes were deleted, duplicated or relocated by this task. Legacy scripts still compile in predefined Unity assemblies, and the prototype is still the enabled build entry. Historical archiving is complete through the existing Git reference; **compilation isolation is deferred**, not claimed. An `_Archive` folder alone would not isolate C# compilation, and adding asmdefs across NGO/editor/smoke code without a gameplay migration would introduce unnecessary risk.

Before changes, the working tree already had a modified MainPrototype, deleted Prototype_01 and metadata, modified EditorBuildSettings/URPProjectSettings, and untracked current level tools/docs. These are not part of the historical tag. A verified local working-tree ZIP (2,577 files), SHA256 manifest, pre-task status, binary diff and complete verified Git bundle were saved at `C:/Users/sipsa/Team_DES_Backup_20261116_023403`. This machine-local backup includes saved files only, not unsaved Editor objects, Library or credentials. Preserve it until the current level work is committed and shared. The deleted Prototype_01 remains recoverable in the tag and bundle; its local deletion was not reversed.
