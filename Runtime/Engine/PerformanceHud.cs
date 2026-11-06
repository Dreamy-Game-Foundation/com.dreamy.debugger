using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Foundation.Service;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger
{
    public sealed class PerformanceHud : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Graphic graph;
        [SerializeField] private RectTransform card;
        [SerializeField] private Text summary, memoryLabel, audioLabel;
        [SerializeField] private HudHistoryGraphic memoryGraph, audioGraph;
        [SerializeField] private Text milliseconds, compactMemory, memoryValues, audioValue;
        [SerializeField] private Text averageValue, onePercentValue, pointOnePercentValue;
        private DebuggerConfiguration policy;
        private HudPosition position;
        private HudMode mode;
        private double nextText, nextMemory, nextAudio, nextGraph;
        private long allocated, reserved, mono;
        private Canvas canvas;
        private IProfilerService profiler;
        private int serviceGeneration=-1;
        private GraphyHudGraph fpsGraph;
        private readonly float[] audioSamples = new float[256];
        private bool dragging, hasFreeOffset;
        private Vector2 dragPointer, dragOrigin, freeOffset, parentSize;
        public HudPosition Position => position;
        public HudMode Mode => mode;
        public bool DragEnabled => policy == null || policy.HudDragEnabled;
        public bool IsDragging => dragging;

        private void Awake() { canvas = GetComponent<Canvas>(); }
        public void Configure(DebuggerConfiguration value)
        {
            policy = value;
            if (graph != null) fpsGraph = graph.GetComponent<GraphyHudGraph>();
            if (fpsGraph != null && fpsGraph.IsInitialized) fpsGraph.Configure(value);
            Layout();
            SetPosition(value.InitialHudPosition);
        }

        public void SetPosition(HudPosition value)
        {
            position = value;
            if (card == null) return;
            dragging = false;
            var right = value == HudPosition.TopRight || value == HudPosition.BottomRight;
            var top = value == HudPosition.TopRight || value == HudPosition.TopLeft;
            var anchor = value == HudPosition.Free ? new Vector2(.5f, .5f) : new Vector2(right ? 1 : 0, top ? 1 : 0);
            card.anchorMin = card.anchorMax = card.pivot = anchor;
            var offset = policy != null ? policy.HudOffset : new Vector2(12, 12);
            card.anchoredPosition = value == HudPosition.Free ? (hasFreeOffset ? freeOffset : offset) :
                new Vector2(right ? -offset.x : offset.x, top ? -offset.y : offset.y);
            Fit();
        }

        public void ResetPosition()
        {
            hasFreeOffset = false;
            SetPosition(policy != null ? policy.InitialHudPosition : HudPosition.TopRight);
        }

        public void SetMode(HudMode value)
        {
            mode = value;
            gameObject.SetActive(value != HudMode.Off);
            Layout();
            Fit();
            nextText = nextMemory = nextAudio = nextGraph = 0;
        }

        private static void Place(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(left, -bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private void Layout()
        {
            if (card == null) return;
            var detailed = mode == HudMode.Detailed;
            var showGraph = detailed && fpsGraph != null && fpsGraph.IsAvailable;
            var graphHeight = showGraph ? 58 : 0;
            var detailBottom = 144 + graphHeight;
            var ram = detailed && (policy == null || policy.HudMemory);
            var audio = detailed && policy != null && policy.HudAudio;
            if (graph != null) { graph.gameObject.SetActive(showGraph); Place(graph.rectTransform,12,136,12,194); }
            if (summary != null) { summary.gameObject.SetActive(detailed); Place(summary.rectTransform,12,114,12,134); }
            var summaries=new[]{averageValue,onePercentValue,pointOnePercentValue};
            for(var i=0;i<summaries.Length;i++)if(summaries[i]!=null){var t=summaries[i];t.gameObject.SetActive(detailed);t.rectTransform.anchorMin=new Vector2(i/3f,1);t.rectTransform.anchorMax=new Vector2((i+1)/3f,1);t.rectTransform.offsetMin=new Vector2(12,-112);t.rectTransform.offsetMax=new Vector2(-8,-74);}
            if (memoryLabel != null) { memoryLabel.gameObject.SetActive(ram); Place(memoryLabel.rectTransform,12,detailBottom,150,detailBottom+60); }
            if(memoryValues!=null){memoryValues.gameObject.SetActive(ram);Place(memoryValues.rectTransform,140,detailBottom,12,detailBottom+60);}
            if (memoryGraph != null) { memoryGraph.gameObject.SetActive(ram); Place(memoryGraph.rectTransform,12,detailBottom+64,12,detailBottom+90); }
            var audioTop=ram?detailBottom+102:detailBottom;
            if (audioLabel != null) { audioLabel.gameObject.SetActive(audio); Place(audioLabel.rectTransform,12,audioTop,120,audioTop+24); }
            if(audioValue!=null){audioValue.gameObject.SetActive(audio);Place(audioValue.rectTransform,160,audioTop,12,audioTop+24);}
            if (audioGraph != null) { audioGraph.gameObject.SetActive(audio); Place(audioGraph.rectTransform,12,audioTop+30,12,audioTop+54); }
            if (label != null) Place(label.rectTransform,12,10,156,58);
            if(milliseconds!=null)Place(milliseconds.rectTransform,136,18,70,50);
            if(compactMemory!=null){compactMemory.gameObject.SetActive(mode==HudMode.Compact && (policy==null || policy.HudMemory));Place(compactMemory.rectTransform,12,68,12,90);}
            var height=detailed?(audio?audioTop+66:ram?detailBottom+102:detailBottom):mode==HudMode.Compact && (policy==null || policy.HudMemory)?96:68;
            card.sizeDelta=new Vector2(mode==HudMode.FpsOnly?284:mode==HudMode.Compact?300:320,height);
        }

        private void Fit()
        {
            var parent = card != null ? card.parent as RectTransform : null;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            parentSize = parent.rect.size;
            var desired = policy != null ? Mathf.Clamp(policy.HudScale, .5f, 2) : 1;
            // The Graphy canvas reference can shrink a 16pt label below 7px on small screens.
            // Keep its authored typography readable; the existing safe-area fit still caps the card.
            var density = Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 160f, 1, 3) : 1;
            if (canvas != null && canvas.scaleFactor > 0) desired *= Mathf.Max(1, density * .8f / canvas.scaleFactor);
            var scale = Mathf.Min(desired, (parent.rect.width - 16) / card.rect.width, (parent.rect.height - 16) / card.rect.height);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
            if (position == HudPosition.Free) ClampFreePosition();
        }

        private void ClampFreePosition()
        {
            var parent = (RectTransform)card.parent;
            var half = Vector2.Scale(card.rect.size, new Vector2(card.localScale.x, card.localScale.y)) * .5f;
            var limit = Vector2.Max(Vector2.zero, parent.rect.size * .5f - half);
            var p = card.anchoredPosition;
            card.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x), Mathf.Clamp(p.y, -limit.y, limit.y));
            freeOffset = card.anchoredPosition; hasFreeOffset = true;
        }

        public void BeginDrag(PointerEventData data)
        {
            if (!DragEnabled || card == null || mode == HudMode.Off) return;
            var parent = (RectTransform)card.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out dragPointer)) return;
            var worldCenter = card.TransformPoint(card.rect.center);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.position = worldCenter;
            position = HudPosition.Free;
            dragOrigin = card.anchoredPosition;
            dragging = true;
        }
        public void Drag(PointerEventData data)
        {
            if (!dragging || !DragEnabled) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)card.parent, data.position, data.pressEventCamera, out var point))
            {
                card.anchoredPosition = dragOrigin + point - dragPointer;
                ClampFreePosition();
            }
        }
        public void EndDrag() { dragging = false; }
        private void OnEnable() { nextText = nextMemory = nextAudio = nextGraph = 0; }
        private void OnDisable() { EndDrag(); }

        private void Update()
        {
            var presentation = DreamyDebug.IsInitialized ? DreamyDebug.Instance : null;
            var hidden = presentation != null && presentation.IsDebugPanelVisible;
            if (canvas != null) canvas.enabled = !hidden;
            if (hidden) { EndDrag(); return; }
            if (card != null && ((RectTransform)card.parent).rect.size != parentSize) Fit();
            if(serviceGeneration!=ServiceManager.Generation){serviceGeneration=ServiceManager.Generation;profiler=null;}
            if (profiler == null) profiler = ServiceManager.GetService<IProfilerService>();
            if (profiler == null || label == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            var seconds = profiler.LastFrameTime;
            var fps = seconds > 0 ? 1 / seconds : 0;
            var average = profiler.AverageFrameTime > 0 ? 1 / profiler.AverageFrameTime : 0;
            if (mode == HudMode.Detailed && fpsGraph != null && now >= nextGraph)
            { nextGraph = now + .1; fpsGraph.Push((float)fps, (float)average, policy); }
            if (mode == HudMode.Detailed && policy != null && policy.HudAudio && now >= nextAudio)
            {
                nextAudio = now + .1;
                AudioListener.GetOutputData(audioSamples, 0);
                var sum = 0f; for (var i = 0; i < audioSamples.Length; i++) sum += audioSamples[i] * audioSamples[i];
                var db = Mathf.Max(-80, 20 * Mathf.Log10(Mathf.Max(.0001f, Mathf.Sqrt(sum / audioSamples.Length))));
                if(audioLabel!=null)audioLabel.text="Audio RMS";
                if(audioValue!=null)audioValue.text=$"{db:0.0} dB";
                if (audioGraph != null) audioGraph.Push(new Vector3((db + 80) / 80, 0, 0));
            }
            if (now < nextText) return; nextText = now + .25;
            if (now >= nextMemory && ((mode == HudMode.Compact || mode == HudMode.Detailed) && (policy == null || policy.HudMemory)))
            {
                nextMemory = now + 1;
                allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                if(memoryLabel!=null)memoryLabel.text="<color=#E7B65A>Allocated</color>\n<color=#C878D8>Reserved</color>\nMono";
                if(memoryValues!=null)memoryValues.text=$"{allocated/1048576f:0.0} MB\n{reserved/1048576f:0.0} MB\n{mono/1048576f:0.0} MB";
                if (mode == HudMode.Detailed && memoryGraph != null) memoryGraph.Push(new Vector3(reserved, allocated, mono) / 1048576f);
            }
            var ink = ColorUtility.ToHtmlStringRGB(policy == null ? Theme.Ivory : fps >= policy.GoodFps ? policy.FpsGood : fps >= policy.CautionFps ? policy.FpsCaution : policy.FpsCritical);
            label.text = $"<color=#{ink}><size=35>{fps:0}</size></color> <size=18>fps</size>";
            if(milliseconds!=null)milliseconds.text=seconds>0?$"{seconds*1000:0.0} ms":"— ms";
            if(compactMemory!=null && mode==HudMode.Compact)compactMemory.text=$"RAM   {allocated/1048576f:0.0} MB allocated";
            if (mode != HudMode.Detailed || summary == null) return;
            if (profiler is Dreamy.Debugger.Presentation.Profiler.ProfilerServiceImpl sampler)
            {
                var w = sampler.Window;
                summary.text=$"{w.Count}/1024 frame window";
                var warm=w.Count<20;
                if(averageValue!=null)averageValue.text=warm?"avg\n—":$"avg\n{average:0.0}";
                if(onePercentValue!=null)onePercentValue.text=warm?"1%\n—":$"1%\n{w.OnePercentFPS}";
                if(pointOnePercentValue!=null)pointOnePercentValue.text=warm?".1%\n—":$".1%\n{w.Zero1PercentFps}";
            }
        }
    }
}
