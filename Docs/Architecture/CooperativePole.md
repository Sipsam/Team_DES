# V3 outbound cooperative pole

Open `Assets/Game/Gameplay/Scenes/V3Gameplay.unity`. Create a Relay room, join from a second process, then start the game on Host. The pole wrapper belongs to the Gameplay bootstrap, outside every level `Generated` hierarchy. Start placement uses `Mountain.V3.Start`; permitted summit IDs and landing areas use `MountainConfigV3`. Do not run a level generator to install this feature.

Approach either end and press E. The first participant becomes pivot; the second receives the remaining grip. Both participants are required for rotation and the private transparent support under the **orbital** participant, as specified by the integrated planning document.

- Orbit: A/D rotates horizontally; W/S adjusts elevation within the configured pitch limit.
- Pivot: WASD carries both endpoints at 2 m/s while both have real support on the same summit. Once the orbit leaves the summit the pivot remains fixed.
- Stop on a different, safe summit. After the landing dwell, the orbital player presses E to exchange roles.
- Esc releases the pair; F4 toggles pole diagnostics. F3 remains the foundation diagnostics key.

Controls, spherical yaw/pitch movement, support dimensions, landing thresholds, carry speed and pole dimensions are implementation hypotheses, not finalized planning values. The current 32 m pole cannot bridge the existing Start-to-small-peak spacing. Do not treat the current scene as a completed mountain route or enlarge the pole/rebuild terrain automatically. Route scale must be reviewed before end-to-end acceptance.

The server owns an atomic pole snapshot and validates sender, phase, revision, grip range, real terrain support and collision sweeps. Attached characters suspend normal movement and OwnerNetworkTransform updates; the pole snapshot drives both endpoint poses. On release the owner restores its normal transform stream. Ordinary unattached characters retain their existing controller and ownership. Cancelling or disconnecting releases both grips. Collision failures respawn the failed participant through existing per-player checkpoints; the partner is released. The planning document does not mandate shared mountain respawn, so that behavior remains a documented hypothesis. No intermediate checkpoint is added.

`Tools > Twin Edge > Gameplay > Validate Cooperative Pole` loads saved assets and performs geometry and real collider queries without regenerating terrain. `Install Cooperative Pole` creates missing wrapper assets and installs only Gameplay references; it prompts to save modified scenes in interactive Editor use. Build with `TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer`.

For opt-in development automation, launch two builds with `-poleSmoke host` / `-poleSmoke client` and `-poleEvidence <fresh absolute directory under ignored Docs/Development/Gameplay/V1/>`. This uses real Relay with station positioning for setup. It does not prove crossing a gap or manual control feel. Optional local evidence and implementation notes live under ignored `Docs/Development/Gameplay/V1/`; fresh clones do not need them.
