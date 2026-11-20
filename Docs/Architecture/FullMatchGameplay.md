# V3 full match

For a regular Windows player and two-person test sheets, use the [V3 playtest guide](PlaytestGuide.md).

The current boot scene is `Assets/Game/Gameplay/Scenes/V3Gameplay.unity`. It loads the saved `LevelBlockoutV3.unity` additively. Use Unity 6000.3.27f1 and the linked Unity Services project with Authentication and Relay access. Build with **Tools > Twin Edge > Gameplay > Build Full Match Development Player** or run `TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildFullMatchPlayer` in batch mode. The output is `Builds/FullMatch/V3Gameplay.exe`.

Host creates the room, Client enters its join code, and Host starts when both players are present. The server moves from `WaitingForPlayers` to `Preparing` and then `Outbound`. If a client disconnects, active play waits for two players again. Host termination ends the session.

| Leg | Intended play route |
| --- | --- |
| Outbound mountain and bridge | Press E at the pole ends. Control the pivot and orbit with movement inputs. Exchange roles with E once the orbit player lands. At Big summit, release the pole with Esc, walk up the timber ramp and press E at the crate. Both players carry and approve 13 deliveries before crossing the installed bridge. |
| Outbound river and castle | Follow the canyon down to the lower dock. Press E to board the linked raft and coordinate W/S strokes, with Shift for stronger strokes. Land at the upper dock and take the approach to the castle. Exchange wall glyphs, solve all four A/B stages, and enter both private numbers at the basement door. |
| Underground | Follow the stairs, take turns activating each other's eight timed platform groups and reach the altar together. The server enters `SealPending`. Both players press E at the seal stations. The first accepted E pickup of the gem enters `Return`. |
| Return | Reverse the now-permanent cavern route and use the trap corridor or twelve-glyph sewer. Board one solo raft each at the upper dock. Ascend the canyon from the lower dock, jump the gaps on the return bridge and charge R to launch between mountain summits. |
| Finish | The gem carrier must enter the marked volume on the starting summit, stay grounded and meet server eligibility through the dwell. The server publishes the winner and stops the match timer in `Finished`. River cleanup preserves both players' positions and checkpoints. |

Gameplay and checkpoint rules are detailed in [V3 foundation](V3Gameplay.md), [cooperative pole](CooperativePole.md), [river](RiverGameplay.md), [castle](CastleGameplay.md), [underground](UndergroundGameplay.md) and [PvP and gem contest](PvPAndGemGameplay.md).

## Development replay

Launch the same Windows Development build in two processes with `-fullMatchRelay host` and `-fullMatchRelay client`. Give both peers the same fresh absolute directory via `-fullMatchEvidence` under the ignored development tree. Pass `-mountainPolePlan <absolute ContinuousPlan.json>` and `-returnPlan <absolute Route.json>` from collider analysis of the saved scene. Run `TwinEdge.Levels.Editor.MountainPoleTuning.VerifyCurrent` with graphics enabled for `Docs/Development/Gameplay/V1/MountainPole/ContinuousPlan.json`. Run `TwinEdge.Gameplay.Editor.MountainReturnValidation.Run` for `Docs/Development/Gameplay/V1/MountainReturn/Route.json`. Both methods inspect saved assets. For D3D11 frame evidence use `-force-d3d11 -fullMatchScreenshots`.

The replay drives the ordinary spawn, keyboard and stick actions, production keypad and launch RPCs, and existing CharacterControllers. The return bridge driver consults the Host's hazard assignment as a route aid. Join codes and step barriers are exchanged through the evidence directory. A pass requires both `*-full-match-pass.txt` files and matching winner and stopped timer values. The optional `-fullMatchLink canyon|approach|summit|castle|river|middle|finish` runs an independent fixture after one initial placement and writes only isolated link results. The finish fixture supplies a test gem after normal bridge construction and uses the actual E pickup, return route and finish volume. Existing isolated smoke modes exercise targeted failures and reconnects. Add `-v3ImpairedRelay` to both Foundation smoke peers to apply a separate Unity Transport delay, jitter and loss scenario.

A two-process Windows Development replay completed the full route from normal spawn in one Relay session. The server accepted the gem carrier at the starting summit, and both peers reported the same winner, `Finished` state and stopped timer. The isolated finish fixture also passed. Manual control feel, camera framing, full-course network impairment and performance profiling on separate machines remain open. Optional local evidence lives under `Docs/Development/Gameplay/V1/FullMatchEvidence/`.
