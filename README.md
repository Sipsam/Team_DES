# Team_DES — 양날의검

Two-player online 3D puzzle game for Windows PC. Unity **6000.3.27f1**, URP **17.3.0**. Active gameplay development uses V3Gameplay and the additive LevelBlockoutV3. The historical multiplayer prototype is retained for reference.

## Clone and open

```powershell
git clone https://github.com/Sipsam/Team_DES.git
cd Team_DES
```

Install Unity 6000.3.27f1 through Unity Hub with Windows Build Support (IL2CPP if needed). In Hub, add this repository root (the folder containing Assets, Packages, ProjectSettings) and open it. Allow package restore and initial import to finish. Commit both package manifest and lock file; Library is regenerated locally. Relay gameplay also requires access to the team's Unity Services project; cloning does not grant service access.

## Play V3 over Relay

1. In Unity, select **Tools > Twin Edge > Gameplay > Build V3 Windows Playtest Player**. Package the `Builds/V3Playtest/` runtime files together and leave the `*_DoNotShip` debug folder on the build machine. Each tester launches `V3Gameplay.exe` from their own extracted copy.
2. Host selects **Create room**, then shares the room code. Client pastes it and selects **Join room**. Once the Host sees **Players 2/2**, select **Start game**.
3. Click the world to capture the mouse. Use WASD to move, Left Shift to sprint, Space to jump and E to interact. Press Esc to release the mouse or cancel the active interaction. Both players work toward the altar, then race back to the starting summit with the gem.
4. Press Esc and select **Leave room** to disconnect. A disconnected Client can enter the Host's code again while the room is active. After Host leaves, create a new room and share its new code.

The playtest build starts in `V3Gameplay.unity` and loads `LevelBlockoutV3.unity` additively. The general Build Settings still start with `MainPrototype.unity`. For Editor play, open `Assets/Game/Gameplay/Scenes/V3Gameplay.unity` first. Use **Build Full Match Development Player** for the existing Relay replay at `Builds/FullMatch/V3Gameplay.exe`. The full control reference, route and two-person test sheets are in the [V3 playtest guide](Docs/Architecture/PlaytestGuide.md). See [full match gameplay](Docs/Architecture/FullMatchGameplay.md) for replay commands.

## Level review and historical tools

Open `Assets/Game/Level/Scenes/LevelBlockoutV3.unity` alone for a spatial review. `LevelBlockoutV2.unity` is the retained V2 review scene. `LevelBlockout.unity` is V1. `LevelBlockout 1.unity` and `LevelBlockoutV2_InitialDraft.unity` are comparison scenes. Open one blockout at a time when checking traversal.

- `Tools > Twin Edge > Level Blockout`: select/create the V2 spatial layout; generate an entire blockout or one section. Work on a new scene or reviewed copy; generation replaces the selected Generated hierarchy. Put authored edits under Manual. Existing Undo is supported.
- `Tools > Twin Edge > Level Visualization`: select a root/section and Top, Side X, Side Z, or Perspective; inspect routes, markers and Elevation Profile without editing geometry.
- `Tools > Twin Edge > V2 > Review selected`: frame the selected geometry. V2 markers reserve gameplay space; they do not implement the latest gameplay.
- `Tools > Twin Edge > Audit project dependencies (read only)`: refresh local AssetDatabase dependency and preview-scene reports under `Docs/Development/Architecture/V1/Audit`.
- `Tools > Twin Edge > Verify Level Visualization (read only)`: run existing camera/profile checks for V1/V2 without regenerating geometry. Local output: `Docs/Development/Level/Shared/Visualization-Validation.txt`.

## Structure and tracking

See [architecture](Docs/Architecture/ProjectStructure.md), [prototype recovery](Docs/Archive/Prototype_v1.md), and [generated asset policy](Docs/Architecture/GeneratedAssetAudit.md).

Internal designs, plans, audits, debugging notes and test results belong in `Docs/Development/<Feature>/V<N>/`. Features version independently; start a version for a major phase and update documents within that phase. Preserve earlier versions. Cross-version tools use `<Feature>/Shared/`. Optional local evidence (absent from fresh clones): `Docs/Development/Level/V1/`, `Level/V2/`, `Level/V3/`, `Level/Shared/`, and `Docs/Development/Architecture/V1/`. Root `AGENTS.md` defines the Codex workflow.

Commit Assets with `.meta` files, Packages, ProjectSettings, design documents and required generated assets. Library, Temp, obj, Logs, UserSettings, Builds/Build, IDE caches, generated C# project files, root tree/list inspection outputs and crash data are ignored. **Generated/Run_* is not ignored**: every saved scene (including drafts) needs its referenced meshes, terrain and materials. Preserve old generation assets used by Undo until they have been explicitly reviewed.

## Team workflow

```powershell
git switch -c feat/short-description
# Edit, validate in Unity, and inspect the exact files before staging.
git status --short
git diff
git add -- <reviewed-paths>
git diff --cached --stat
git diff --cached --check
git commit -m "feat(level): describe the change"
```

Use feature branches and Pull Requests. Write short English Conventional Commit messages (`feat`, `fix`, `chore`, `docs`). Include Unity version, changed scene/config, generated dependencies, checks actually run and remaining playtests in the PR. Coordinate ownership before editing the same scene; preserve GUIDs during moves through Unity's Project window/AssetDatabase. Do not stage local scene deletions or shared settings without reviewing their purpose. Never run old prototype scene builders on an active level scene.
