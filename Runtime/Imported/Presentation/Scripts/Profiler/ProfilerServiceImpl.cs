using UnityEngine;
using Dreamy.Debugger.Foundation.Service;
using Dreamy.Debugger.Metrics;
using Dreamy.Debugger.Presentation.Services;
namespace Dreamy.Debugger.Presentation.Profiler {
    public class ProfilerServiceImpl : ServiceBase<IProfilerService>,IProfilerService {
        public static bool CollectForHud;
        private readonly FrameWindow window=new FrameWindow(120);
        private readonly CircularBuffer<ProfilerFrame> buffer=new CircularBuffer<ProfilerFrame>(120);
        public float AverageFrameTime=>(float)window.AverageSeconds;
        public float LastFrameTime{get;private set;}
        public CircularBuffer<ProfilerFrame> FrameBuffer=>buffer;
        protected override void Awake(){base.Awake();CachedTransform.SetParent(Dreamy.Debugger.Foundation.Hierarchy.Get("DreamyDebugger"));}
        protected override void Update(){base.Update();var panel=Internal.Service.Panel;
            if(!CollectForHud && !(panel.IsVisible && panel.ActiveTab==DefaultTabs.Profiler))return;
            LastFrameTime=Time.unscaledDeltaTime;window.Add(LastFrameTime);
            buffer.PushBack(new ProfilerFrame{FrameTime=LastFrameTime,OtherTime=LastFrameTime});
        }
    }
}
