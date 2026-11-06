using System;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Options;
using UnityEngine;

namespace Dreamy.Debugger.Sample
{
    /// <summary>Imported sample auto-registers in debug builds. Values remain in memory.</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectCheatHost : MonoBehaviour
    {
        [SerializeField, Range(1,20)] private int requiredTaps = 5;
        [SerializeField, Tooltip("Demo authoring value only. Do not put shared production credentials in sample assets.")]
        private string pin = "0000";
        private IDisposable optionsLease;
        private DebuggerRuntime owner;
        private bool automatic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSample()
        {
            if (!Debug.isDebugBuild || FindObjectsByType<ProjectCheatHost>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length != 0) return;
            var root = new GameObject("Dreamy Sample Cheats");
            root.SetActive(false);
            root.AddComponent<ProjectCheatHost>().automatic = true;
            DontDestroyOnLoad(root);
            root.SetActive(true);
        }
        public AccessSession Access { get; private set; }
        public CheatRegistry Registry { get; private set; }
        public ProjectCheats Cheats { get; private set; }
        private void OnEnable()
        {
            Cheats = new ProjectCheats();
            if (!TryBindRuntime() && !automatic)
            {
                // Retained for standalone sample tests/tools; never opens a second panel.
                var session = new AccessSession(new AccessPolicy(requiredTaps, pin),
                    () => Time.realtimeSinceStartupAsDouble);
                Access = session;
                Registry = new CheatRegistry(() => session.IsUnlocked && this != null &&
                    isActiveAndEnabled && Debug.isDebugBuild);
                optionsLease = Registry.RegisterContainer(Cheats, "sample");
            }
        }

        private void Update() => TryBindRuntime();

        private bool TryBindRuntime()
        {
            var runtime = DebuggerRuntime.Instance;
            if (runtime == null || !runtime.isActiveAndEnabled || runtime.Cheats == null)
            {
                if (owner != null || !ReferenceEquals(owner, null)) Release();
                return false;
            }
            if (ReferenceEquals(owner, runtime) && ReferenceEquals(Registry, runtime.Cheats)) return true;
            Release();
            optionsLease = runtime.RegisterCheats(Cheats, "sample");
            owner = runtime;
            Access = runtime.Access;
            Registry = runtime.Cheats;
            return true;
        }

        private void OnDisable() => Release();

        private void Release()
        {
            optionsLease?.Dispose();
            optionsLease = null;
            Access?.Relock();
            owner = null;
            Access = null;
            Registry = null;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Access?.Relock(); }
        private void OnApplicationPause(bool paused) { if (paused) Access?.Relock(); }
        // Host UI must call this only for taps inside its configured trigger region.
        public bool ForwardTap(int pointerId = 0) => isActiveAndEnabled && Access != null && Access.Tap(pointerId);
        public UnlockResult SubmitPin(string value) => Access == null || !isActiveAndEnabled
            ? UnlockResult.GestureRequired : Access.SubmitPin(value);
    }
}
