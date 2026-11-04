using System;
namespace Dreamy.Debugger.Metrics {
    public sealed class FrameWindow {
        private readonly double[] frames;
        private int next,count;
        private double total;
        public int Count => count;
        public int Capacity => frames.Length;
        public double AverageSeconds => count==0?0:total/count;
        public double FramesPerSecond => total<=0?0:count/total;
        public FrameWindow(int capacity=120) {if(capacity<2 || capacity>4096)throw new ArgumentOutOfRangeException(nameof(capacity));frames=new double[capacity];}
        public void Add(double seconds) {
            if(seconds<=0 || double.IsNaN(seconds) || double.IsInfinity(seconds))return;
            if(count==frames.Length)total-=frames[next];else count++;
            frames[next]=seconds;total+=seconds;next=(next+1)%frames.Length;
        }
        public double GetOldest(int index) {if(index<0 || index>=count)throw new ArgumentOutOfRangeException(nameof(index));return frames[(next-count+frames.Length+index)%frames.Length];}
        public void Clear(){Array.Clear(frames,0,frames.Length);next=count=0;total=0;}
    }
}
