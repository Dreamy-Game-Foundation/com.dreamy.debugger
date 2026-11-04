using System;
using System.Threading;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Reporting;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Debugger.Sample.Editor
{
    /// <summary>Editor-only acceptance playground; not the shipping runtime panel.</summary>
    public sealed class DebuggerAcceptanceWindow : EditorWindow
    {
        private int tapCount = 5;
        private string configuredPin = "0000", enteredPin = "", status = "Tap the test trigger five times, then enter 0000.";
        private AccessSession access;
        private CheatRegistry registry;
        private IDisposable lease;
        private CancellationTokenSource lifetime;
        private FakeReportTransport fake;
        private ReportDispatcher dispatcher;
        private FakeReportOutcome outcome;
        private bool sending;
        private Vector2 scroll;
        [MenuItem("Window/Dreamy Debugger/Acceptance Playground")]
        public static void Open() { GetWindow<DebuggerAcceptanceWindow>("Dreamy Debugger"); }
        private void OnEnable() { Apply(); }
        private void OnDisable() { Release(); }
        private void Release()
        {
            access?.Relock(); lease?.Dispose(); lease = null;
            if (lifetime != null) { lifetime.Cancel(); lifetime.Dispose(); lifetime = null; }
            sending = false;
        }
        private void Apply()
        {
            try
            {
                // Validate first: invalid edits do not tear down a working session.
                var policy = new AccessPolicy(tapCount,configuredPin);
                Release(); lifetime = new CancellationTokenSource();
                access = new AccessSession(policy, () => EditorApplication.timeSinceStartup);
                registry = new CheatRegistry(() => access.IsUnlocked && lifetime != null);
                lease = registry.Register(new ProjectCheats().CreateOptions());
                fake = new FakeReportTransport(); dispatcher = new ReportDispatcher(fake);
                enteredPin = ""; status = "Configuration applied. Use test trigger to unlock.";
            }
            catch (Exception error) { status = error.Message; }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("EDITOR ACCEPTANCE SAMPLE • edits affect demo state only • fake reports send no network traffic",MessageType.Info);
            tapCount = EditorGUILayout.IntSlider("Required taps",tapCount,1,20);
            configuredPin = EditorGUILayout.PasswordField("Configured PIN",configuredPin);
            if (GUILayout.Button("Apply access configuration / reset demo")) Apply();
            if (access == null || registry == null) return;
            if (GUILayout.Button("Test trigger tap")) { access.Tap(); Repaint(); }
            if (access.IsPromptOpen)
            {
                enteredPin = EditorGUILayout.PasswordField("Unlock PIN",enteredPin);
                if (GUILayout.Button("Unlock")) { status = access.SubmitPin(enteredPin).ToString(); enteredPin = ""; }
            }
            EditorGUILayout.LabelField("Access",access.IsUnlocked ? "Unlocked" : "Locked");
            if (GUILayout.Button("Relock")) access.Relock();
            EditorGUILayout.HelpBox(status,MessageType.None);
            using (new EditorGUI.DisabledScope(!access.IsUnlocked))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                string category = null;
                foreach (var option in registry.Snapshot())
                {
                    if (category != option.Category) { category = option.Category; EditorGUILayout.Space(); EditorGUILayout.LabelField(category,EditorStyles.boldLabel); }
                    try { DrawOption(option); } catch (Exception error) { status = error.Message; }
                }
                EditorGUILayout.EndScrollView();
                outcome = (FakeReportOutcome)EditorGUILayout.EnumPopup("Fake report outcome",outcome);
                using (new EditorGUI.DisabledScope(sending))
                    if (GUILayout.Button("Send [FAKE] report (local simulation)")) SendFake();
            }
        }
        private void DrawOption(CheatOption option)
        {
            if (option.IsAction)
            {
                if (GUILayout.Button(option.DisplayName))
                {
                    var confirmed = !option.RequiresConfirmation || EditorUtility.DisplayDialog("Confirm demo action",option.DisplayName,"Run","Cancel");
                    if (confirmed) registry.Execute(option.Id,option.RequiresConfirmation);
                }
                return;
            }
            if (!access.IsUnlocked) { EditorGUILayout.LabelField(option.DisplayName,"Locked"); return; }
            var before = registry.Read(option.Id);
            using (new EditorGUI.DisabledScope(option.IsReadOnly))
            {
                EditorGUI.BeginChangeCheck();
                object after = before;
                var type = option.ValueType;
                if (type == typeof(bool)) after = EditorGUILayout.Toggle(option.DisplayName,(bool)before);
                else if (type == typeof(int)) after = EditorGUILayout.IntField(option.DisplayName,(int)before);
                else if (type == typeof(long)) after = EditorGUILayout.LongField(option.DisplayName,(long)before);
                else if (type == typeof(float)) after = EditorGUILayout.FloatField(option.DisplayName,(float)before);
                else if (type == typeof(double)) after = EditorGUILayout.DoubleField(option.DisplayName,(double)before);
                else if (type.IsEnum) after = EditorGUILayout.EnumPopup(option.DisplayName,(Enum)before);
                else after = EditorGUILayout.TextField(option.DisplayName,(string)before ?? "");
                if (EditorGUI.EndChangeCheck() && !option.IsReadOnly) registry.Set(option.Id,after);
            }
        }
        private async void SendFake()
        {
            if (sending || lifetime == null || !access.IsUnlocked) return;
            var owner = lifetime;
            sending = true; fake.Outcome = outcome;
            try
            {
                var result = await dispatcher.SendAsync(new DiagnosticReport("[FAKE] QA report from acceptance playground","editor-demo",true),owner.Token);
                if (this != null && ReferenceEquals(owner,lifetime)) status = "[FAKE] " + result + " • no network sent";
            }
            catch (Exception error) { if (this != null && ReferenceEquals(owner,lifetime)) status = error.Message; }
            finally { if (this != null && ReferenceEquals(owner,lifetime)) { sending = false; Repaint(); } }
        }
    }
}
