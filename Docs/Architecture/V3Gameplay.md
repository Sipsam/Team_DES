# V3 Gameplay foundation

Open `Assets/Game/Gameplay/Scenes/V3Gameplay.unity` in Unity 6000.3.27f1 and enter Play. The bootstrap loads the existing V3 level additively before enabling room controls. Do not add gameplay objects under the level generator Generated trees.

Create room on one instance, copy the Relay room code into Join room on the other instance, and use Start game on Host when Players shows 2/2. The Unity project must remain linked to its configured Unity Services project with Authentication/Relay access. Use an Editor and a Windows development build, or two separate builds. Each process uses a separate anonymous Authentication profile.

WASD moves, Shift runs, Space jumps, E interacts with the cube near the start. The cube tests network interaction and carry animation. Esc cancels an active interaction and releases the cursor; click outside the menu to capture it. F3 toggles debug details. Leave room returns to offline controls. No local direct-host fallback is substituted for Relay room codes.

The scene reuses the original NGO player as a prefab variant, existing session/spawn/movement/interaction/state/respawn systems, and one owner-bound camera per process. New foundation state is an opt-in mode on GameStateManager. The original prototype scene remains the default build scene; select V3Gameplay explicitly or build with Tools/Twin Edge/Gameplay editor code. The builder appends both required scenes to Build Settings.

Only foundation systems are included. Terrain gaps still need the planned pole/raft/puzzle modules. Return checkpoint routing, actual lava hazards, shared raft destruction, gem and winner logic are not playable features in this foundation. Ordinary checkpoints are per-player; future cooperative raft modules must use the explicit shared checkpoint/respawn hooks.

To validate saved assets: Tools > Twin Edge > Gameplay > Validate Saved Foundation (saves no level changes; opens gameplay scene). The create command refuses to overwrite existing assets. `TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer` builds a development player to `Builds/V3Gameplay/V3Gameplay.exe`. Development-only automation can be launched with `-v3Smoke host` or `-v3Smoke client`, plus `-v3Evidence <absolute ignored output directory>`; start both processes with the same fresh evidence directory. This uses real Relay and teleports between test stations; it does not prove complete route traversal.

Optional local implementation/validation evidence lives under ignored `Docs/Development/Gameplay/V1/`. It is not required on a fresh clone.
