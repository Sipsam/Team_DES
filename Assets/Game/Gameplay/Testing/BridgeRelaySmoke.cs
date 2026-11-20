#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TwinEdge.Gameplay
{
    public sealed class BridgeRelaySmoke : MonoBehaviour
    {
        bool host, failed, carryDisconnect, contactWithoutFall;
        FullMatchRelayReplay fullMatch;
        string output, peer;
        BridgeGameplay bridge;
        PlayerMovement player;
        Keyboard keyboard;
        BridgePlankV3[] planks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int mode = Array.IndexOf(args, "-bridgeSmoke");
            if (mode < 0) return;
            var test = new GameObject("BridgeRelaySmoke").AddComponent<BridgeRelaySmoke>();
            test.host = args[mode + 1] == "host";
            test.carryDisconnect = Array.IndexOf(args, "-bridgeCarryDisconnect") >= 0;
            test.contactWithoutFall = Array.IndexOf(args, "-bridgeContactWithoutFall") >= 0;
            test.peer = test.host ? "host" : "client";
            test.output = args[Array.IndexOf(args, "-bridgeEvidence") + 1];
            Directory.CreateDirectory(test.output);
        }

        IEnumerator Start()
        {
            if (fullMatch != null) yield break;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += Log;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var manager = FindFirstObjectByType<NetworkManager>();
            var sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            var game = FindFirstObjectByType<GameStateManager>();
            bridge = game.GetComponent<BridgeGameplay>();
            yield return Until(() => bridge.PlankCount == 198 && FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4, "V3 ready", 80);
            if (host)
            {
                var room = sessions.CreateRoomAsync();
                yield return Until(() => room.IsCompleted, "Relay create", 90);
                Check(sessions.HasSession, "Relay host");
                File.WriteAllText(output + "/room.tmp", sessions.CurrentJoinCode);
                File.Move(output + "/room.tmp", output + "/room.txt");
            }
            else
            {
                yield return Until(() => File.Exists(output + "/room.txt"), "Relay code", 100);
                var room = sessions.JoinRoomAsync(File.ReadAllText(output + "/room.txt"));
                yield return Until(() => room.IsCompleted, "Relay join", 90);
                Check(sessions.HasSession, "Relay client");
            }
            yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Two players", 90);
            if (host) game.StartFoundation();
            yield return Until(() => game.CanPlay, "Outbound");
            planks = FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None).OrderBy(p => p.plankId).ToArray();
            Check(planks.Length == bridge.PlankCount, "Exactly one synchronized deck, found " + planks.Length);
            CheckSupport(0);
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
            var side = Vector3.right * (host ? -bridge.GripHalfWidth : bridge.GripHalfWidth);
            // Only initial summit station placement. All deliveries and crossing use production input and CC.
            Teleport(bridge.SupplyPoint + side);
            yield return Barrier("station");

            yield return BuildAndCross(sessions, manager, game, side);
            if (carryDisconnect) yield break;
            if (host)
            {
                Check(game.AdvanceFoundation(FoundationPhase.SealPending), "Seal test transition");
                Check(game.AdvanceFoundation(FoundationPhase.Return), "Return test transition");
            }
            yield return ReturnCrossing(side);
            File.WriteAllText(output + "/" + peer + "-pass.txt", "PASS outbound and independent Return crossings with one permanently collapsed plank.");
            yield return Barrier("finish");
            Application.Quit(0);
        }

        internal IEnumerator ReplayOutbound(FullMatchRelayReplay match)
        {
            fullMatch = match;
            host = match.Host;
            peer = match.Role;
            output = match.Output;
            bridge = BridgeGameplay.Current;
            player = match.Player;
            keyboard = match.Keyboard;
            planks = FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None).OrderBy(p => p.plankId).ToArray();
            var side = Vector3.right * (host ? -bridge.GripHalfWidth : bridge.GripHalfWidth);
            yield return BuildAndCross(null, match.Manager, match.Game, side);
        }

        internal IEnumerator ReplayReturn()
        {
            contactWithoutFall = true;
            var side = Vector3.right * (host ? -bridge.GripHalfWidth : bridge.GripHalfWidth);
            yield return ReturnCrossing(side);
        }

        IEnumerator BuildAndCross(MultiplayerSessionManager sessions, NetworkManager manager, GameStateManager game, Vector3 side)
        {
            for (int delivery = 0; delivery < bridge.TotalDeliveries; delivery++)
            {
                yield return WalkTo(bridge.SupplyPoint + side, true);
                yield return Barrier("supply" + delivery);
                if (host) yield return Tap(Key.E);
                yield return Until(() => bridge.Load.supplied, "E supply " + delivery);
                yield return Until(() => ReferenceEquals(player.GetComponent<PlayerInteraction>().CurrentTarget, bridge), "Grip target " + delivery);
                if (!host) yield return Until(() => bridge.Load.left != BridgeGameplay.LoadState.Empty, "Left grip occupied");
                if (host && delivery == 0 && fullMatch == null)
                {
                    yield return Tap(Key.E);
                    yield return Until(() => bridge.Holds(player.OwnerClientId), "Solo grip");
                    yield return Tap(Key.E);
                    Check(!bridge.Load.Paired && bridge.Load.right == BridgeGameplay.LoadState.Empty, "The same player cannot occupy both handles");
                    var center = bridge.Load.center;
                    Keys(Key.W);
                    yield return new WaitForSeconds(.3f);
                    Keys();
                    Check(bridge.Load.center == center && bridge.CompletedDeliveries == 0, "Solo cannot move or install");
                    yield return MarkCancel();
                    yield return Until(() => File.Exists(output + "/client-cancel-observed.txt"), "Client saw cancellation");
                    yield return Tap(Key.E);
                    yield return Until(() => bridge.Load.supplied, "Timber supplied again");
                }
                if (!host && delivery == 0 && fullMatch == null)
                {
                    yield return Until(() => File.Exists(output + "/host-cancel.txt") && !bridge.Load.supplied, "Cancellation replicated");
                    File.WriteAllText(output + "/client-cancel-observed.txt", "PASS");
                    yield return Until(() => bridge.Load.supplied && bridge.Load.left != BridgeGameplay.LoadState.Empty, "Renewed left hold");
                }
                yield return Tap(Key.E);
                yield return Until(() => bridge.Load.Paired, "Both ends grabbed " + delivery);
                float startZ = bridge.Load.center.z;
                if (delivery == 0 && fullMatch == null)
                {
                    if (host) Keys(Key.W);
                    yield return Barrier("single-input");
                    yield return new WaitForSeconds(.35f);
                    Check(Mathf.Abs(bridge.Load.center.z - startZ) < .02f, "One movement input cannot move paired timber");
                    Keys();
                    yield return Barrier("both-input");
                }
                if (carryDisconnect)
                {
                    float yaw = bridge.Load.yaw;
                    Keys(host ? Key.W : Key.S);
                    yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(yaw, bridge.Load.yaw)) >= 6f, "Opposing inputs rotated the carried load");
                    Keys();
                    yield return Barrier("rotation");
                    Check(Mathf.Abs(bridge.Load.center.z - startZ) < .4f, "Rotation opposed translation");
                }
                Keys(Key.W);
                yield return Until(() => bridge.Load.center.z >= bridge.SocketPoint.z - .38f, "Two-input carry " + delivery, 12);
                Keys();
                if (carryDisconnect)
                {
                    yield return Barrier("moving-before-disconnect");
                    if (!host)
                    {
                        var leave = sessions.LeaveRoomAsync();
                        yield return Until(() => leave.IsCompleted && !manager.IsListening, "Client left during carry", 30);
                        yield return Until(() => File.Exists(output + "/host-released.txt"), "Host reset load");
                        var join = sessions.JoinRoomAsync(File.ReadAllText(output + "/room.txt"));
                        yield return Until(() => join.IsCompleted && game.CanPlay && manager.LocalClient?.PlayerObject != null, "Client rejoined", 70);
                        player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
                        var rejoined = player.GetComponent<GameplayCharacter>();
                        yield return Until(() => rejoined.CheckpointName.Value.ToString().Contains("Big summit"), "Rejoin uses Big summit checkpoint");
                        Check(Vector3.Distance(rejoined.transform.position, bridge.StartPoint) < 100, "Rejoin player near bridge");
                        File.WriteAllText(output + "/client-rejoined.txt", "PASS");
                        yield return Until(() => File.Exists(output + "/host-outbound-fall.txt"), "Host completed outbound fall check", 40);
                    }
                    else
                    {
                        yield return Until(() => !game.IsGameReady && !bridge.Load.supplied && !bridge.Holds(player.OwnerClientId), "Survivor released after disconnect");
                        Check(bridge.CompletedDeliveries == 0 && player.GetComponent<CharacterController>().enabled, "No installation or controller lock after disconnect");
                        File.WriteAllText(output + "/host-released.txt", "PASS");
                        yield return Until(() => game.CanPlay && File.Exists(output + "/client-rejoined.txt"), "Outbound resumed after rejoin", 80);
                        var character = player.GetComponent<GameplayCharacter>();
                        int before = character.RespawnCount.Value;
                        // Current carry already left the host on the start stub. Walk into the unbuilt span.
                        Keys(Key.W);
                        yield return Until(() => player.transform.position.z > bridge.StartPoint.z + 1.2f, "Walk off the uninstalled deck");
                        Keys();
                        yield return Until(() => character.RespawnCount.Value > before && character.CheckpointName.Value.ToString().Contains("Big summit") &&
                            (player.transform.position - bridge.StartPoint).sqrMagnitude > 100, "Gravity fall respawn at Big summit");
                        File.WriteAllText(output + "/host-outbound-fall.txt", "PASS actual walk into unbuilt span and Big summit recovery");
                        yield return Until(() => File.Exists(output + "/client-disconnect-pass.txt"), "Client observed final recovery");
                    }
                    File.WriteAllText(output + "/" + peer + "-disconnect-pass.txt", "PASS");
                    Application.Quit(0);
                    yield break;
                }
                yield return Barrier("socket" + delivery);
                if (host && delivery == 0 && fullMatch == null) SaveRender("carry", bridge.Load.center + Vector3.up, new Vector3(6, 5, -8));
                if (delivery == 0) fullMatch?.Capture("bridge-first-delivery");
                yield return new WaitForSeconds(.4f);
                Check(bridge.Load.center.z - startZ > 4f && Vector3.Distance(player.transform.position,
                    bridge.Load.Grip(!host, bridge.GripHalfWidth)) < .65f, "Load travelled and owner aligned " + delivery);
                if (!host) yield return Until(() => bridge.Load.approvals != 0, "First approval " + delivery);
                yield return Tap(Key.E);
                int expected = delivery + 1;
                yield return Until(() => bridge.CompletedDeliveries == expected, "Server installation " + expected);
                CheckSupport(bridge.InstalledCount);
                yield return Barrier("installed" + expected);
            }

            int respawns = player.GetComponent<GameplayCharacter>().RespawnCount.Value;
            yield return WalkTo(bridge.EndPoint + side + Vector3.forward * 3, true);
            Check(player.transform.position.z > bridge.EndPoint.z && player.GetComponent<GameplayCharacter>().RespawnCount.Value == respawns,
                "Continuous summit-to-Canyon outbound crossing");
            yield return Barrier("outbound");
            if (host && fullMatch == null) SaveRender("outbound", bridge.EndPoint - Vector3.forward * 12, new Vector3(9, 6, 12));
            File.WriteAllText(output + "/" + peer + "-outbound-pass.txt", "PASS real Relay, 13 deliveries, original support colliders and outbound crossing.");
        }

        IEnumerator ReturnCrossing(Vector3 side)
        {
            var character = player.GetComponent<GameplayCharacter>();
            if (host)
            {
                yield return Until(() => bridge.ReadServerHazardsForTest() != null, "Server selected Return hazards");
                // Out-of-band driving instructions only for the automated route. The gameplay client receives no hazard map.
                var hazards = bridge.ReadServerHazardsForTest();
                File.WriteAllText(output + "/route.tmp", string.Concat(hazards.Select(value => value ? "1" : "0")));
                File.Move(output + "/route.tmp", output + "/route.txt");
            }
            yield return Until(() => bridge.ReturnActive && File.Exists(output + "/route.txt"), "Return ready");
            CheckSupport(planks.Length);
            if (!host) Check(bridge.ReadServerHazardsForTest() == null, "Client has no hazard assignment");
            yield return Barrier("hidden-hazards");
            var route = File.ReadAllText(output + "/route.txt").Select(value => value == '1').ToArray();
            int hole = Enumerable.Range(16, planks.Length - 32).First(i =>
                route[i] && !route[i + 1] && (!contactWithoutFall || !route[i - 1]));
            if (host && !contactWithoutFall)
            {
                // Separate Canyon-facing fall station. Footstep and resulting fall use normal movement.
                Teleport(planks[hole + 1].socket + side);
                yield return new WaitForSeconds(.4f);
                int before = character.RespawnCount.Value;
                Keys(Key.S);
                yield return Until(() => bridge.IsCollapsed(hole), "Real support contact collapsed plank");
                yield return Until(() => player.transform.position.z < planks[hole].socket.z + .12f, "Enter missing support");
                Keys();
                yield return Until(() => character.RespawnCount.Value > before, "Gravity fall");
                yield return Until(() => character.CheckpointName.Value.ToString() == "Lower dock" && player.transform.position.y < 170, "Lower dock respawn");
                Check(bridge.IsCollapsed(hole), "Collapse persists after respawn");
                SaveRender("collapsed", planks[hole].socket, new Vector3(8, 6, 7));
                File.WriteAllText(output + "/host-fall-pass.txt", "PASS contact, gravity, Lower dock, persistent collapse");
                Teleport(bridge.EndPoint + side + Vector3.forward * 3);
            }
            if (!contactWithoutFall)
                yield return Until(() => bridge.IsCollapsed(hole) && File.Exists(output + "/host-fall-pass.txt"), "Collapse and respawn replicated");
            CheckSupport(planks.Length);
            yield return Barrier("return-crossing-start");
            int respawns = character.RespawnCount.Value;
            int singles = 0, doubles = 0;
            for (int high = route.Length - 1; high > 0; high--)
            {
                if (route[high] || !route[high - 1]) continue;
                int low = high - 2;
                if (route[low]) low--;
                bool sprint = high - low == 3;
                var bounds = planks[high].GetComponent<Collider>().bounds;
                var takeoff = planks[high].socket + side;
                bool contact = contactWithoutFall && high == hole + 1;
                takeoff.z = bounds.min.z + (contact && fullMatch == null ? -.21f : .17f);
                yield return WalkTo(takeoff, true, contact && fullMatch == null ? .05f : .13f);
                if (contact)
                {
                    if (fullMatch != null && host)
                    {
                        yield return TouchHazardEdge(hole, bounds.min.z);
                        yield return WalkTo(takeoff, false, .08f);
                    }
                    yield return Until(() => bridge.IsCollapsed(hole), "Bridge contact collapsed a plank");
                    fullMatch?.Capture("bridge-collapse");
                }
                yield return Until(() => player.IsGrounded, "Grounded takeoff " + high);
                if (sprint) Keys(Key.S, Key.Space, Key.LeftShift);
                else Keys(Key.S, Key.Space);
                yield return Until(() => !player.IsGrounded, "Owner jump " + high);
                yield return Until(() => player.IsGrounded, "Owner landed " + low, 2.5f);
                Keys();
                var landing = planks[low].GetComponent<Collider>().bounds;
                Check(player.transform.position.y > bridge.StartPoint.y - .2f &&
                    player.transform.position.z <= landing.max.z + .3f && player.transform.position.z >= landing.min.z - .3f,
                    $"Jump {high}->{low} landed on required remaining support");
                if (sprint) doubles++;
                else singles++;
                Check(character.RespawnCount.Value == respawns, "No respawn during jump " + high);
                yield return new WaitForSeconds(.08f);
            }
            yield return WalkTo(bridge.StartPoint + side - Vector3.forward * 3, true);
            Check(player.transform.position.z < bridge.StartPoint.z && singles > 0 && doubles > 0 &&
                character.RespawnCount.Value == respawns && bridge.IsCollapsed(hole), "Return crossing after permanent collapse");
            File.WriteAllText(output + "/" + peer + "-return-pass.txt", $"PASS {singles} walk jumps and {doubles} sprint jumps, no route teleport or respawn");
        }

        IEnumerator TouchHazardEdge(int hazard, float safeEdge)
        {
            // Keep the center in the seam and overlap both supports with the capsule.
            // The same walk/jump motion is used after retreating to the sound plank.
            float deadline = Time.realtimeSinceStartup + 3;
            float target = safeEdge - .135f;
            while (!bridge.IsCollapsed(hazard) && player.transform.position.z > target)
            {
                Check(Time.realtimeSinceStartup < deadline && player.transform.position.y >= bridge.StartPoint.y - .12f,
                    "Approach collapse contact on the supported edge");
                var camera = Camera.main.transform;
                float amount = Mathf.Min(.23f, (player.transform.position.z - target) / .16f);
                Vector2 input = new(-Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized.z,
                    -Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized.z);
                if (player.transform.position.z - target < .008f) break;
                fullMatch.SteerPole(input * amount);
                yield return null;
            }
            fullMatch.SteerPole(Vector2.zero);
            yield return new WaitForSeconds(.24f);
            Check(bridge.IsCollapsed(hazard), "Real safe-edge contact collapsed the adjacent plank");
        }

        IEnumerator MarkCancel()
        {
            yield return Tap(Key.Escape);
            yield return Until(() => !bridge.Load.supplied && !bridge.Holds(player.OwnerClientId), "Escape cancellation");
            File.WriteAllText(output + "/host-cancel.txt", "PASS");
        }

        void CheckSupport(int count)
        {
            for (int i = 0; i < planks.Length; i++)
            {
                var plank = planks[i];
                bool enabled = i < count && !bridge.IsCollapsed(i);
                Check(plank.GetComponent<Collider>().enabled == enabled && plank.GetComponent<Renderer>().enabled == enabled, "Deck state " + i);
            }
        }

        IEnumerator WalkTo(Vector3 target, bool sprint, float tolerance = .28f)
        {
            if (fullMatch != null)
            {
                yield return fullMatch.Walk(target, tolerance, sprint);
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 25;
            while (true)
            {
                var delta = target - player.transform.position;
                if (Mathf.Abs(delta.x) < tolerance && Mathf.Abs(delta.z) < tolerance) break;
                Check(Time.realtimeSinceStartup < deadline, "Walk to " + target + " from " + player.transform.position);
                var keys = new System.Collections.Generic.List<Key>();
                if (Mathf.Abs(delta.x) >= tolerance * .8f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(delta.z) >= tolerance * .8f) keys.Add(delta.z > 0 ? Key.W : Key.S);
                if (sprint && delta.sqrMagnitude > 2) keys.Add(Key.LeftShift);
                Keys(keys.ToArray());
                yield return null;
            }
            Keys();
            yield return new WaitForSeconds(.18f);
        }

        void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.GetComponent<OwnerNetworkTransform>().Teleport(V3GameplayBootstrap.SafeGround(position), Quaternion.identity, Vector3.one);
            player.ResetVerticalVelocity();
            controller.enabled = true;
        }

        void SaveRender(string label, Vector3 focus, Vector3 offset)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cameraObject = new GameObject("Bridge evidence camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.farClipPlane = 2000;
            camera.transform.position = focus + offset;
            camera.transform.LookAt(focus);
            var target = new RenderTexture(960, 540, 24);
            var prior = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(output + "/" + label + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prior;
                camera.targetTexture = null;
                target.Release();
                Destroy(target);
                Destroy(image);
                Destroy(cameraObject);
            }
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return new WaitForSeconds(.12f);
            Keys();
            yield return new WaitForSeconds(.12f);
        }

        IEnumerator Barrier(string name)
        {
            File.WriteAllText(output + "/" + peer + "-" + name + ".txt", player.transform.position.ToString("F3"));
            yield return Until(() => File.Exists(output + "/" + (host ? "client" : "host") + "-" + name + ".txt"), name, 35);
        }

        IEnumerator Until(Func<bool> condition, string label, float seconds = 10)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Check(Time.realtimeSinceStartup < deadline, label);
                yield return null;
            }
            Debug.Log("BRIDGE_RELAY " + label);
        }

        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("BRIDGE_RELAY " + label +
                $" player={(player != null ? player.transform.position.ToString("F3") : "none")} load={bridge?.Load.center} completed={bridge?.CompletedDeliveries}");
        }

        void Log(string message, string stack, LogType type)
        {
            if (failed || type != LogType.Exception && type != LogType.Assert) return;
            failed = true;
            File.WriteAllText(output + "/" + peer + "-fail.txt", message + "\n" + stack);
            Application.Quit(1);
        }
    }
}
#endif
