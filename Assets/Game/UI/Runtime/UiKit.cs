using System;
using UnityEngine;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    // All metrics are in reference-canvas units. Callers place the returned controls with anchors or layout groups.
    public sealed class UiKit
    {
        readonly Font font;
        readonly Sprite emblem;

        public UiKit(Font font, Sprite emblem)
        {
            this.font = font ? font : throw new ArgumentNullException(nameof(font));
            this.emblem = emblem ? emblem : throw new ArgumentNullException(nameof(emblem));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static Image Surface(Transform parent, string name, Color color, bool hit = false)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }

        public static VerticalLayoutGroup Stack(Transform parent, float spacing = 12, int padding = 0)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void Measure(RectTransform rect, float height, float width = -1)
        {
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = height;
            size.preferredWidth = width;
        }

        public Text Text(Transform parent, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            var text = Rect(parent, "Text").gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.text = content;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public RectTransform Panel(Transform parent, Color edge)
        {
            var panel = Surface(parent, "Panel", PresentationTheme.Background, true).rectTransform;
            var line = Surface(panel, "Edge", edge).rectTransform;
            Stretch(line);
            line.anchorMin = new Vector2(0, 1);
            line.offsetMin = new Vector2(0, -3);
            line.SetAsLastSibling();
            var content = Rect(panel, "Content");
            Stretch(content, 24, 20, 24, 20);
            return content;
        }

        public RectTransform Brand(Transform parent)
        {
            var lockup = Rect(parent, "TWIN EDGE");
            Measure(lockup, 108);
            var mark = Surface(lockup, "Crossed blades and gem", Color.white);
            mark.sprite = emblem;
            mark.preserveAspect = true;
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0, .5f);
            mark.rectTransform.pivot = new Vector2(0, .5f);
            mark.rectTransform.sizeDelta = new Vector2(88, 88);
            var title = Text(lockup, "TWIN EDGE", 64, PresentationTheme.PrimaryText, style: FontStyle.Bold);
            Stretch(title.rectTransform, 106);
            return lockup;
        }

        public UiButton Button(Transform parent, string caption, Action pressed, bool interactable = true)
        {
            var face = Surface(parent, caption, Color.white, true);
            Measure(face.rectTransform, 56);
            var label = Text(face.transform, caption, PresentationTheme.BodySize, PresentationTheme.PrimaryText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 16, 0, 16);
            var button = face.gameObject.AddComponent<UiButton>();
            button.targetGraphic = face;
            button.colors = PresentationTheme.Controls(false);
            button.SetStyle(Marker(face.transform), label);
            button.interactable = interactable;
            if (pressed != null) button.onClick.AddListener(() => pressed());
            return button;
        }

        public UiTextField Field(Transform parent, string placeholder, int characterLimit)
        {
            var face = Surface(parent, placeholder, Color.white, true);
            Measure(face.rectTransform, 58);
            var hint = Text(face.transform, placeholder, PresentationTheme.BodySize, PresentationTheme.SecondaryText);
            var value = Text(face.transform, "", PresentationTheme.BodySize, PresentationTheme.PrimaryText);
            foreach (var text in new[] { hint, value })
            {
                Stretch(text.rectTransform, 18, 3, 18, 3);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            var field = face.gameObject.AddComponent<UiTextField>();
            field.targetGraphic = face;
            field.colors = PresentationTheme.Controls(true);
            field.textComponent = value;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = PresentationTheme.PrimaryText;
            field.selectionColor = new Color(.77f, .64f, .41f, .42f);
            field.SetMarker(Marker(face.transform));
            return field;
        }

        static Image Marker(Transform parent)
        {
            var image = Surface(parent, "State marker", PresentationTheme.Accent);
            image.rectTransform.anchorMin = new Vector2(0, 0);
            image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.pivot = new Vector2(0, .5f);
            return image;
        }

        public Text Status(Transform parent, string label, string message, Color stripe)
        {
            var outer = Surface(parent, label, new Color32(51, 47, 41, 255)).rectTransform;
            var layout = Stack(outer, 3);
            layout.padding = new RectOffset(20, 20, 10, 12);
            var line = Marker(outer);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.sizeDelta = new Vector2(4, 0);
            line.color = stripe;
            Text(outer, label, PresentationTheme.CaptionSize, PresentationTheme.PrimaryText, style: FontStyle.Bold);
            return Text(outer, message, PresentationTheme.CaptionSize, PresentationTheme.PrimaryText);
        }

        public RectTransform Modal(Transform parent)
        {
            var backdrop = Surface(parent, "Modal dimmer", new Color32(17, 16, 14, 210), true).rectTransform;
            Stretch(backdrop);
            var content = Panel(backdrop, PresentationTheme.Accent);
            var frame = (RectTransform)content.parent;
            frame.anchorMin = new Vector2(.29f, .5f);
            frame.anchorMax = new Vector2(.71f, .5f);
            frame.sizeDelta = new Vector2(0, 300);
            return content;
        }

        public Image Progress(Transform parent, float initial, Color color, float height = 14)
        {
            var track = Surface(parent, "Progress track", PresentationTheme.Stone);
            Measure(track.rectTransform, height);
            var inset = Surface(track.transform, "Groove", PresentationTheme.Background);
            Stretch(inset.rectTransform, 2, 2, 2, 2);
            var fill = Surface(inset.transform, "Progress fill", color);
            Stretch(fill.rectTransform);
            SetProgress(fill, initial);
            return fill;
        }

        public static void SetProgress(Image fill, float value)
        {
            var anchor = new Vector2(Mathf.Clamp01(value), 1);
            if (fill.rectTransform.anchorMax != anchor) fill.rectTransform.anchorMax = anchor;
        }

        public void KeyPrompt(Transform parent, string key, string action)
        {
            var row = Rect(parent, "Interaction prompt");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            Measure(row, 46);
            var cap = Surface(row, "Keycap", PresentationTheme.Parchment);
            Measure(cap.rectTransform, 46, 58);
            var glyph = Text(cap.transform, key, PresentationTheme.BodySize, PresentationTheme.Background, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(glyph.rectTransform, 4, 2, 4, 2);
            Text(row, action, PresentationTheme.BodySize, PresentationTheme.PrimaryText);
        }

        public Image Loading(Transform parent, string message, float progress)
        {
            var view = Rect(parent, "Loading indicator");
            Stack(view, 7);
            Text(view, message, PresentationTheme.CaptionSize, PresentationTheme.PrimaryText);
            return Progress(view, progress, PresentationTheme.Accent, 10);
        }
    }
}
