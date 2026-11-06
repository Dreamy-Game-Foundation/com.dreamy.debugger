namespace Dreamy.Debugger.Presentation.Services.Implementation
{
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UnityEngine;

    [Service(typeof (IDebugCameraService))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Dreamy.Debugger.Presentation.Services.Implementation", sourceAssembly: null, sourceClassName: "DebugCameraServiceImpl")]
    public class DreamyCameraService : IDebugCameraService
    {
        private Camera _debugCamera;

        public DreamyCameraService()
        {
            if (Settings.Instance.UseDebugCamera)
            {
                _debugCamera = new GameObject("DiagnosticsCamera").AddComponent<Camera>();

                _debugCamera.cullingMask = 1 << Settings.Instance.DebugLayer;
                _debugCamera.depth = Settings.Instance.DebugCameraDepth;

                _debugCamera.clearFlags = CameraClearFlags.Depth;

                _debugCamera.transform.SetParent(Hierarchy.Get("DreamyDebugger"));
            }
        }

        public Camera Camera
        {
            get { return _debugCamera; }
        }
    }
}
