using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;
namespace Dreamy.Debugger {
    /// <summary>Bounded, allocation-free view of samples supplied by PerformanceHud.</summary>
    public sealed class HudHistoryGraphic : MaskableGraphic {
        public enum Plot { Frames, Memory, Audio }
        public Plot Kind;
        private const int Capacity = 120;
        private readonly Vector3[] samples = new Vector3[Capacity];
        private int count, cursor;
        private readonly UIVertex[] quad = new UIVertex[4];
        public void Push(Vector3 sample) { samples[cursor]=sample;cursor=(cursor+1)%Capacity;count=Mathf.Min(count+1,Capacity);SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();if(count==0)return;
            var rect=rectTransform.rect;
            var max=Kind==Plot.Frames?Mathf.Max(60,Application.targetFrameRate)*1.2f:1f;
            for(var i=0;i<count;i++){var s=At(i);max=Mathf.Max(max,Mathf.Max(s.x,Mathf.Max(s.y,s.z))*1.1f);}
            if(Kind==Plot.Audio){
                var bars=Mathf.Min(count,32);var w=rect.width/32f;
                for(var i=0;i<bars;i++){var value=Mathf.Clamp01(At(count-bars+i).x);var x=rect.xMin+i*w;var h=value*rect.height;
                    Box(vh,new Rect(x,rect.yMin,w*.65f,h),Theme.Muted);
                    if(h>2)Box(vh,new Rect(x,rect.yMin+h-2,w*.65f,2),value>.8f?Theme.Warning:Theme.Ivory);
                }return;
            }
            var lines=Kind==Plot.Memory?3:1;
            for(var line=0;line<lines;line++){
                var ink=line==0?(Kind==Plot.Memory?Theme.MemorySeries:Theme.Ivory):line==1?Theme.Warning:Theme.Ivory;
                for(var i=1;i<count;i++){
                    var a=Point(i-1,line,rect,max);var b=Point(i,line,rect,max);
                    var fill=Kind==Plot.Frames?Theme.FpsSeries:ink;fill.a=Kind==Plot.Frames?.24f:.08f;
                    Quad(vh,new Vector2(a.x,rect.yMin),new Vector2(b.x,rect.yMin),b,a,fill);
                    var n=new Vector2(-(b-a).y,(b-a).x).normalized*.65f;
                    Quad(vh,a-n,b-n,b+n,a+n,ink);
                }
            }
            if(Kind==Plot.Frames){var target=Application.targetFrameRate>0?Application.targetFrameRate:60;var y=rect.yMin+Mathf.Clamp01(target/max)*rect.height;Box(vh,new Rect(rect.xMin,y,rect.width,1),Theme.Warning);}
        }
        private Vector3 At(int i)=>samples[(cursor-count+i+Capacity)%Capacity];
        private Vector2 Point(int i,int line,Rect r,float max)=>new Vector2(r.xMin+i*r.width/(Capacity-1),r.yMin+Mathf.Clamp01(At(i)[line]/max)*r.height);
        private void Box(VertexHelper vh,Rect r,Color c)=>Quad(vh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),c);
        private void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ink){
            for(var i=0;i<4;i++){quad[i]=UIVertex.simpleVert;quad[i].color=ink;}
            quad[0].position=a;quad[1].position=b;quad[2].position=c;quad[3].position=d;vh.AddUIVertexQuad(quad);
        }
    }
}
