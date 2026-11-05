using System;
namespace Dreamy.Debugger.Metrics {
    /// <summary>Unscaled caller-owned clock; sustained low FPS, hysteresis and bounded repetitions.</summary>
    public sealed class FpsThresholdRule {
        private readonly double threshold,sustain,cooldown;
        private double lowSince=-1,recoveredSince=-1,next;
        private bool armed=true;private int count;
        public FpsThresholdRule(double threshold,double sustain,double cooldown){if(threshold<=0||sustain<0||cooldown<0||double.IsNaN(threshold+sustain+cooldown)||double.IsInfinity(threshold+sustain+cooldown))throw new ArgumentException("Invalid FPS rule");this.threshold=threshold;this.sustain=sustain;this.cooldown=cooldown;}
        public bool Step(double time,double fps,bool suppressed=false){
            if(suppressed||time<5||fps<=0||double.IsNaN(fps)||double.IsInfinity(fps)){lowSince=recoveredSince=-1;return false;}
            if(!armed){if(fps>=threshold+5){if(recoveredSince<0)recoveredSince=time;if(time-recoveredSince>=2)armed=true;}else recoveredSince=-1;return false;}
            if(fps>=threshold){lowSince=-1;return false;}
            if(lowSince<0)lowSince=time;
            if(time-lowSince<sustain||time<next||count>=10)return false;
            count++;next=time+cooldown;armed=false;lowSince=-1;return true;
        }
    }
}
