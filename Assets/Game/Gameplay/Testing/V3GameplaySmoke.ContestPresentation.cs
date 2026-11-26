#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public sealed partial class V3GameplaySmoke
{
    static readonly Vector3 ContestFloor = new(50, 500, 50);
    bool ContestScreenshots => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null &&
        Environment.GetCommandLineArgs().Contains("-v3ContestScreenshots");

    void CaptureContestPair(string label, GameplayCharacter hitter, GameplayCharacter victim)
    {
        var camera = Camera.main.transform;
        var original = new Pose(camera.position, camera.rotation);
        Vector3 center = (hitter.transform.position + victim.transform.position) * .5f + Vector3.up * 1.05f;
        try
        {
            camera.position = center + new Vector3(host ? -1.5f : 1.5f, .7f, -3.8f);
            camera.LookAt(center);
            CaptureContest(label);
        }
        finally
        {
            camera.SetPositionAndRotation(original.position, original.rotation);
        }
    }

    IEnumerator CheckCapeColors(GameplayCharacter hitter, GameplayCharacter victim, string suffix = "")
    {
        yield return Until(() => hitter.CapeIndexForTest < GameplayCharacter.CapeColorCount &&
            victim.CapeIndexForTest < GameplayCharacter.CapeColorCount, "both cape colors assigned");
        string Cape(GameplayCharacter character)
        {
            Check(character.transform.Find("IdentityBand") == null &&
                character.GetComponent<CharacterController>().enabled, "box removed and capsule enabled");
            var properties = new MaterialPropertyBlock();
            character.CapeRenderer.GetPropertyBlock(properties);
            Check(!properties.isEmpty, "cape renderer receives a per-player override");
            return ColorUtility.ToHtmlStringRGB(properties.GetColor("_BaseColor"));
        }

        string first = Cape(hitter), second = Cape(victim);
        Check(first != second && hitter.CapeRenderer.sharedMaterial == victim.CapeRenderer.sharedMaterial,
            "distinct rendered cape colors use the same source material");
        string path = Path.Combine(evidence, "cape" + suffix);
        string colors = first + " " + second;
        File.WriteAllText($"{path}-{ContestRole}.txt", colors);
        yield return SyncContest("color" + suffix);
        Check(colors == File.ReadAllText($"{path}-{(host ? "client" : "host")}.txt"), $"both peers render {colors}");
    }

    IEnumerator CheckAttackRange(GameplayCharacter hitter, GameplayCharacter victim)
    {
        Box("Contest floor", ContestFloor + Vector3.down * .5f, new Vector3(36, 1, 36));
        const float mainRange = 2.65f;
        const float range = mainRange * 1.3f;
        Check(Mathf.Abs(hitter.HitRangeForTest - range) < .0001f, "ordinary hit range 2.650m to 3.445m (1.300x)");
        for (int probe = 0; probe < 4; probe++)
        {
            yield return Until(() => manager.ServerTime.Time > hitter.AttackReadyAtForTest + .1, "range probe cooldown");
            float gap = probe == 0 ? range - .02f : probe == 1 ? range + .02f : 2.4f;
            float yaw = host ? (probe == 2 ? -90 : 90) : -90;
            ContestPlace(ContestFloor + Vector3.right * (host ? -.5f : .5f) * gap, Quaternion.Euler(0, yaw, 0));
            BoxCollider barrier = null;
            if (probe == 3)
            {
                barrier = new GameObject("Contest sight-line wall").AddComponent<BoxCollider>();
                barrier.transform.position = ContestFloor + Vector3.up;
                barrier.size = new Vector3(.12f, 2, 4);
            }
            yield return SyncContest($"strike-probe-{probe}");
            yield return new WaitForSeconds(StagePause);
            float span = Vector3.Distance(hitter.transform.position, victim.transform.position);
            Check(Mathf.Abs(span - gap) < .01f, $"range probe {probe} root distance {span:F3}m");
            double ready = hitter.AttackReadyAtForTest;
            double hit = victim.HitReactAtForTest, selfHit = hitter.HitReactAtForTest;
            if (host) yield return Click();
            yield return Until(() => hitter.AttackReadyAtForTest > ready, "new swing accepted");
            if (probe == 0)
            {
                yield return Until(() => victim.HitReactAtForTest > hit, "3.425m strike landed");
                double delay = victim.HitReactAtForTest - hitter.AttackContactAtForTest;
                Check(delay >= 0 && delay < .06, $"actual impact {delay:F3}s from rendered contact on server clock");
                yield return Until(() => hitter.ContestPoseAppliedForTest && victim.ContestPoseAppliedForTest,
                    "both strike follow-through and victim recoil rendered");
            }
            else
            {
                yield return Until(() => manager.ServerTime.Time > hitter.AttackContactAtForTest + .25,
                    "server passed strike time");
                string rule = probe == 1 ? "3.465m strike rejected" : probe == 2 ? "rear strike rejected" : "wall blocked strike";
                Check(victim.HitReactAtForTest == hit, rule);
            }
            Check(hitter.HitReactAtForTest == selfHit, "attack cannot hit its owner");
            if (barrier != null) Destroy(barrier.gameObject);
            yield return SyncContest($"strike-probe-{probe}-checked");
        }
    }

    IEnumerator CheckCarryStride(GameplayCharacter hitter, GameplayCharacter victim)
    {
        var camera = Camera.main.GetComponent<ThirdPersonCamera>();
        camera.FaceInitialSpawn(Quaternion.Euler(0, host ? -90 : 90, 0));
        yield return PlacePair(ContestFloor, 1.55f, "carry-stride");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.CarriedOpponent == victim.transform && victim.ContestPoseAppliedForTest,
            "held pose renders on both peers");
        if (host)
        {
            yield return new WaitForSeconds(.2f);
            Check(camera.transform.position.y > hitter.transform.position.y + 4 &&
                camera.transform.position.x > victim.transform.position.x + 2.5f, "raised owner framing clears the lifted character");
            var wall = new GameObject("Carry camera wall").AddComponent<BoxCollider>();
            wall.transform.position = hitter.transform.position + new Vector3(3, 2.5f, 0);
            wall.size = new Vector3(.2f, 5, 5);
            Physics.SyncTransforms();
            yield return Until(() => camera.transform.position.x < wall.bounds.min.x - .15f,
                "carry camera still stops before a wall", 1);
            Destroy(wall.gameObject);
            yield return null;
            camera.FaceInitialSpawn(Quaternion.Euler(0, 90, 0));
            yield return MeasureStride(3.5f, Key.W);
            SetControlKeys(Key.W, Key.LeftShift);
            yield return Until(() => hitter.SprintAllowed, "carrier received normal sprint lease");
            yield return MeasureStride(8.4f, Key.W, Key.LeftShift);
            SetControlKeys();
        }
        else
        {
            yield return Until(() => hitter.AnimationState == "Walk" && victim.AnimationState == "Idle",
                "remote carrier Walk and local held pose");
            yield return Until(() => hitter.AnimationState == "Run" && victim.AnimationState == "Idle",
                "remote carrier Run and local held pose");
        }
        yield return SyncContest("carry-stride-checked");
        Check(hitter.CarriedOpponent == victim.transform && victim.AnimationState == "Idle",
            $"normal carry strides kept the grip and never entered Jump (stamina {hitter.StaminaFraction:P0})");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.CarriedOpponent == null, "carry stride fixture released into throw");
        yield return new WaitForSeconds(1f);
    }
}
#endif
