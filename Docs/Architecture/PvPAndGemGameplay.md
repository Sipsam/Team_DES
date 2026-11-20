# V3 PvP, gem contest and victory

Open `Assets/Game/Gameplay/Scenes/V3Gameplay.unity` or build it with `TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer`. Create a Relay room on Host, join from a separate process, then start when both players are connected. The boot scene loads the saved level additively. Player tuning lives on `GameplayCharacter` in the V3 network player prefab. Gem tuning lives on `GemOwnership`. The finish volume uses the existing `Mountain.V3.Start` marker and is built at runtime.

## Controls and stamina

- Left click swings in the direction the character faces. The first click used to capture the cursor is reserved for capture. A swing starts the cooldown even if it misses.
- F grabs a nearby opponent. Press F again to throw. The throw gets stronger while the opponent is held, up to the charge limit. Tap F repeatedly while held to escape.
- Space jumps. Shift spends stamina while running. Shift with W/S spends the same player's stamina on strong rowing. An exhausted rower can still row normally. Zero stamina prevents stamina-consuming actions and causes a carrier to put down the opponent.
- The planning baseline specifies no HP, approximately 0.1 seconds of hit stun, a 1-second swing cooldown, a 3-second grab cooldown, and a 1–2-second gem pickup protection window. The active pickup protection is 1.5 seconds and covers gem loss from strikes.
- Starting balance values are 100 stamina, 3 per jump, 2/s sprint, 23/s strong rowing, 22/s holding, 16/s recovery and a 5-second empty delay. The 2.65 m swing, 1.85 m grab, 60° facing half-angle, seven resistance presses, 55% carry speed, two-second throw charge and the impulse fields are tuning values.

## Multiplayer rules

- The server validates owner identity, the active life, phase, distance, facing, sightline, action locks and cooldowns. The server sends the approved movement effect to the victim owner. The existing owner transform replicates the result and the CharacterController resolves timed impulse movement against colliders. The server evaluates combat and the finish against its received owner transform snapshots. The Foundation movement protocol assumes an honest owner transform.
- The owner starts a jump from replicated stamina and local ground contact. The server validates the life and movement locks and spends stamina. A denied prediction stops the ascent. Sprint uses a short server lease. The raft retains its original server hull simulation and reads the same stamina pool.
- A successful strike or grab cancels involved pole grips, resets bridge load at the supply, closes the affected keypad, or interrupts seal participation. Solved puzzle progress remains available. A seated raft rider or a player in the mountain launch sequence keeps that movement controller until it releases them. Trap injury and slow timers keep their own lifetime. While PvP owns the capsule, it takes precedence over underground trap impulses. Death or disconnect clears any carry relationship. All these rules apply in both live phases where the corresponding gimmick exists.
- The first valid request on the server changes the contest state. New-life IDs invalidate queued PvP, stamina and gem requests from before a respawn. A same-frame death of the player who just dropped the gem overrides the hit drop. A dropped gem becomes eligible for acquisition on the following server frame. A successful acquisition changes the gem revision and rejects the other request for that revision.

## Gem and finish

- Complete the joint seal, then use E near the gem to start Return. Only one player can own it. The carrier can strike and can be grabbed. Carrying the gem prevents grabbing opponents. Strike the carrier after the protection window, then use E within reach to steal the dropped gem.
- A hit drop searches for clear, walkable ground below the hit. Unsafe or disappearing surfaces use the last safe carrier drop point and a nearby course checkpoint fallback. Disconnect uses the regional relocation rule, preserving Return for a replacement peer.
- On carrier death the underground jump map sends the gem to the cavern entry, the castle to the upper dock, the river to the lower dock, and the bridge or small peaks to Big summit. The final-start approach exception uses the authored canyon connection to the starting summit, including Peak_04 and Start in the current level. Death there keeps the gem on a safe surface in the final section when possible. Player checkpoint rules are independent of gem movement and Return continues after death.
- The four finish markers enclose a 4.5 m square on the starting summit. The server checks the volume, ground support, active movement state and gem ownership on every frame of a 0.35-second dwell. A strike, drop, respawn or departure during that window resets eligibility. The first confirmed eligible carrier stops the match timer and publishes the winner ID and Finished phase.

## Reproducing checks

Use a Windows Development build with the configured Unity Services project. Launch two processes with `-v3Smoke host` and `-v3Smoke client`, each with `-v3Contest -v3Evidence <fresh absolute directory>`. The fixture uses station placement and a test-unlocked gem. Its combat and pickup inputs use Input System and real Relay. Add `-v3ContestRegions` to verify each staged death region or use `-v3ContestLifecycle` for stale RPC, carry respawn, rejoin and concurrent pickup checks. For the actual seal, use the castle and underground replay. Full route and human balance checks are separate. See [V3 Foundation](V3Gameplay.md), [Castle gameplay](CastleGameplay.md) and [Underground gameplay](UndergroundGameplay.md).
