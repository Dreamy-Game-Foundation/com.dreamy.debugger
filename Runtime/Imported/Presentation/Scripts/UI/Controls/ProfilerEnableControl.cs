namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using Internal;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;
#if UNITY_5_5_OR_NEWER
    using UnityEngine.Profiling;
#endif

    public class ProfilerEnableControl : ComponentBehaviour
    {
        private bool _previousState;
        [RequiredField] public Text ButtonText;
        [RequiredField] public UnityEngine.UI.Button EnableButton;
        [RequiredField] public Text Text;

        protected override void Start()
        {
            base.Start();

            if (!Profiler.supported)
            {
                Text.text = PanelStrings.Current.Profiler_NotSupported;
                EnableButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (!Application.HasProLicense())
            {
                Text.text = PanelStrings.Current.Profiler_NoProInfo;
                EnableButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            UpdateLabels();
        }

        protected void UpdateLabels()
        {
            if (!Profiler.enabled)
            {
                Text.text = PanelStrings.Current.Profiler_EnableProfilerInfo;
                ButtonText.text = "Enable";
            }
            else
            {
                Text.text = PanelStrings.Current.Profiler_DisableProfilerInfo;
                ButtonText.text = "Disable";
            }

            _previousState = Profiler.enabled;
        }

        protected override void Update()
        {
            base.Update();

            if (Profiler.enabled != _previousState)
            {
                UpdateLabels();
            }
        }

        public void ToggleProfiler()
        {
            Debug.Log("Toggle Profiler");
            Profiler.enabled = !Profiler.enabled;
        }
    }
}
