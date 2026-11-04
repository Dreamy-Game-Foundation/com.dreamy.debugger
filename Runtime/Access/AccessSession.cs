using System;

namespace Dreamy.Debugger.Access
{
    public enum UnlockResult { Unlocked, InvalidPin, GestureRequired, Cooldown }

    /// <summary>Main-thread owner. Host must call Relock on focus loss/scene policy changes.</summary>
    public sealed class AccessSession
    {
        private readonly AccessPolicy policy;
        private readonly Func<double> clock;
        private readonly TapSequence taps;
        private bool prompt;
        private int failed;
        private double cooldownUntil, unlockedUntil;
        public bool IsUnlocked => unlockedUntil > clock();
        public bool IsPromptOpen => prompt;
        public AccessSession(AccessPolicy policy, Func<double> unscaledClock)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            clock = unscaledClock ?? throw new ArgumentNullException(nameof(unscaledClock));
            taps = new TapSequence(policy);
        }
        public bool Tap(int pointerId = 0)
        {
            var now = clock();
            if (now < cooldownUntil || IsUnlocked || prompt) return false;
            prompt = taps.Tap(now, pointerId);
            return prompt;
        }
        public bool RequestPrompt() {
            if (clock() < cooldownUntil || IsUnlocked) return false;
            taps.Reset(); prompt = true; return true;
        }
        public UnlockResult SubmitPin(string candidate)
        {
            var now = clock();
            if (now < cooldownUntil) return UnlockResult.Cooldown;
            if (!prompt) return UnlockResult.GestureRequired;
            if (policy.Verify(candidate))
            {
                failed = 0; prompt = false; unlockedUntil = now + policy.SessionSeconds;
                return UnlockResult.Unlocked;
            }
            if (++failed >= policy.FailedAttemptLimit)
            {
                cooldownUntil = now + policy.CooldownSeconds; failed = 0; Relock();
                return UnlockResult.Cooldown;
            }
            return UnlockResult.InvalidPin;
        }
        public void Relock() { unlockedUntil = 0; prompt = false; taps.Reset(); }
    }
}
