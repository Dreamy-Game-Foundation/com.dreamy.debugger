using UnityEngine;
using Dreamy.Debugger.Foundation.Service;
using Dreamy.Debugger.Metrics;
using Dreamy.Debugger.Presentation.Services;
namespace Dreamy.Debugger.Presentation.Profiler {
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Dreamy.Debugger.Presentation.Profiler", sourceAssembly: null, sourceClassName: "ProfilerServiceImpl")]
    public class DreamyProfilerService : ServiceBase<IProfilerService>,IProfilerService {
        public static bool CollectForHud,CollectForRules,CollectForReports;
        public GraphyFpsWindow Window {get;}=new GraphyFpsWindow();
        public RecentFrameWindow RecentFrames {get;}=new RecentFrameWindow(.5);
        private readonly CircularBuffer<ProfilerFrame> buffer=new CircularBuffer<ProfilerFrame>(120);
        public float AverageFrameTime=>(float)Window.AverageSeconds;
        public float LastFrameTime{get;private set;}
        public double LastSampleTime { get; private set; } = double.NegativeInfinity;
        public CircularBuffer<ProfilerFrame> FrameBuffer=>buffer;
        protected override void Awake(){base.Awake();CachedTransform.SetParent(Dreamy.Debugger.Foundation.Hierarchy.Get("DreamyDebugger"));}
        protected override void Update(){base.Update();var panel=Internal.Service.Panel;
            if(!CollectForHud && !CollectForRules && !CollectForReports && !(panel!=null && panel.IsVisible && panel.ActiveTab==DefaultTabs.Profiler))return;
            LastFrameTime=Time.unscaledDeltaTime;Window.Add(LastFrameTime);
            LastSampleTime=Time.realtimeSinceStartupAsDouble;
            if(Application.isFocused)RecentFrames.Add(LastFrameTime);else RecentFrames.Clear();
            buffer.PushBack(new ProfilerFrame{FrameTime=LastFrameTime,OtherTime=LastFrameTime});
        }
    }
}
