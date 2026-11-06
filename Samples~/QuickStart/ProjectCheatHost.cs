using System;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Options;
using UnityEngine;

namespace Dreamy.Debugger.Sample
{
    /// <summary>Explicit opt-in demo host. No input interception or automatic startup.</summary>
    public sealed class ProjectCheatHost : MonoBehaviour
    {
        [SerializeField, Range(1,20)] private int requiredTaps = 5;
        [SerializeField, Tooltip("Demo authoring value only. Do not put shared production credentials in sample assets.")]
        private string pin = "0000";
        private IDisposable optionsLease;
        public AccessSession Access { get; private set; }
        public CheatRegistry Registry { get; private set; }
        public ProjectCheats Cheats { get; private set; }
        private void OnEnable()
        {
            var session = new AccessSession(new AccessPolicy(requiredTaps,pin), () => Time.realtimeSinceStartupAsDouble);
            Access = session;
            var runtime=DebuggerRuntime.Instance;
            if(runtime!=null)Access=runtime.Access;
            Registry = new CheatRegistry(() => session.IsUnlocked && this != null && isActiveAndEnabled && Debug.isDebugBuild);
            if(runtime!=null)Registry=runtime.Cheats;
            Cheats = new ProjectCheats();
            optionsLease = runtime != null
                ? runtime.RegisterCheats(Cheats, "sample")
                : Registry.RegisterContainer(Cheats, "sample");
        }
        private void OnDisable()
        {
            optionsLease?.Dispose(); optionsLease = null; Access?.Relock();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Access?.Relock(); }
        private void OnApplicationPause(bool paused) { if (paused) Access?.Relock(); }
        // Host UI must call this only for taps inside its configured trigger region.
        public bool ForwardTap(int pointerId = 0) => isActiveAndEnabled && Access != null && Access.Tap(pointerId);
        public UnlockResult SubmitPin(string value) => Access == null || !isActiveAndEnabled
            ? UnlockResult.GestureRequired : Access.SubmitPin(value);
    }
}
