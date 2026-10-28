# MainPrototype integration

Open `Assets/_Project/Scenes/MainPrototype.unity`. It is the enabled first scene in Build Settings.
`Prototype_01.unity` and the existing NetworkPlayer prefab are preserved.

## Scene structure

```text
NetworkManager                    # must be a root for NGO's DontDestroyOnLoad behavior
GameSystems
  GameManager                     # one NetworkObject: GameStateManager, RespawnManager, JumpPuzzleController
  NetworkDebugUI                  # MultiplayerSessionManager and generated development UI
Players
  SpawnPoints
    PlayerSpawnPoint_01
    PlayerSpawnPoint_02
Main Camera                       # owner binds CameraTarget on spawn
Level
  StartArea
  BridgeArea
  Transition_01
  RaftArea
  Transition_02
  DoorPuzzleArea
  Transition_03
  JumpPuzzleArea
  TurnPointArea
  CourseGuides
Checkpoints
  CP_Start / CP_Bridge / CP_Raft / CP_Door / CP_Jump
  CP_Bridge_Return / CP_Raft_Return / CP_Door_Return / CP_Jump_Return
KillZones
  KillZone
GoalArea
  Goal
  GoalMarker
```

Player objects are spawned by NGO, never placed in the scene. NetworkManager remains a root because NGO checks for parents before marking it DontDestroyOnLoad. Other systems are grouped under GameSystems. Keep the moving platform root at scale `(1,1,1)`; resize its Base child instead.

## Outbound cooperation

1. Host creates a Relay room; the second player joins using the code. Movement, interactions, and the start barrier wait for two spawned players. Debug UI shows Waiting / Game ready.
2. At the bridge entrance, each player presses E on a different orange switch. The blue bridge opens.
3. At the raft, each player occupies a different control point. Both W moves forward; W/S inputs steer. Before completion, an abandoned raft returns to its fixed spawn marker after four seconds.
4. At the door puzzle, one player uses InfoStation, the other InputStation. Communicate the private code and submit it. The shared door opens on both peers. Remember the code.
5. At the jump puzzle, A holds the near switch while B rides. B steps onto the far approach and holds B while A is still holding A. This unlocks the cooperative platforms. B then keeps the moving platform running while A boards. The unlocked stepping route stays available so both can leave their switches. Both must reach EndPlatform to mark completion.
6. Both players enter TurnPoint. Competition starts only when all four completion flags are true.

## Return competition

Return through the same sections toward Goal:

| Section | Safe route | Faster route |
| --- | --- | --- |
| Jump | Broad green detour | Narrow red jumping platforms |
| Door | Green route through the already open shared door | Enter the remembered code at the far-side red ShortcutInputStation; unlock is personal |
| Raft | Green platform detour | Raft; W drives toward the start, at higher competition speed |
| Bridge | Green detour | Red jump route; the blue outbound bridge is removed |

The original door remains open. The straight cooperation hallway is disabled during competition so that the memory shortcut offers a real route choice. Cooperation-only stations and the jump ferry surface are hidden during return. First Goal arrival wins on the server; later arrivals cannot change WinnerClientId. Finished blocks new gameplay inputs.

## Checkpoints and recovery

Five outbound checkpoints sit before the major sections. Four return checkpoints sit at the corresponding far-side entrances. Outbound triggers do not overwrite return progress. Competition first replaces each player's respawn pose with the turn-point return pose, then each return checkpoint independently updates that player's pose. Checkpoint IDs/names must be unique.

The common KillZone covers the course. Server-side recovery below y=-25 also prevents infinite falling outside it. Respawn clears external velocity/grabs and teleports the owning NetworkTransform. Respawning does not reset a solved gimmick. Starting a new session resets all gimmicks, winner, timers, occupancy, and platform poses.

## Inspector defaults

- GameStateManager: Require All Gimmicks enabled; Bridge/Raft/Door/Jump references assigned.
- RespawnManager: Competition Start = TurnPointArea/CompetitionRespawnPoint; Fall Recovery Height = -25.
- Raft: Move Speed 2.2; Competition Speed Multiplier 2.5; Reverse Drive In Competition enabled. End at z=83.
- MovingPlatform: server NetworkTransform; Move Speed 3; Wait Time 0.75; fixed A/B marker references. RiderTrigger reuses RaftMovingPlatform.
- HoldSwitch: A id=0, B id=1; trigger colliders; server validates distance and different occupants.
- Door: Cooperation Floor, Competition Route, and dedicated Shortcut Station references assigned. Shortcut station is Competition Only.
- Checkpoint: Phase Cooperation or Competition; trigger center one meter above its respawn marker.
- NetworkManager: UnityTransport, existing NetworkPlayer prefab, connection approval by NetworkSpawnCoordinator. Do not add a second scene player or manager.

Greybox materials are in `Assets/_Project/Materials/Greybox`: gray ground, cyan cooperation paths, orange interaction, blue checkpoints, yellow goals, green safe routes, red risk routes, dark blue decorative water. Water has no collider.

Reusable templates are in `Assets/_Project/Prefabs/Greybox`: Checkpoint, KillZone, SpawnPoint, BridgeSwitch, HoldSwitch, MovingPlatform. Scene references must be assigned on an instance. Checkpoints, spawn markers, and switches in MainPrototype are prefab instances.

