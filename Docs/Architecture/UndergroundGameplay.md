# Underground gameplay

V3 uses the existing two-player Relay session and the `FoundationPhase` state machine. The level scene remains the source of the stair route, cavern shell, lava elevation, fixed shelves, and altar terrace. `V3Gameplay.unity` holds the two underground network components and their visual references. On level load, `UndergroundGameplay` binds a gameplay wrapper and hides the generated switch, timed-deck, and gem reservations at runtime.

| Event | Server result |
| --- | --- |
| Both players cross the open castle basement door | The completed castle puzzle stays open and the removable partitions are disabled. The phase stays `Outbound`. |
| The reciprocal crossings are complete and both players reach the altar | The phase becomes `SealPending`. The game is still in `Cooperation`. |
| Both players press E at their seal stations and stay in range during the channel | The seal opens and the altar gem becomes available. |
| The server accepts the first E pickup | The gem gets one carrier, the phase becomes `Return`, and all eight groups of main-route stones are permanently enabled. |
| A return player falls into the lava | The player respawns at the altar. A carried gem moves independently to the lava entrance. |

The switch order is A0, B1, A1, B2, A2, B3, A3, then the B-side altar switch. Each switch extends a timed group for the opposite lane. The B-side altar switch stays reachable while A attempts the last crossing. The 111 stones are split into groups of 16, 14, 15, 15, 15, 15, 10 and 11. The Inspector setting defaults to 25 seconds and accepts values from 20 to 30 seconds. On outbound lava death the server resets the reciprocal sequence and returns cavern participants to the common start. A seal already opened stays open for the session.

The server assigns switch inputs to the sender's assigned lane and validates progression, proximity, and the previous group deadline. The replicated deadlines and a shared server clock control both the stone model and its support collider. Timed flame, arrow, spike, moving, and retracting hazards run during traversal in both directions. Castle corridor hazards begin on Return. The development Return control is compiled for Editor and Development players.

`GemOwnership` replicates state and carrier, and the gem model follows that carrier on each peer. The central respawn hook resolves the gem's relocation separately from player checkpoints. The current destinations are the cave start, upper dock, lower dock, and big summit, depending on the death region. See [PvP, gem contest and victory](PvPAndGemGameplay.md) for drop protection, theft and finish eligibility.

Use **Tools > Twin Edge > Gameplay > Validate Saved Underground Gameplay** to check stored prefab references and level colliders. The Windows build entry point is `TwinEdge.Gameplay.Editor.UndergroundGameplaySetup.Build`. The two-peer replay accepts `-castleReplay host` and `-castleReplay client`, each with `-castleEvidence <absolute ignored output directory>`. Run with `-castleScreenshots` on a graphical peer for captured frames.

For an isolated cavern replay, add `-castleCavernProbe`. Add `-castleCavernSeconds 20` or `-castleCavernSeconds 30` to override the Host timer for that development replay, or `-castleCavernReconnect` to include disconnect and rejoin. The isolated run starts with placement at the cave entrance. The standard replay traverses the castle and stairs using normal movement.
