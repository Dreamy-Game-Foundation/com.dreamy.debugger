using System;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Metrics;
using NUnit.Framework;
namespace Dreamy.Debugger.Tests
{
    public sealed class ReleaseRegressionTests
    {
        [Test] public void ObserverFailureCannotLoseRegistrationLeaseOrOtherObservers()
        {
            var registry=new CheatRegistry(()=>true);int notifications=0;
            registry.Changed+=()=>throw new InvalidOperationException("observer");
            registry.Changed+=()=>notifications++;
            var lease=registry.Register(new[]{CheatOption.Value("test","Test","Value",()=>1)});
            Assert.That(registry.Snapshot().Count,Is.EqualTo(1));Assert.That(notifications,Is.EqualTo(1));
            Assert.That(registry.LastNotificationError,Is.TypeOf<InvalidOperationException>());
            Assert.DoesNotThrow(()=>lease.Dispose());Assert.That(registry.Snapshot(),Is.Empty);
            Assert.That(notifications,Is.EqualTo(2));
        }
        [Test] public void ValueNotificationsDoNotRebuildStructureAndRetainAuthorization()
        {
            bool allowed=true;int value=1,structure=0,updates=0;
            var registry=new CheatRegistry(()=>allowed);
            using(var lease=registry.Register(new[]{CheatOption.Value("value","Test","Value",()=>value,v=>value=v),CheatOption.Button("add","Test","Add",()=>value++)})){
                registry.Changed+=()=>structure++;
                registry.ValuesChanged+=()=>throw new Exception("bad view");registry.ValuesChanged+=()=>updates++;
                registry.Set("value",3);registry.Execute("add");Assert.That(value,Is.EqualTo(4));
                Assert.That(updates,Is.EqualTo(2));Assert.That(structure,Is.Zero);
                allowed=false;Assert.Throws<UnauthorizedAccessException>(()=>registry.Set("value",5));Assert.That(updates,Is.EqualTo(2));
            }
        }
        [Test] public void ShortMetricWindowTriggersRulePromptlyAfterRealFrameRateDrop()
        {
            var recent=new RecentFrameWindow(.5);var history=new FrameWindow(1024);var rule=new FpsThresholdRule(30,3,60);double time=0;
            for(int i=0;i<1024;i++){recent.Add(1d/60);history.Add(1d/60);time+=1d/60;rule.Step(time,1/recent.AverageSeconds);}
            double delay=-1;
            for(int i=1;i<=120;i++){recent.Add(1d/20);history.Add(1d/20);time+=1d/20;if(rule.Step(time,1/recent.AverageSeconds)){delay=i/20d;break;}}
            Assert.That(delay,Is.InRange(3,4));Assert.That(history.FramesPerSecond,Is.GreaterThan(30),"Long history is deliberately separate from alert metric");
        }
        [Test] public void CardMetadataValidatesRolesWithoutAddingGameplayAuthority(){
            long amount=100;
            var balance=CheatOption.Value("wallet.balance","Economy","Balance",()=>1L).WithCard("wallet","Coins",CheatCardKind.Resource,CheatCardRole.Balance);
            var input=CheatOption.Value("wallet.amount","Economy","Amount",()=>amount,v=>amount=v).WithCard("wallet","Coins",CheatCardKind.Resource,CheatCardRole.Amount);
            Assert.That(balance.Card.Id,Is.EqualTo(input.Card.Id));Assert.That(amount,Is.EqualTo(100));
            Assert.Throws<ArgumentException>(()=>CheatOption.Value("invalid","Test","Value",()=>true).WithCard("test","Test",CheatCardKind.Resource,CheatCardRole.Amount));
            Assert.Throws<ArgumentException>(()=>CheatOption.Value("invalid","Test","Value",()=>1).WithCard("test","Test",CheatCardKind.Resource,CheatCardRole.Amount));
            Assert.Throws<ArgumentException>(()=>new CheatCard("test","Test",CheatCardKind.Ownership,CheatCardRole.Balance));
        }

    }
}
