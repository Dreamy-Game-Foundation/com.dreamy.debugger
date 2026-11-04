using Dreamy.Debugger.Access;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Dreamy.Debugger.Presentation.UI.Controls {
    public class MultiTapButton : UnityEngine.UI.Button {
        public int RequiredTapCount=5;
        public float ResetTime=.4f;
        public float SequenceTimeout=2.5f;
        private TapSequence sequence;
        private int configuredCount,lastFrame=-1;
        private float configuredGap,configuredTimeout;
        public override void OnPointerClick(PointerEventData eventData){
            if(!IsActive() || !IsInteractable() || eventData.button!=PointerEventData.InputButton.Left || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            if(sequence==null || configuredCount!=RequiredTapCount || configuredGap!=ResetTime || configuredTimeout!=SequenceTimeout){
                configuredCount=RequiredTapCount;configuredGap=ResetTime;configuredTimeout=SequenceTimeout;
                sequence=new TapSequence(new AccessPolicy(Mathf.Clamp(RequiredTapCount,1,20),tapGapSeconds:Mathf.Max(.05f,ResetTime),sequenceSeconds:Mathf.Max(.1f,SequenceTimeout)));
            }
            // Input System touch IDs can change between taps; one accepted pointer click per frame prevents simultaneous double counting.
            if(sequence.Tap(Time.unscaledTimeAsDouble))base.OnPointerClick(eventData);
        }
        protected override void OnDisable(){sequence?.Reset();lastFrame=-1;base.OnDisable();}
        private void OnApplicationFocus(bool focused){if(!focused){sequence?.Reset();lastFrame=-1;}}
    }
}
