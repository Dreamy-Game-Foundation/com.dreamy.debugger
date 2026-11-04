using System;
using System.Security.Cryptography;
using System.Text;

namespace Dreamy.Debugger.Access
{
    /// <summary>Immutable access configuration. Local unlock prevents accidental access, not client tampering.</summary>
    public sealed class AccessPolicy
    {
        private readonly byte[] salt;
        private readonly byte[] verifier;
        public int TapCount { get; }
        public int PinLength { get; }
        public double TapGapSeconds { get; }
        public double SequenceSeconds { get; }
        public int FailedAttemptLimit { get; }
        public double CooldownSeconds { get; }
        public double SessionSeconds { get; }

        public AccessPolicy(int tapCount = 5, string pin = "0000", double tapGapSeconds = .4,
            double sequenceSeconds = 2.5, int failedAttemptLimit = 5,
            double cooldownSeconds = 30, double sessionSeconds = 300)
        {
            if (tapCount < 1 || tapCount > 20) throw new ArgumentOutOfRangeException(nameof(tapCount));
            if (!ValidPin(pin)) throw new ArgumentException("PIN must contain 4–32 ASCII digits.", nameof(pin));
            if (!Positive(tapGapSeconds) || !Positive(sequenceSeconds) || !Positive(cooldownSeconds) ||
                !Positive(sessionSeconds) || failedAttemptLimit < 1) throw new ArgumentException("Invalid access timing/attempt limit.");
            TapCount = tapCount; PinLength = pin.Length; TapGapSeconds = tapGapSeconds;
            SequenceSeconds = sequenceSeconds; FailedAttemptLimit = failedAttemptLimit;
            CooldownSeconds = cooldownSeconds; SessionSeconds = sessionSeconds;
            salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            verifier = Hash(pin);
        }

        internal bool Verify(string candidate)
        {
            if (!ValidPin(candidate) || candidate.Length != PinLength) return false;
            var actual = Hash(candidate);
            var different = 0;
            for (var i = 0; i < actual.Length; i++) different |= actual[i] ^ verifier[i];
            Array.Clear(actual, 0, actual.Length);
            return different == 0;
        }

        private byte[] Hash(string pin)
        {
            var bytes = Encoding.UTF8.GetBytes(pin);
            var input = new byte[salt.Length + bytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(bytes, 0, input, salt.Length, bytes.Length);
            try { using (var sha = SHA256.Create()) return sha.ComputeHash(input); }
            finally { Array.Clear(bytes, 0, bytes.Length); Array.Clear(input, 0, input.Length); }
        }

        private static bool ValidPin(string pin)
        {
            if (pin == null || pin.Length < 4 || pin.Length > 32) return false;
            foreach (var c in pin) if (c < '0' || c > '9') return false;
            return true;
        }
        private static bool Positive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
