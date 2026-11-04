using UnityEngine;
using UnityEngine.UI;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Foundation.Service;
namespace Dreamy.Debugger {
    public sealed class PerformanceHud : MonoBehaviour {
        [SerializeField]private Text label;
        [SerializeField]private Graphic graph;
        private HudMode mode;private double nextText,nextMemory;private long memory;
        public HudMode Mode=>mode;
        public void SetMode(HudMode value){mode=value;gameObject.SetActive(value!=HudMode.Off);if(graph!=null)graph.gameObject.SetActive(value==HudMode.Detailed);}
        private void OnEnable(){nextText=nextMemory=0;}
        private void Update(){
            var canvas=GetComponent<Canvas>();
            if(DreamyDebug.IsInitialized && DreamyDebug.Instance.IsDebugPanelVisible){if(canvas!=null)canvas.enabled=false;return;}
            if(canvas!=null)canvas.enabled=true;
            var time=Time.realtimeSinceStartupAsDouble;if(time<nextText)return;nextText=time+.25;
            if(time>=nextMemory){memory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();nextMemory=time+1;}
            var service=ServiceManager.GetService<IProfilerService>();var seconds=service.AverageFrameTime;
            label.text=mode==HudMode.FpsOnly?$"{(seconds>0?1/seconds:0):0} FPS":$"{(seconds>0?1/seconds:0):0} FPS  •  {seconds*1000:0.0} ms\n{memory/1048576f:0} MB allocated";
            // The imported graph shares the same profiler service; it does not create a second collector.
        }
    }
}