## Safe builder commands

- `Tools > Prototype > Create New Main Prototype Scene`: creates a new integrated scene with a unique path. It never overwrites an existing scene or existing NetworkPlayer prefab. Enables the new scene as the first build scene.
- `Tools > Prototype > Add Missing Integration Objects`: fills missing integration markers/checkpoints and references in an existing four-gimmick scene; keeps existing geometry. Review and save the scene afterward. It does not redesign a manually edited route.
- `Tools > Prototype > Validate Integrated Scene`: checks manager count, scene players, nested NetworkObjects, required references, trigger scales, duplicate checkpoint names, and checkpoint floors.
- `Tools > Prototype > Create New Prototype Scene`: legacy template creation now also uses a unique path.

## Changed files

- New: MainPrototype scene, MainPrototypeSceneBuilder.cs, PrototypeLevelFlow.cs, IntegratedCourseSmoke.cs, this guide and IntegrationValidation.md, Greybox materials/prefabs and their metadata.
- Game: GameStateManager.cs, Checkpoint.cs, RespawnManager.cs, TurnPoint.cs, Goal.cs.
- Bridge: BridgeController.cs, BridgeSwitch.cs.
- Raft: RaftController.cs, RaftMovingPlatform.cs.
- Door: DoorPuzzleController.cs, DoorPuzzleStation.cs, PuzzleDoor.cs.
- Jump: JumpPuzzleController.cs, HoldSwitch.cs, MovingPlatform.cs, JumpPuzzleEndTrigger.cs.
- Player/input/debug: PlayerMovement.cs, PlayerInteraction.cs, PlayerCombat.cs, NetworkDebugUI.cs.
- Editor/assets: PrototypeSceneBuilder.cs, DefaultNetworkPrefabs.asset, EditorBuildSettings.asset.
- Retained: Prototype_01 scene, existing NetworkPlayer prefab, package manifest/lock, Relay session API implementation.

## Host/client playtest

1. Build MainPrototype in windowed mode. Run Editor + build, or two builds. Each process uses its own anonymous authentication profile.
2. Create Room, copy Join Code, Join Room. Check Players=2/2, Game ready, two independently controlled players/cameras.
3. Complete the four outbound sections in order. At each section compare completion flags and visible geometry on both screens.
4. Make one player arrive at Jump End, then TurnPoint early. Neither completion nor competition should occur until the required second arrival.
5. Return by different routes. Check the client sees shared door opening; only the successful memory-code player gets its shortcut door open.
6. Before and during return, register different checkpoints and fall on each peer. Each must return to its own latest phase-appropriate checkpoint.
7. Try F/G/H and mouse hit on the return. Check cooldown, displacement, and grab release after falling.
8. Reach Goal in order. Compare WinnerClientId and Finished on both peers. Confirm the second arrival cannot overwrite the result.
9. Leave, create another room, and reconnect. Bridge/door/jump must reset, raft/ferry must be at initial poses, and no old occupancy or winner may remain.
10. Repeat on two PCs using Relay. Authentication/Relay require the existing Unity project/service configuration and internet access.

The 8–15 minute target needs first-time human playtests; it is not a measured completion time. Debug UI displays server-based elapsed time. Record cooperation, return, failure count, and route choice. Expand playable puzzle space or adjust timing after measuring, rather than adding long flat transitions.

## Automated checks

`MainPrototypeSceneBuilder.CreateForBatch` creates and structurally validates the scene in Unity batch mode. `MainPrototypeSceneBuilder.BuildSmokePlayer` builds a development player under Builds/IntegrationSmoke. Opt-in `-prototypeSmoke host|client -prototypeSmokeCode <shared-test-path>` runs two-process direct-transport checks of state replication, the raft crossing, moving-platform carrying, UI submission, personal respawn, memory shortcut privacy, winner, disconnect, and session reset. The test teleports between stations; it does not measure route difficulty or Relay service connectivity. The test driver is excluded from release builds and does nothing without the explicit command-line flag.

Add `-prototypeSmokeRelay` to both processes to use the production MultiplayerSessionManager's real Create Room / Join Code / Leave flow instead of direct transport. The join-code fixture and the puzzle-code fixture are separate. The host test also leaves the completed room, creates a new Relay session, and checks fresh gameplay state. See `IntegrationValidation.md` for the checks actually executed.

## Common integration failures

- Scene mismatch: rebuild both players from the same MainPrototype scene; NGO in-scene object hashes and NetworkBehaviour order must match.
- Host can proceed alone: confirm GameStateManager is spawned and the local owner uses its replicated CanPlay flag.
- Trigger too large or below the surface: keep moving roots and trigger parents at unit scale; resize mesh children.
- Respawn skips a return section: check checkpoint Phase and CompetitionStart. Never make an outbound checkpoint Both for this course.
- Ferry or raft moves but player slides: verify the RiderTrigger/PlayerStandArea trigger, carrying transform reference, and the owning CharacterController.
- Door shortcut cannot be entered on return: use ShortcutInputStation on the far side, not the cooperation InputStation.
- Only one screen changes: inspect the replicated state, required controller references, and build mismatch before adding another RPC.
- A new room starts with old progress: inspect OnNetworkSpawn reset and make sure the in-scene NetworkObjects were not duplicated.
