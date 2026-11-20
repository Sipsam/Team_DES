# V3 Windows playtest

## Install and connect

Each tester needs a Windows PC, keyboard, mouse and an internet connection. Use the same build on both PCs. On a linked Team_DES Unity project, open Unity **6000.3.27f1** and select **Tools > Twin Edge > Gameplay > Build V3 Windows Playtest Player**. Package the `Builds/V3Playtest/` runtime files together and leave the `*_DoNotShip` debug folder on the build machine. Extract the package to a writable folder and launch `V3Gameplay.exe`. The player starts in V3 and connects to the configured Unity Authentication and Relay project.

Use this command to reproduce the windowed Direct3D 11 setup used for the Windows desktop check.

```powershell
.\V3Gameplay.exe -force-d3d11 -screen-fullscreen 0 -screen-width 960 -screen-height 680
```

1. Host selects **Create room**, selects **Copy** beside the code, then sends it to Client.
2. Client pastes the code and selects **Join room**. When Host sees **Players 2/2**, Host selects **Start game**.
3. Click the world to capture the mouse. Press Esc to open the room menu and release the pointer. The first world click after release captures the mouse. The following click can attack.
4. Select **Leave room** to disconnect or leave after the result. Client can try the same code while Host keeps the session alive. Play waits for a second player. Check the restored position and puzzle state after rejoining. After Host leaves, both players return to the room menu and use a new code.
5. Select **Quit game** from the disconnected menu. If a connection fails, record the on-screen message, whether Host is still connected, and which build each tester used.

## Controls

| Input | Use |
| --- | --- |
| Mouse | Orbit the local character after capture. Direction also aims mountain launches. |
| WASD | Move. While on the pole, pivot moves the pair on the same summit and orbit uses A/D to turn and W/S to raise or lower. |
| Left Shift | Sprint using stamina. Hold with W/S on either raft for a stronger stroke. |
| Space | Jump. |
| E | Use the displayed target. Grab or swap pole roles, collect and install timber, board or leave a docked raft, operate glyphs and switches, use the keypad, channel the seal or pick up the available gem. |
| Left click | Attack in the direction the character faces. The same button operates UI when the mouse is released. |
| F | Grab a nearby opponent, press again to throw, or tap repeatedly to resist when held. Gem carriers cannot grab. |
| R | On a supported return summit, hold to charge and release to launch. W/S adjusts the angle while charging. |
| Esc | Release the mouse. Cancel a pole grip, timber carry, docked raft seat, launch charge, keypad or your seal participation when applicable. |
| Digits and Submit | After opening your castle keypad with E, click its field, type the four digits shown at your number station and select Submit. Press Esc to close it. |

## Route

| Section | Player objective |
| --- | --- |
| Outbound mountain | Approach opposite pole ends and press E. Move together on the summit, swing the orbit player onto the next summit and exchange roles once stable. At Big summit, release the pole and follow the ramp. |
| Outbound bridge and canyon | Use E at the crate, take one timber grip each, carry to the socket and both approve with E. Install all 13 deliveries, cross, then follow the canyon down to the lower dock. |
| Outbound river | Board the linked raft with E. Both players row with W/S and coordinate their strokes. Use the stamina display to pace stronger strokes. Disembark at the upper dock and walk to the castle. |
| Outbound castle | Follow your assigned A or B lane. Read the glyph clue and send it via the shared wall. Enter the three glyphs in order in each stage. Use your four-digit number at your keypad and both cross the basement door. |
| Underground altar | Activate each timed switch for the opposite lane. Cross before the 25-second timer expires. At the altar, both press E at their seal stations and remain nearby until the seal opens. Use E to acquire the gem and start Return. |
| Return underground and castle | Use the permanent stones, then navigate the trap corridor or enter the twelve outbound glyphs in order at the sewer shortcut. Return to the upper dock. |
| Return river and canyon | Board a solo raft each. W/S rows and A/D steers. After the lower dock, climb the canyon path. |
| Return bridge and mountain | Jump gaps and unstable bridge planks. Charge and aim successive R launches from safe summits. An opponent can strike the gem carrier after pickup protection expires, then acquire the drop with E. |
| Finish | Bring the gem inside the four markers on the starting summit and stay grounded. Compare the shared outcome and stopped time on both PCs. |

## Test order

Start with a room lifecycle check. Create, join, start, leave as Client and then rejoin. Verify control recovery after rejoining. Leave as Host and verify both can use a new room. Start the timed course from a fresh room and use the table above. Log elapsed wall-clock time separately from the in-game timer and note retries and pauses. Save a second run for deliberate failures such as a late timed jump, gem loss, a missed launch or a disconnect during a cooperative action.

At each section, check objective clarity, the local camera and framing, input response, camera recovery after Esc, and whether the prompt matches the action when targets overlap. Check PvP grab, throw and resistance in an agreed safe area of the course. Watch for stuck movement after release, respawn or reconnect, one E press affecting two mechanisms, unusable keypad controls, missing controls after a cancelled launch, inaccessible checkpoints, an unrecoverable gem, diverging puzzle state, and either player being unable to finish. Record a blocker as soon as it prevents continuing.

Rate mountain control, timber repetition, raft steering and obstacle avoidance, shared-wall readability, the 25-second crossing window, gem theft fairness and overall duration on separate 1–5 scales. Write down what happened for any low score. The historical 42:25.908 result was an automated replay. Record each tester's actual wall-clock duration here.

### Session sheet

Record the build, date, testers, Host/Client roles, castle slots, PC specs and network locations once per session. Use one copy of the table per tester.

| Section | Started | Ended | Retries or pauses | Rating / notes |
| --- | --- | --- | --- | --- |
| Outbound mountain | | | | |
| Outbound bridge | | | | |
| Canyon descent | | | | |
| Linked raft | | | | |
| Outbound castle | | | | |
| Underground and seal | | | | |
| Return underground and castle | | | | |
| Solo raft | | | | |
| Canyon ascent | | | | |
| Return bridge | | | | |
| Return mountain and finish | | | | |

### Issue sheet

Record the build and date, Host/Client role, section and phase, nearest landmark/checkpoint, whether either player had the gem, and first failing elapsed time. List exact inputs and reproduction steps. Record expected and observed outcomes from both PCs, occurrence count, whether there was a recovery path, and whether play had to stop. Attach both screenshots or video and relevant excerpts from `%USERPROFILE%\AppData\LocalLow\DefaultCompany\MyGame\Player.log`. Copy logs before restarting and redact active room codes.

See the [full match replay](FullMatchGameplay.md) for automated evidence and the [contest rules](PvPAndGemGameplay.md) for gem recovery and finish eligibility.
