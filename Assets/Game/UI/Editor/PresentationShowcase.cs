using TwinEdge.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TwinEdge.Presentation.Editor
{
    // This assembly is Editor-only. The preview uses the same controls and root as future Player screens.
    internal sealed class PresentationShowcase
    {
        internal const string FontPath = "Assets/Game/UI/Fonts/NotoSansKR-Regular.otf";
        internal const string EmblemPath = "Assets/Game/UI/Art/TwinEdgeEmblem.png";
        internal const string MenuPath = "Tools/Twin Edge/Presentation/Toggle UI Component Showcase";

        internal readonly PresentationRoot Root;
        internal readonly GameObject Sample, Alternate, Dialog;
        internal readonly UiButton DialogButton, FadeButton, DisabledButton, CloseButton;
        internal readonly UiTextField Nickname, RoomCode;
        internal readonly Text LongError, Wordmark;
        internal readonly Image Stamina;

        static PresentationShowcase current;

        [MenuItem(MenuPath, true)]
        static bool CanToggle() => EditorApplication.isPlaying && V3GameplayBootstrap.Current &&
            V3GameplayBootstrap.Current.Presentation && !V3GameplayBootstrap.Current.manager.IsListening;

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            if (current != null && current.Root && current.Sample)
            {
                if (current.Root.IsTransitioning) return;
                current.Dispose();
                current = null;
            }
            else
            {
                current = new PresentationShowcase(V3GameplayBootstrap.Current.Presentation);
                current.Open();
            }
        }

        internal PresentationShowcase(PresentationRoot root)
        {
            Root = root;
            var kit = new UiKit(AssetDatabase.LoadAssetAtPath<Font>(FontPath), AssetDatabase.LoadAssetAtPath<Sprite>(EmblemPath));
            var sample = UiKit.Rect(root.Screens, "Component showcase");
            UiKit.Stretch(sample);
            Sample = sample.gameObject;
            Sample.SetActive(false);

            var left = kit.Panel(sample, PresentationTheme.Accent);
            Anchor((RectTransform)left.parent, .05f, .29f, .445f, .91f);
            UiKit.Stack(left, 12);
            Wordmark = kit.Brand(left).GetComponentInChildren<Text>(true);
            kit.Text(left, "공통 요소 미리보기", PresentationTheme.TitleSize, PresentationTheme.PrimaryText,
                style: FontStyle.Bold);
            kit.Text(left, "방과 경기 화면에 쓰일 조작과 안내를 확인합니다.", PresentationTheme.CaptionSize,
                PresentationTheme.SecondaryText);

            kit.Text(left, "닉네임", PresentationTheme.CaptionSize, PresentationTheme.PrimaryText);
            Nickname = kit.Field(left, "닉네임을 입력하세요", 16);
            Nickname.SetTextWithoutNotify("산길 탐험가");
            kit.Text(left, "방 코드", PresentationTheme.CaptionSize, PresentationTheme.PrimaryText);
            RoomCode = kit.Field(left, "방 코드를 입력하세요", 24);

            var buttons = UiKit.Rect(left, "Actions");
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandHeight = false;
            DialogButton = kit.Button(buttons, "모달 열기", OpenDialog);
            FadeButton = kit.Button(buttons, "화면 전환", () => root.FadeTo(Alternate));
            DisabledButton = kit.Button(left, "대기 중", null, false);

            var right = kit.Panel(sample, PresentationTheme.Stone);
            Anchor((RectTransform)right.parent, .554f, .29f, .95f, .91f);
            UiKit.Stack(right, 14);
            Heading(kit, right, "상태 안내");
            kit.Status(right, "활성화", "체크포인트가 활성화되었습니다.", PresentationTheme.Success);
            kit.Status(right, "협력", "동료와 함께 봉인을 해제하세요.", PresentationTheme.Cooperation);
            kit.Status(right, "경쟁", "상대가 보석을 획득했습니다.", PresentationTheme.Competition);
            LongError = kit.Status(right, "연결 오류",
                "동료가 만든 방에 입장할 수 없습니다. 방 코드가 정확한지 확인하고, 방이 닫혔다면 동료에게 새 코드를 받아 다시 입장해 주세요. 문제가 계속되면 잠시 기다린 뒤 다시 시도해 주세요.",
                PresentationTheme.Warning);

            var prompt = kit.Panel(sample, PresentationTheme.Wood);
            Anchor((RectTransform)prompt.parent, .05f, .075f, .29f, .20f);
            UiKit.Stack(prompt, 8);
            kit.Text(prompt, "상호작용", PresentationTheme.CaptionSize, PresentationTheme.SecondaryText);
            kit.KeyPrompt(prompt, "E", "목재 들기");

            var loading = kit.Panel(sample, PresentationTheme.Stone);
            Anchor((RectTransform)loading.parent, .386f, .075f, .614f, .20f);
            UiKit.Stack(loading, 6);
            kit.Loading(loading, "경기 공간을 불러오는 중", .42f);

            var stamina = kit.Panel(sample, PresentationTheme.Wood);
            Anchor((RectTransform)stamina.parent, .755f, .075f, .95f, .20f);
            UiKit.Stack(stamina, 8);
            kit.Text(stamina, "스태미나", PresentationTheme.CaptionSize, PresentationTheme.PrimaryText);
            Stamina = kit.Progress(stamina, .68f, PresentationTheme.Accent);

            var alternate = kit.Panel(root.Screens, PresentationTheme.Accent);
            Anchor((RectTransform)alternate.parent, .28f, .35f, .72f, .65f);
            Alternate = alternate.parent.gameObject;
            Alternate.SetActive(false);
            UiKit.Stack(alternate, 14);
            kit.Brand(alternate);
            Heading(kit, alternate, "화면 전환 미리보기");
            kit.Button(alternate, "공통 요소로 돌아가기", () => root.FadeTo(Sample, focus: FadeButton.gameObject));

            var dialog = kit.Modal(root.Modals);
            Dialog = dialog.parent.parent.gameObject;
            Dialog.SetActive(false);
            UiKit.Stack(dialog, 14);
            Heading(kit, dialog, "방에서 나가시겠습니까?");
            kit.Text(dialog, "다시 참가하려면 동료에게 방 코드를 받아 입력하세요.", PresentationTheme.BodySize,
                PresentationTheme.PrimaryText);
            CloseButton = kit.Button(dialog, "닫기", root.CloseModal);
        }

        internal void Open() => Root.ShowScreen(Sample);
        internal void OpenDialog() => Root.ShowModal(Dialog, CloseButton.gameObject);

        void Dispose()
        {
            if (Dialog.activeSelf) Root.CloseModal();
            Root.ShowScreen(null);
            Object.Destroy(Sample);
            Object.Destroy(Alternate);
            Object.Destroy(Dialog);
        }

        static void Heading(UiKit kit, Transform parent, string text) =>
            kit.Text(parent, text, PresentationTheme.HeadingSize, PresentationTheme.PrimaryText, style: FontStyle.Bold);

        static void Anchor(RectTransform panel, float left, float bottom, float right, float top)
        {
            panel.anchorMin = new Vector2(left, bottom);
            panel.anchorMax = new Vector2(right, top);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }
    }
}
