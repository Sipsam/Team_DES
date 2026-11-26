using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed class UiTextField : InputField
    {
        Image marker;

        internal void SetMarker(Image value)
        {
            marker = value;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            UiButton.SetMarkerState(marker, (int)state);
        }
    }
}
