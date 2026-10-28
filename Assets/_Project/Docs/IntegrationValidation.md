# Integration validation — 2026-10-28

Unity 6000.3.27f1, NGO 2.13.3, Multiplayer Services 2.4.1. Package versions were retained.

## Executed successfully

- Unity batch-mode compilation, NGO RPC postprocessing, and Windows development player build.
- Actual MainPrototype scene, eight materials, and six reusable prefab assets generated and saved.
- Structural validation: one NetworkManager, one game/respawn manager, four gimmick controllers, no scene player, no nested NetworkObjects, required references, unique checkpoint names, correctly scaled end trigger, floor below every respawn marker.
- Two separate Windows player processes using direct Unity Transport: both `PROTOTYPE_SMOKE_PASS`.
- Two separate Windows player processes using real Unity Services authentication, Multiplayer Services sessions, Join Code, and Relay: both `PROTOTYPE_SMOKE_PASS`.
- Two spawned owned players and shared game readiness.
- Different bridge switches activated on different peers; shared bridge opened.
- Both players carried by the moving cooperative raft through its actual crossing; completion shared.
- Host-only information code; client answer field empty. Wrong UI submission kept door closed. Correct client UI submission opened the shared door collider on both peers.
- Client carried more than five meters on the moving jump platform. A/B hold handoff unlocked the route. One arrival did not complete the jump puzzle; second arrival completed it on both peers.
- TurnPoint changed both peers to Competition. Bridge cooperation path hidden; jump safe/risk, raft platform route, and door competition route enabled.
- Players registered different return checkpoints and respawned to their own positions after entering KillZone.
- Client memory shortcut opened its local collider while the host's shortcut collider stayed closed.
- First Goal arrival fixed WinnerClientId=0 and Finished on both peers; later arrival did not replace it.
- Client leave/disconnect cleared readiness. Host leave/new room reset game state, winner, all four completion flags, and raft pose. Relay rooms were left through the production session API.
- The opt-in test exits on a Unity Error/Exception/Assert log; both successful runs completed without such logs.

## Bugs corrected during integration

- Shared trigger retry throttles could starve a player's jump-end request. Owner filtering and server-side bounds checks now make arrival registration reliable.
- Scaled primitive roots made rider/end trigger volumes inherit incorrect dimensions. Moving roots and end trigger parents now use unit scale.
- Outbound checkpoints could overwrite return progress. Checkpoints have explicit phases and return entry markers; the turn point establishes a fresh race respawn pose.
- Memory input was on the outbound side. A dedicated return-side station now validates shortcut submissions.
- A closed/inactive shortcut could lose its authored door panel pose before Awake. Door pose capture is now lazy and idempotent.
- Client occupancy callbacks could call a server-only status method. Status RPC dispatch is now guarded by IsServer.
- Teleported riders could receive a stale large platform delta. Carrying validates overlap and ignores reset-sized displacement.
- Generated text guides had no font assigned. Labels now explicitly reference Unity's built-in runtime font.

## Scope and remaining human checks

Automated tests teleport between stations and invoke the existing UI button actions. They exercise networking and state transitions; they do not constitute a human traversal of every path or a mouse-raycast UI test. Raft movement and moving-platform carrying are exercised in real time.

The Relay run uses two processes on the same PC, connected through the real service. A second-PC test, manual WASD/jump/camera and mouse-button operation, competition PvP balance, and complete human traversal should still be performed. The 8–15 minute target has not been measured. Use the shared elapsed timer and the playtest steps in MainPrototypeGuide.md.

Local logs and the development executable are under the git-ignored `Builds/IntegrationSmoke` directory: `Host.log`, `Client.log`, `HostRelay.log`, `ClientRelay.log`, `PrototypeSmoke.exe`. Batch-generation and build logs are under `Logs`.
