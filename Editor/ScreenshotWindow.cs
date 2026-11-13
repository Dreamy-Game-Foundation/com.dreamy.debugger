using System;
using System.Linq;
using Dreamy.Debugger.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    public sealed class ScreenshotWindow : EditorWindow
    {
        private Vector2 scroll;
        private static readonly int[] Delays = { 0, 3, 5 }, Timers = { 0, 15, 30, 60 };
        [MenuItem("Window/Dreamy Debugger/Screenshot")]
        public static void Open() => GetWindow<ScreenshotWindow>("Screenshot");
        private void OnInspectorUpdate() => Repaint();
        private void OnGUI()
        {
            GUILayout.Label("Gameplay screenshot", EditorStyles.boldLabel);
            var runtime = EditorApplication.isPlaying ? DebuggerRuntime.Instance : null;
            var mode = runtime != null ? runtime.Screenshots : null;
            if (mode == null) { EditorGUILayout.HelpBox("Enter Play Mode with Dreamy Debugger enabled to capture Game View.", MessageType.Info); return; }
            if (!mode.CanUse)
            {
                EditorGUILayout.HelpBox("Unlock the debugger in Game View to use Screenshot Mode.", MessageType.Info);
                if (GUILayout.Button("Open Screenshot tab / unlock", GUILayout.Height(36))) runtime.Open(DefaultTabs.Screenshot);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Debugger is always hidden in the PNG. This window stays outside Game View and cannot appear in the image.", MessageType.None);
            mode.GameplayUi = (ScreenshotGameplayUi)EditorGUILayout.Popup("UI in captured PNG", (int)mode.GameplayUi, new[] { "Keep gameplay UI", "Hide selected UI groups", "Hide all Canvas UI" });
            var targets = UnityEngine.Object.FindObjectsByType<ScreenshotHideTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(target => target.enabled).ToArray();
            if (mode.GameplayUi == ScreenshotGameplayUi.RegisteredTargets || mode.HideGameplayUi == ScreenshotHideUi.RegisteredTargets)
            {
                if (targets.Length == 0) EditorGUILayout.HelpBox("No UI groups registered. Choose Hide all Canvas UI, or register groups in the project.", MessageType.Info);
                foreach (var target in targets) target.Selected = EditorGUILayout.ToggleLeft(target.DisplayName, target.Selected);
            }
            GUILayout.Label("Capture delay (immediate while UI is hidden)", EditorStyles.boldLabel);
            var delayIndex = GUILayout.Toolbar(Array.IndexOf(Delays, mode.DelaySeconds), new[] { "Now", "3 seconds", "5 seconds" }, GUILayout.Height(34));
            if (delayIndex >= 0) mode.DelaySeconds = Delays[delayIndex];
            using (new EditorGUI.DisabledScope(mode.IsBusy || mode.IsPreparingToHide || mode.IsDeletingScreenshots || mode.GameplayUi == ScreenshotGameplayUi.RegisteredTargets && !targets.Any(target => target.Selected)))
                if (GUILayout.Button(mode.DelaySeconds == 0 ? "Capture PNG now" : "Capture in " + mode.DelaySeconds + " seconds", GUILayout.Height(42))) mode.CaptureFromUi();
            EditorGUILayout.Space(); GUILayout.Label("Screenshot mode / external capture", EditorStyles.boldLabel);
            mode.HiddenShutterEnabled = EditorGUILayout.ToggleLeft("Screenshot mode: tap top-right to capture immediately", mode.HiddenShutterEnabled);
            mode.HideGameplayUi = (ScreenshotHideUi)EditorGUILayout.Popup("Hide UI policy", (int)mode.HideGameplayUi, new[] { "Hide all UI", "Hide selected groups" });
            var timerIndex = GUILayout.Toolbar(Array.IndexOf(Timers, mode.AutoRestoreSeconds), new[] { "Until restored", "15s", "30s", "60s" }, GUILayout.Height(34));
            if (timerIndex >= 0) mode.AutoRestoreSeconds = Timers[timerIndex];
            using (new EditorGUI.DisabledScope(mode.IsBusy || mode.IsUiHidden || mode.IsPreparingToHide || mode.HideGameplayUi == ScreenshotHideUi.RegisteredTargets && !targets.Any(target => target.Selected)))
                if (GUILayout.Button("Hide UI for external capture", GUILayout.Height(36))) mode.HideUi();
            EditorGUILayout.HelpBox(mode.RestoreInstructions + "\nExternal capture remains hidden when switching windows. App pause or scene change restores UI.", MessageType.None);
            using (new EditorGUI.DisabledScope(!mode.IsBusy && !mode.IsUiHidden && !mode.IsPreparingToHide))
                if (GUILayout.Button(mode.IsUiHidden ? "Restore UI now" + (mode.AutoRestoreSeconds > 0 ? " (" + mode.RestoreSecondsRemaining + "s remaining)" : "") : "Restore UI / cancel", GUILayout.Height(36))) mode.RestoreUi();
            EditorGUILayout.LabelField("Status", mode.Status, EditorStyles.wordWrappedLabel);
            if (mode.LastCapture != null)
            {
                EditorGUILayout.Space(); GUILayout.Label("Last screenshot • " + mode.LastCapture.Width + " × " + mode.LastCapture.Height, EditorStyles.boldLabel);
                if (mode.Preview != null)
                {
                    var rect = GUILayoutUtility.GetAspectRect((float)mode.Preview.width / mode.Preview.height, GUILayout.MaxHeight(260));
                    GUI.DrawTexture(rect, mode.Preview, ScaleMode.ScaleToFit);
                }
                if (GUILayout.Button("Open image")) EditorUtility.OpenWithDefaultApp(mode.LastCapture.Path);
                if (GUILayout.Button("Open screenshot folder")) EditorUtility.RevealInFinder(mode.LastCapture.Path);
                using (new EditorGUI.DisabledScope(mode.IsBusy)) if (GUILayout.Button("Capture again", GUILayout.Height(36))) mode.CaptureFromUi();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
