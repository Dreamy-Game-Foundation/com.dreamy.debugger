using System;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Reporting;
using NUnit.Framework;

namespace Dreamy.Debugger.Sample.Tests
{
    public sealed class FakeReportTests
    {
        [TestCase(FakeReportOutcome.Success, DeliveryStatus.Accepted)]
        [TestCase(FakeReportOutcome.Offline, DeliveryStatus.Retry)]
        [TestCase(FakeReportOutcome.RateLimited, DeliveryStatus.Retry)]
        [TestCase(FakeReportOutcome.ServerError, DeliveryStatus.Retry)]
        [TestCase(FakeReportOutcome.Reject, DeliveryStatus.Rejected)]
        [TestCase(FakeReportOutcome.Timeout, DeliveryStatus.Retry)]
        public async Task FakeScenariosHaveNoNetwork(FakeReportOutcome outcome,DeliveryStatus expected)
        {
            var fake=new FakeReportTransport{Outcome=outcome,DelayMilliseconds=1};
            var report=new DiagnosticReport("[FAKE] QA incident", "qa",true);
            Assert.That(await new ReportDispatcher(fake).SendAsync(report,CancellationToken.None),Is.EqualTo(expected));
            Assert.That(fake.SendCount,Is.EqualTo(1));
        }
        [Test] public void FakeTransportRejectsRealReport()
        {
            var fake=new FakeReportTransport();
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await fake.SendAsync(
                new DiagnosticReport("real", "qa"),CancellationToken.None));
        }
        [Test] public void EditableProjectCheatsExerciseGroupedValuesAndConfirmedReset()
        {
            var model=new ProjectCheats(); var registry=new CheatRegistry(()=>true);
            using(registry.Register(model.CreateOptions()))
            {
                registry.Set("player.god-mode",true); registry.Set("game.difficulty",DemoDifficulty.Hard);
                registry.Set("game.speed",3f); registry.Execute("economy.grant", true);
                Assert.That(model.Coins,Is.EqualTo(100)); Assert.That(model.GodMode,Is.True);
                Assert.Throws<InvalidOperationException>(()=>registry.Execute("save.reset"));
                registry.Execute("save.reset",true);
                Assert.That(model.Coins,Is.Zero); Assert.That(model.GodMode,Is.False);
                Assert.That(model.Speed,Is.EqualTo(1)); Assert.That(model.Difficulty,Is.EqualTo(DemoDifficulty.Normal));
            }
            Assert.That(registry.Snapshot().Count,Is.Zero);
        }
    }
}
