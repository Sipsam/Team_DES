using System;
using UnityEngine;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed partial class SessionPresentation
    {
        GameObject result;
        Text resultTitle, resultName, resultTime, resultStatus;
        RawImage resultImage;
        UiButton rematch, resultLeave;
        LobbyCharacterPreview resultPreview;

        void BuildResult()
        {
            result = Page("Match Result", 203);
            var panel = Panel(result, new Vector2(.5f, .5f), new Vector2(650, 785));
            resultTitle = Label(panel, "", 52, 86, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            resultName = Label(panel, "", PresentationTheme.HeadingSize, 52, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            resultName.resizeTextForBestFit = true;
            resultName.resizeTextMinSize = 18;
            resultName.resizeTextMaxSize = PresentationTheme.HeadingSize;
            var portrait = UiKit.Rect(panel, "Result character portrait");
            UiKit.Measure(portrait, 365);
            resultImage = UiKit.Rect(portrait, "Result character render target").gameObject.AddComponent<RawImage>();
            var rect = resultImage.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = Vector2.one * 365;
            resultImage.raycastTarget = false;
            resultImage.enabled = false;
            resultTime = Label(panel, "", PresentationTheme.BodySize, 50, PresentationTheme.Parchment, TextAnchor.MiddleCenter);
            resultStatus = Label(panel, "", PresentationTheme.CaptionSize, 56, PresentationTheme.SecondaryText, TextAnchor.MiddleCenter);
            var actions = Row(panel, 58);
            rematch = ui.Button(actions, "재경기", ToggleRematch);
            resultLeave = ui.Button(actions, "메인 메뉴", () => _ = Leave());
        }

        void ToggleRematch()
        {
            var state = game.state;
            int slot = game.manager.IsHost ? 0 : 1;
            if (state.IsSpawned)
                state.SetRematchConsentRpc(state.MatchIdentity, !state.HasRematchVote(slot));
        }

        void RefreshResult(bool busy)
        {
            int slot = game.manager.IsHost ? 0 : 1;
            var local = peers[slot];
            var state = game.state;
            if (!result.activeSelf || !local || !state.IsSpawned || state.Phase != FoundationPhase.Finished)
            {
                ClearResult();
                return;
            }

            bool won = state.Won(local.LobbySlot);
            SetText(resultTitle, won ? "승리" : "패배");
            resultTitle.color = won ? PresentationTheme.Accent : new Color32(229, 143, 132, 255);
            SetText(resultName, local.Nickname);
            SetText(resultTime, "경기 시간  " + FormatResultTime(state.ElapsedSeconds));
            if (resultPreview == null)
            {
                var visual = game.manager.NetworkConfig.PlayerPrefab.transform.Find("VisualRoot");
                resultPreview = new LobbyCharacterPreview(resultImage, visual, slot, won ? game.victory : game.defeat);
            }
            resultPreview.SetCape(local.CapeColorIndex);
            resultPreview.TickResult();

            bool voted = state.HasRematchVote(slot);
            bool opponentVoted = state.HasRematchVote(1 - slot);
            bool both = voted && opponentVoted;
            rematch.SetCaption(voted && !both ? "재경기 취소" : "재경기");
            rematch.interactable = !busy && state.IsGameReady && !both;
            resultLeave.interactable = !busy && session.HasSession && !both;
            SetText(resultStatus, both ? "새 경기를 준비 중입니다." :
                !state.IsGameReady ? "상대 플레이어의 재입장을 기다리는 중입니다." :
                state.RematchLoadFailed ? "새 경기를 준비하지 못했습니다. 다시 시도해 주세요." :
                voted ? "상대방의 재경기 응답을 기다리는 중입니다." :
                opponentVoted ? "상대방이 재경기를 요청했습니다." : "");
        }

        internal static string FormatResultTime(double seconds)
        {
            var elapsed = TimeSpan.FromSeconds(Math.Floor(seconds));
            string minutesAndSeconds = $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
            return elapsed.TotalHours >= 1 ? $"{(long)elapsed.TotalHours:00}:{minutesAndSeconds}" : minutesAndSeconds;
        }

        void ClearResult()
        {
            resultPreview?.Dispose();
            resultPreview = null;
        }
    }
}
