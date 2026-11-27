using System;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TwinEdge.Presentation
{
    internal sealed class MatchTransitionPresentation : IDisposable
    {
        readonly CanvasGroup banner;
        readonly RectTransform panel;
        readonly Text title, detail;
        readonly Image edge, wash;
        readonly Transform world;
        GameObject burst;
        Light glow;
        Mesh shardMesh;
        Material shardMaterial;
        readonly Transform[] shards = new Transform[10];
        uint sequence;
        bool wasLocked, wasVisible, wasAccepting;

        // Local visual setting hook for PRES-08. The server's input window is independent.
        internal static float EffectIntensity => LocalPresentationSettings.EffectPercent / 100f;

        internal MatchTransitionPresentation(UiKit ui, Transform overlay, Transform world)
        {
            this.world = world;
            wash = UiKit.Surface(overlay, "Return emphasis", Color.clear);
            UiKit.Stretch(wash.rectTransform);
            wash.rectTransform.SetAsFirstSibling();
            var rect = panel = UiKit.Rect(overlay, "Match transition");
            rect.anchorMin = new Vector2(.26f, .78f);
            rect.anchorMax = new Vector2(.74f, .78f);
            rect.offsetMin = new Vector2(0, -72);
            rect.offsetMax = new Vector2(0, 72);
            banner = rect.gameObject.AddComponent<CanvasGroup>();
            banner.blocksRaycasts = banner.interactable = false;
            var background = UiKit.Surface(rect, "Transition backing", PresentationTheme.Background);
            UiKit.Stretch(background.rectTransform);
            edge = UiKit.Surface(rect, "Phase marker", PresentationTheme.Cooperation);
            UiKit.Stretch(edge.rectTransform);
            edge.rectTransform.anchorMin = new Vector2(0, 1);
            edge.rectTransform.offsetMin = new Vector2(0, -4);
            title = ui.Text(rect, "", 44, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(title.rectTransform, 8, 52, 8, 5);
            detail = ui.Text(rect, "", 24, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            UiKit.Stretch(detail.rectTransform, 12, 10, 12, 83);
            Hide();
        }

        internal bool Refresh(GameStateManager game, int slot)
        {
            var window = game.Transition;
            double now = game.MatchServerTime;
            bool countdown = window.Kind == MatchTransition.Countdown;
            bool locked = game.TransitionBlocksInput;
            if (sequence != window.Sequence || locked != wasLocked)
            {
                Trace(window, locked ? "window-received" : "input-deadline-reached", now);
                if (sequence != window.Sequence) wasVisible = false;
                sequence = window.Sequence;
                wasLocked = locked;
            }

            if (wasAccepting != game.AcceptsGameplay)
            {
                wasAccepting = game.AcceptsGameplay;
                if (wasAccepting) Trace(window, "actions-enabled", now);
            }

            bool startNotice = countdown && game.Phase == FoundationPhase.Outbound &&
                now >= window.End && now < window.End + GameStateManager.OutboundNoticeSeconds;
            if (window.Kind == MatchTransition.None || (!locked && !startNotice) || now < window.Start)
            {
                Hide();
                return false;
            }

            if (!wasVisible)
            {
                Trace(window, "first-display", now);
                wasVisible = true;
            }

            float age = (float)(now - window.Start);
            float remaining = (float)((startNotice ? window.End + GameStateManager.OutboundNoticeSeconds : window.End) - now);
            float intensity = Mathf.Clamp01(EffectIntensity);
            bool returning = window.Kind == MatchTransition.Return;
            edge.color = returning ? PresentationTheme.Competition : PresentationTheme.Cooperation;
            float fade = Mathf.Clamp01(remaining / .24f);
            banner.alpha = Mathf.Lerp(1, fade, intensity);
            banner.gameObject.SetActive(true);

            bool number = countdown && !startNotice;
            panel.anchorMin = new Vector2(number ? .455f : .26f, .78f);
            panel.anchorMax = new Vector2(number ? .545f : .74f, .78f);
            panel.offsetMin = new Vector2(0, number ? -56 : -72);
            panel.offsetMax = new Vector2(0, number ? 56 : 72);
            if (number)
            {
                int beat = Mathf.Clamp(Mathf.FloorToInt(age), 0, 2);
                title.text = (3 - beat).ToString();
                title.fontSize = 62;
                UiKit.Stretch(title.rectTransform, 8, 4, 8, 4);
                detail.text = "";
            }
            else
            {
                title.fontSize = 44;
                UiKit.Stretch(title.rectTransform, 8, countdown || returning ? 52 : 4, 8, 5);
                title.text = countdown ? "왕로 시작" : returning ? "복로 시작" : "공동 봉인이 해제되었습니다";
                detail.text = countdown ? "서로 협력하여 보석을 찾으세요." : !returning ? "" : window.CarrierSlot == slot
                    ? "보석을 지켜 결승점으로 돌아가세요."
                    : "상대의 보석을 빼앗아 결승점으로 돌아가세요.";
            }

            Color tint = PresentationTheme.Competition;
            tint.a = returning ? .13f * fade * Mathf.Clamp01(age / .16f) * intensity : 0;
            wash.color = tint;
            if (!countdown && intensity > 0)
                RefreshBurst(window, age, intensity);
            else if (burst)
                burst.SetActive(false);
            return true;
        }

        void RefreshBurst(MatchTransitionWindow window, float age, float intensity)
        {
            if (!burst) CreateBurst();
            burst.SetActive(true);
            burst.transform.position = window.Point;
            bool seal = window.Kind == MatchTransition.Seal;
            Color color = seal ? new Color(.55f, .79f, 1f) : new Color(1f, .73f, .32f);
            float t = Mathf.Clamp01(age / (float)(window.End - window.Start));
            float energy = Mathf.SmoothStep(0, 1, age / .15f) * (1 - t) * intensity;
            glow.color = color;
            glow.intensity = energy * 2;
            if (!shardMaterial) return;
            shardMaterial.SetColor("_BaseColor", color);
            shardMaterial.SetColor("_EmissionColor", color * (1.4f * energy));
            for (int i = 0; i < shards.Length; i++)
            {
                float angle = i * 2.39996f + t * .55f;
                float radius = .16f + t * (.75f + .22f * (i % 4));
                Vector3 direction = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var shard = shards[i];
                shard.localPosition = direction * radius + Vector3.up * (.65f * Mathf.Sin(t * Mathf.PI) + t * (i % 3) * .22f);
                shard.localRotation = Quaternion.Euler(i * 37 + age * 110, i * 59, age * 75);
                shard.localScale = new Vector3(.10f, .19f, .10f) * energy;
            }
        }

        void CreateBurst()
        {
            burst = new GameObject("Match transition world effects");
            burst.transform.SetParent(world, false);
            glow = burst.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 3.5f;
            glow.shadows = LightShadows.None;
            var source = GemOwnership.Current?.VisualMaterial;
            if (!source || !source.HasProperty("_EmissionColor")) return;
            shardMaterial = new Material(source);
            shardMaterial.EnableKeyword("_EMISSION");
            shardMesh = new Mesh { name = "Transition crystal" };
            shardMesh.vertices = new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down };
            shardMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            shardMesh.RecalculateNormals();
            for (int i = 0; i < shards.Length; i++)
            {
                var shard = new GameObject("Crystal", typeof(MeshFilter), typeof(MeshRenderer));
                shards[i] = shard.transform;
                shards[i].SetParent(burst.transform, false);
                shard.GetComponent<MeshFilter>().sharedMesh = shardMesh;
                var renderer = shard.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = shardMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        internal void Hide()
        {
            banner.gameObject.SetActive(false);
            wash.color = Color.clear;
            if (burst) burst.SetActive(false);
        }

        public void Dispose()
        {
            if (burst) Object.Destroy(burst);
            if (shardMaterial) Object.Destroy(shardMaterial);
            if (shardMesh) Object.Destroy(shardMesh);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void Trace(MatchTransitionWindow window, string action, double now) =>
            Debug.Log($"MATCH_TRANSITION {action} kind={window.Kind} seq={window.Sequence} time={now:F3} start={window.Start:F3} end={window.End:F3}");
    }
}
