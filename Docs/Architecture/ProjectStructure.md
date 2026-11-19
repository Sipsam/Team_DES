# Development architecture

The latest integrated design in `Docs/GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf` takes priority. V3Gameplay and LevelBlockoutV3 are the current gameplay entry point and additive level. The V2 spatial documents record the earlier blockout.

| Path | Ownership / purpose |
| --- | --- |
| Assets/Game/Level/Runtime | Active LevelLayoutConfig ScriptableObject, section/marker/route components and validation |
| Assets/Game/Level/Editor | Generator, terrain/structure builders, V2 spatial construction/review, visualization, regression and dependency audit tools |
| Assets/Game/Level/Data | V1–V3 configuration assets and persistent generated dependencies |
| Assets/Game/Level/Scenes | V3 level, retained V2 blockout and V1 comparison scenes |
| Assets/_Project | Historical multiplayer prototype and compatibility runtime; historical tools/docs/prefabs/materials retained |
| Assets/Settings | Shared URP pipeline/renderers and volume configuration |
| Assets/InputSystem_Actions.inputactions | Shared input asset, also referenced by Build Settings configuration |
| Assets/DefaultNetworkPrefabs.asset | Shared NGO registry referencing legacy NetworkPlayer and Greybox/MovingPlatform |
| Packages / ProjectSettings | Shared package versions, Unity version, render/input/build configuration |
| Docs/GameDesign, Docs/Architecture, Docs/Archive | Shared planning, required architecture contracts and prototype recovery instructions |
| Docs/Development/Feature/VN (or Feature/Shared) | Ignored local-only designs, plans, reviews, audits, validation and development notes; never stage |
| Tools/ProjectAudit | External Python filesystem/GUID reporting, outside Unity compilation |

No empty Core/Player/Network/Gameplay placeholders were created. Introduce these under `Assets/Game` as current-design implementations actually arrive, with deliberate migration of reviewed reusable components. No geometry, generation behavior, GUIDs, working scenes or production settings were changed for cosmetic consistency.

## Compilation and references

There are currently no project-owned assembly definitions or assembly references. Runtime scripts compile into Assembly-CSharp; Editor directory scripts compile into Assembly-CSharp-Editor. Level runtime code uses UnityEngine and its own types; Editor code uses UnityEditor and level types. The level sources do not reference prototype runtime types; saved scene dependencies and the global NGO registry must also be considered before excluding legacy code. Package assemblies remain managed by their packages. Runtime validation intentionally does not depend on UnityEditor.

Future isolation requires a reviewed runtime/editor asmdef boundary, explicit package references, checking serialized class identities (including managed references), retained prototype scene/smoke behavior, and compile/build verification. An archive folder name does not disable compilation. This task uses the verified historical tag and preserves the compatibility runtime instead of changing assembly identities.

## Level ownership

Each owned `TwinEdge_Blockout` has five LevelSection objects, stable marker IDs, route metadata and a layout config. Regeneration replaces only Generated within the selected scope. Manual holds authored additions and must survive regeneration. Undo retains references to earlier asset generations. V2 construction, per-section generation, Manual/Generated split, Undo implementation, visualization and validation code were retained unchanged.

`LevelBlockoutV2.unity` is the canonical V2 review scene; the initial draft and two V1 scenes are references with their own required dependencies. Scene ownership is explicit even when drafts share assets. Avoid loading overlapping blockouts additively for traversal tests. Shared Build Settings still launch the historical prototype; the explicit V3 Gameplay build selects the current gameplay and additive level scenes.
