# V3 castle gameplay

Run `Assets/Game/Gameplay/Scenes/V3Gameplay.unity` with the configured Unity Services project and two players in the same Relay room. The gameplay scene loads `LevelBlockoutV3` additively. Start on Host once both players are connected. At the keep entrance the positive-X path is A and the negative-X path is B. The two paths follow the server's allocated player slots.

## Outbound

- In A1, B2, A3 and B4, stand at the hint stone and press E. The order appears on the stone for that player.
- Use the three sender controls beside the shared wall to extend one stone at a time on both faces. The receiver can read the numbered sockets from their room. Press E at Clear to reset the message.
- Each player uses E near their room's three pads in order. A wrong glyph resets that player's entry. After both finish, their exit leaves open and remain open.
- Beyond the fourth room, each player reads their number prop. Use E on the matching keypad, enter the four digits, and submit. Both players then walk through the opened basement threshold.

## Return

The server opens the return corridor after both crossing records are set. For this V3 slice the same event advances Foundation through SealPending to Return so downstream return mechanics can run. When the seal and underground jump-map module ships, it will own the global Return transition. The physical basement crossing remains the event that converts the castle layout.

Traps warn before attacking and recharge between attacks. The first hit slows movement and disables sprint. The second returns the player to the basement door. At the sewer entrance, enter the twelve outbound glyphs in order using E on the three pads. Clear erases a partial entry. A wrong full entry causes a hit and a retry delay. An opened sewer stays open and returns to the keep entrance beyond the trap rows.

## Scene and simulation ownership

- `CastleGameplay` is an in-scene NGO behaviour on the V3 GameState object. It binds the saved Fortress interior by part, A/B room identifiers and the `RemovablePartitions` group. Runtime-only objects supply doors, wall mechanisms, pads, traps and sewer. The generator and saved level scene are preserved.
- The server chooses codes, verifies interactions against sender slot, current stage, input revision and station distance, and tracks both actual door crossings. Replies containing the private hint or number go to the relevant owner. Progress and open states are replicated. The Host can inspect authoritative answers with F3 in Editor or a development build.
- Rejoining a live room restores the server's slot state. A disconnect in the castle resumes at the upper dock during outbound or near the basement door during return. The existing Relay room controls and owner-authoritative movement remain in use.

## Validation

Use **Tools > Twin Edge > Gameplay > Validate Saved Castle Gameplay** to check the saved scene and references. The Windows development build entry point is `TwinEdge.Gameplay.Editor.CastleGameplaySetup.Build`. Launch two instances with `-castleReplay host` and `-castleReplay client`, each with `-castleEvidence <absolute ignored directory>` pointing at the same fresh directory. `-castleScreenshots` enables frame captures. The replay uses real Relay and simulated keyboard input, with one initial placement outside the castle. To run the separate outbound death and reconnect scenario, add `-castleOutboundReconnect` to both processes and choose a fresh evidence directory.
