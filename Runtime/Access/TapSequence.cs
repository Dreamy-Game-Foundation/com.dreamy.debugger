using System;

namespace Dreamy.Debugger.Access
{
    /// <summary>Host forwards taps inside its safe-area trigger. Does not intercept gameplay input.</summary>
    public sealed class TapSequence
    {
        private readonly AccessPolicy policy;
        private int count;
        private double first, last;
        private int pointer;
        public TapSequence(AccessPolicy policy) { this.policy = policy ?? throw new ArgumentNullException(nameof(policy)); }
        public bool Tap(double unscaledTime, int pointerId = 0)
        {
            if (double.IsNaN(unscaledTime) || double.IsInfinity(unscaledTime) || unscaledTime < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledTime));
            if (count > 0 && (unscaledTime < last || unscaledTime - last > policy.TapGapSeconds ||
                unscaledTime - first > policy.SequenceSeconds || pointerId != pointer)) Reset();
            if (count == 0) { first = unscaledTime; pointer = pointerId; }
            last = unscaledTime;
            if (++count < policy.TapCount) return false;
            Reset();
            return true;
        }
        public void Reset() { count = 0; }
    }
}
