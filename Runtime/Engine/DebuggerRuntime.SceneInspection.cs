using Dreamy.Debugger.SceneInspection;
using UnityEngine;
using Dreamy.Debugger.Foundation.Service;
using Dreamy.Debugger.Presentation.Services.Implementation;

namespace Dreamy.Debugger
{
    public sealed partial class DebuggerRuntime
    {
        private SceneInspectionSession sceneInspection;
        private Transform inspectionServiceRoot, inspectionShortcutRoot;
        public bool CanInspectScene => hasPresentation && runtimeConfig != null && runtimeConfig.EnableSceneInspection &&
            (runtimeConfig.Mode == DebuggerMode.Development || runtimeConfig.Mode == DebuggerMode.Qa) && Access != null && Access.IsUnlocked;
        public bool CanEditScene => CanInspectScene && runtimeConfig.AllowSceneEditing;
        internal bool SceneInspectionEnabled => runtimeConfig != null && runtimeConfig.EnableSceneInspection &&
            (runtimeConfig.Mode == DebuggerMode.Development || runtimeConfig.Mode == DebuggerMode.Qa);
        internal DebuggerConfiguration InspectionConfiguration => runtimeConfig;
        internal SceneInspectionSession GetSceneInspection()
        {
            if (!CanInspectScene) return null;
            if (sceneInspection == null)
            {
                inspectionServiceRoot = ServiceManager.HasInstance ? ServiceManager.Instance.transform : null;
                inspectionShortcutRoot = ServiceManager.HasService<KeyboardShortcutListenerService>() ? ServiceManager.GetService<KeyboardShortcutListenerService>().transform : null;
                sceneInspection = new SceneInspectionSession(gameObject.scene, () => CanInspectScene, () => CanEditScene,
                    IsInspectionExcluded, runtimeConfig.SceneRefreshSeconds, runtimeConfig.SceneScanBudgetMilliseconds);
            }
            return sceneInspection;
        }
        private bool IsInspectionExcluded(GameObject target)
        {
            var t = target.transform;
            return t.IsChildOf(transform) || presentationRoot != null && t.IsChildOf(presentationRoot.transform) ||
                Hud != null && t.IsChildOf(Hud.transform) || confirmation != null && t.IsChildOf(confirmation.transform) ||
                inspectionServiceRoot != null && t.IsChildOf(inspectionServiceRoot) || inspectionShortcutRoot != null && t.IsChildOf(inspectionShortcutRoot) ||
                Screenshots != null && (Screenshots.OverlayRoot != null && t.IsChildOf(Screenshots.OverlayRoot.transform) ||
                    Screenshots.HoldFeedbackRoot != null && t.IsChildOf(Screenshots.HoldFeedbackRoot.transform));
        }
    }
}
