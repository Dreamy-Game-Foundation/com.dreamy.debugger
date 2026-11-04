using System;
using Dreamy.Debugger.Access;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class AccessTests
    {
        private double now;
        private AccessSession Create(AccessPolicy policy = null) => new AccessSession(policy ?? new AccessPolicy(), () => now);
        private void Gesture(AccessSession session, int count = 5)
        {
            for (var i = 0; i < count; i++) { session.Tap(); now += .1; }
        }
        [SetUp] public void SetUp() { now = 1; }
        [Test] public void DefaultsRequireFiveTapsThen0000()
        {
            var session = Create();
            Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.GestureRequired));
            for (var i=0;i<4;i++) { Assert.That(session.Tap(), Is.False); now += .1; }
            Assert.That(session.IsPromptOpen, Is.False);
            Assert.That(session.Tap(), Is.True);
            Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
            Assert.That(session.IsUnlocked, Is.True);
        }
        [TestCase(1,"0000")]
        [TestCase(3,"001234")]
        [TestCase(8,"00000000000000000000000000000001")]
        public void CustomCountAndPinPreserveLeadingZeroes(int count,string pin)
        {
            var session = Create(new AccessPolicy(count, pin)); Gesture(session, count);
            Assert.That(session.SubmitPin(pin), Is.EqualTo(UnlockResult.Unlocked));
        }
        [TestCase(0)] [TestCase(21)] public void RejectsInvalidTapCount(int count)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new AccessPolicy(count)); }
        [TestCase(null)] [TestCase("")] [TestCase("123")] [TestCase("12a4")] [TestCase("１２３４")]
        [TestCase("000000000000000000000000000000000")]
        public void RejectsInvalidPin(string pin) { Assert.Throws<ArgumentException>(() => new AccessPolicy(pin:pin)); }
        [Test] public void RejectsNonFiniteTiming()
        {
            Assert.Throws<ArgumentException>(() => new AccessPolicy(tapGapSeconds:double.NaN));
            Assert.Throws<ArgumentException>(() => new AccessPolicy(sequenceSeconds:double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new AccessPolicy(cooldownSeconds:0));
        }
        [Test] public void GapResetsSequence()
        {
            var sequence = new TapSequence(new AccessPolicy(3));
            Assert.That(sequence.Tap(1), Is.False); Assert.That(sequence.Tap(1.1), Is.False);
            Assert.That(sequence.Tap(2), Is.False); Assert.That(sequence.Tap(2.1), Is.False);
            Assert.That(sequence.Tap(2.2), Is.True);
        }
        [Test] public void OverallTimeoutResetsEvenIfEachGapIsValid()
        {
            var sequence = new TapSequence(new AccessPolicy(3, sequenceSeconds:.5));
            sequence.Tap(1); sequence.Tap(1.3);
            Assert.That(sequence.Tap(1.6), Is.False);
        }
        [Test] public void DifferentPointerAndTimeRollbackResetSequence()
        {
            var sequence = new TapSequence(new AccessPolicy(2));
            sequence.Tap(1,1); Assert.That(sequence.Tap(1.1,2), Is.False);
            Assert.That(sequence.Tap(.5,2), Is.False); Assert.That(sequence.Tap(.6,2), Is.True);
        }
        [Test] public void RejectsInvalidTimestamp()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new TapSequence(new AccessPolicy()).Tap(double.NaN)); }
        [Test] public void IncorrectPinDoesNotUnlockAndCanRetry()
        {
            var session = Create(); Gesture(session);
            Assert.That(session.SubmitPin("0001"), Is.EqualTo(UnlockResult.InvalidPin));
            Assert.That(session.SubmitPin("000"), Is.EqualTo(UnlockResult.InvalidPin));
            Assert.That(session.IsUnlocked, Is.False);
            Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
        }
        [Test] public void CooldownSurvivesRelockAndRequiresNewGestureAfterExpiry()
        {
            var session = Create(new AccessPolicy(failedAttemptLimit:2)); Gesture(session);
            session.SubmitPin("1111"); Assert.That(session.SubmitPin("1111"), Is.EqualTo(UnlockResult.Cooldown));
            session.Relock(); Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.Cooldown));
            Assert.That(session.Tap(), Is.False); now += 31;
            Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.GestureRequired));
            Gesture(session); Assert.That(session.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
        }
        [Test] public void SessionExpiresAndFocusRelockClearsPartialGesture()
        {
            var session = Create(new AccessPolicy(sessionSeconds:2)); Gesture(session); session.SubmitPin("0000");
            now += 2; Assert.That(session.IsUnlocked, Is.False);
            session.Tap(); session.Tap(); session.Relock(); Gesture(session,4);
            Assert.That(session.IsPromptOpen, Is.False);
        }
        [Test] public void SuccessfulUnlockResetsFailureCount()
        {
            var session = Create(new AccessPolicy(failedAttemptLimit:2)); Gesture(session);
            session.SubmitPin("1111"); session.SubmitPin("0000"); session.Relock(); Gesture(session);
            Assert.That(session.SubmitPin("1111"), Is.EqualTo(UnlockResult.InvalidPin));
        }
    }
}
