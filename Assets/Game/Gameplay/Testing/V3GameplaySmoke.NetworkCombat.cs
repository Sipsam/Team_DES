#if DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class V3GameplaySmoke
{
    void PrepareNetworkCombatStation()
    {
        var arena = MarkerPoint("Mountain.V3.ReturnFinish") + Vector3.forward * 11;
        var spawns = V3GameplayBootstrap.Current.spawnPoints;
        for (int i = 0; i < spawns.Length; i++)
        {
            float side = i == 0 ? -1 : 1;
            var foot = V3GameplayBootstrap.SafeGround(arena + Vector3.right * (.775f * side));
            spawns[i].SetPositionAndRotation(foot, Quaternion.Euler(0, -90 * side, 0));
        }
    }

    IEnumerator RunNetworkCombat(GameStateManager game, GameplayCharacter remote)
    {
        var probe = RelayNetworkProbe.Current;
        probe.Segment = "combat";
        var own = player.GetComponent<GameplayCharacter>();
        var attacker = host ? own : remote;
        var victim = host ? remote : own;
        var initialVictim = victim.transform.position;
        contestMouse = InputSystem.AddDevice<Mouse>();
        if (!host) own.RequestHitForLifeForTest(own.OwnerLife - 1);
        yield return SyncContest("network-combat-old-life");
        yield return new WaitForSecondsRealtime(1);
        Check(victim.AttackReadyAtForTest == 0, "Old life ID attack was refused");

        if (host) yield return Click();
        yield return Until(() => victim.ControlledUntilForTest > 0, "Mouse attack and server hit replicated");
        yield return Until(() => victim.transform.position.x > initialVictim.x + .4f, "Hit displaced owner capsule");
        yield return Until(() => manager.ServerTime.Time > victim.ControlledUntilForTest + .4 && victim.HasGround(), "Victim recovered from hit");
        if (!host)
        {
            Camera.main.GetComponent<ThirdPersonCamera>().FaceInitialSpawn(Quaternion.Euler(0, -90, 0));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Until(() => victim.transform.position.x < attacker.transform.position.x + 1.65f,
                "Victim returned into grab range by walking");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
        yield return SyncContest("network-combat-in-range");
        yield return new WaitForSecondsRealtime(.7f);
        if (host) yield return Tap(Key.F);
        yield return Until(() => attacker.HeldForTest == victim.OwnerClientId && victim.HolderForTest == attacker.OwnerClientId,
            "Both peers observed approved grab and reciprocal owner IDs");
        yield return Until(() => attacker.ChargeForTest > .35f, "Server-authoritative throw charge was visible");
        yield return SyncContest("network-combat-charged");
        Vector3 beforeThrow = victim.transform.position;
        if (host) yield return Tap(Key.F);
        yield return Until(() => attacker.HeldForTest == ulong.MaxValue && victim.HolderForTest == ulong.MaxValue,
            "Throw cleared both replicated ownership relationships");
        yield return Until(() => victim.transform.position.x > beforeThrow.x + .5f, "Applied throw moved the owner capsule");
        yield return SyncContest("network-combat-complete");
        probe.Note("one valid hit, grab and throw; previous-life hit rejected");
        probe.Automatic = false;
        probe.Restore();
        yield return SyncContest("network-combat-restored");
        if (host) yield return Until(() => !game.IsGameReady, "Combat Client disconnected", 45);
        var leave = sessions.LeaveRoomAsync();
        yield return Until(() => leave.IsCompleted && !manager.IsListening, "Combat Relay room closed");
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS live strike, grab, throw and stale life rejection.");
        Application.Quit(failed ? 2 : 0);
    }
}
#endif
