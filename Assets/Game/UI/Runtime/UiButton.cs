using UnityEngine;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed class UiButton : Button
    {
        Image marker;
        Text caption;

        internal void SetCaption(string value)
        {
            if (caption && caption.text != value) caption.text = value;
        }

        internal void SetStyle(Image stateMarker, Text label)
        {
            marker = stateMarker;
            caption = label;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            SetMarkerState(marker, (int)state);
            if (caption)
                caption.color = state == SelectionState.Disabled ? PresentationTheme.SecondaryText : PresentationTheme.PrimaryText;
        }

        // Selectable orders these states as Normal, Highlighted, Pressed, Selected, Disabled.
        internal static void SetMarkerState(Image marker, int state)
        {
            if (!marker) return;
            marker.color = state == 4 ? PresentationTheme.Stone : PresentationTheme.Accent;
            marker.rectTransform.sizeDelta = state switch
            {
                1 => new Vector2(4, -6),
                2 => new Vector2(8, -18),
                3 => new Vector2(6, 0),
                4 => new Vector2(3, -28),
                _ => new Vector2(2, -18)
            };
        }
    }
}
