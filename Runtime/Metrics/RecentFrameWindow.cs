using System;
namespace Dreamy.Debugger.Metrics
{
    /// <summary>Short time window for responsive rules, fed by the existing profiler sampler.</summary>
    public sealed class RecentFrameWindow
    {
        private readonly double[] frames = new double[4096];
        private readonly double duration;
        private int start, count;
        private double total;
        public double AverageSeconds => count == 0 ? 0 : total / count;
        public RecentFrameWindow(double durationSeconds = .5)
        {
            if (durationSeconds <= 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            duration = durationSeconds;
        }
        public void Add(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (count == frames.Length) RemoveOldest();
            frames[(start + count) % frames.Length] = seconds; count++; total += seconds;
            while (count > 1 && total - frames[start] >= duration) RemoveOldest();
        }
        private void RemoveOldest() { total -= frames[start]; start = (start + 1) % frames.Length; count--; }
        public void Clear() { start = count = 0; total = 0; }
    }
}
