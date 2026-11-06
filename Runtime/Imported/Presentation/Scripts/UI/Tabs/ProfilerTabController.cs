//#define Debug_CONSOLE_DEBUG

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine.UI;
    using UnityEngine;
    using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

    public class ProfilerTabController : ComponentBehaviour
    {
        private bool _isDirty;
        private static readonly string[] HudModes={"Off","FpsOnly","Compact","Detailed"};
        private readonly Image[] modeImages=new Image[4];
        private string lastHudMode;
        private void BuildHudControls(){
            var font=PinToggle.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var row=new GameObject("Dreamy HUD controls",typeof(RectTransform),typeof(LayoutElement),typeof(HorizontalLayoutGroup));
            row.transform.SetParent(PinToggle.transform.parent,false);
            var size=row.GetComponent<LayoutElement>();size.preferredWidth=222;size.preferredHeight=32;
            var group=row.GetComponent<HorizontalLayoutGroup>();group.spacing=3;group.childForceExpandHeight=true;group.childForceExpandWidth=true;
            var labels=new[]{"HUD Off","FPS","Compact","Detail"};
            for(var i=0;i<HudModes.Length;i++){
                var mode=HudModes[i];var button=new GameObject("HUD "+mode,typeof(RectTransform),typeof(Image),typeof(Button));button.transform.SetParent(row.transform,false);
                modeImages[i]=button.GetComponent<Image>();button.GetComponent<Button>().onClick.AddListener(()=>{PreferenceBridge.Set("hud.mode",mode);RefreshHudControls();});
                var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(button.transform,false);
                var rect=(RectTransform)label.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                var text=label.GetComponent<Text>();text.text=labels[i];text.font=font;text.fontSize=11;text.alignment=TextAnchor.MiddleCenter;text.color=Theme.Ivory;text.raycastTarget=false;
            }
            PinToggle.gameObject.SetActive(false);RefreshHudControls();
        }
        private void RefreshHudControls(){
            lastHudMode=PreferenceBridge.Get("hud.mode","Off");
            for(var i=0;i<modeImages.Length;i++)if(modeImages[i]!=null)modeImages[i].color=lastHudMode==HudModes[i]?Theme.Accent:Theme.Control;
        }


        [RequiredField] public Toggle PinToggle;

        protected override void Start()
        {
            base.Start();

            BuildHudControls();
            Refresh();
        }

        private void PinToggleValueChanged(bool isOn)
        {
            DreamyDebug.Instance.IsProfilerDocked = isOn;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _isDirty = true;
        }

        protected override void Update()
        {
            base.Update();

            if(lastHudMode!=PreferenceBridge.Get("hud.mode","Off"))RefreshHudControls();
            if (_isDirty)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            PinToggle.isOn = DreamyDebug.Instance.IsProfilerDocked;
            _isDirty = false;
        }
    }
}
