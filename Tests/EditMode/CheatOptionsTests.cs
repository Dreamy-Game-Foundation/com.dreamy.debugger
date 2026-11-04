using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class CheatOptionsTests
    {
        private enum Mode { Normal, Hard }
        [Test] public void TypedOptionsSupportBoolNumberEnumStringAndReadOnly()
        {
            var registry = new CheatRegistry(() => true);
            bool god=false; int coins=0; long seed=0; float speed=1; double time=1; Mode mode=Mode.Normal; string note="";
            using(registry.Register(new[] {
                CheatOption.Value("god","Player","God",()=>god,v=>god=v),
                CheatOption.Value("coins","Economy","Coins",()=>coins,v=>coins=v,0,100),
                CheatOption.Value("seed","World","Seed",()=>seed,v=>seed=v),
                CheatOption.Value("speed","Game","Speed",()=>speed,v=>speed=v,.1,5),
                CheatOption.Value("time","Game","Time",()=>time,v=>time=v),
                CheatOption.Value("mode","Game","Mode",()=>mode,v=>mode=v),
                CheatOption.Value("note","QA","Note",()=>note,v=>note=v),
                CheatOption.Value("readonly","QA","Read only",()=>42)
            }))
            {
                registry.Set("god",true); registry.Set("coins",50); registry.Set("seed",99L);
                registry.Set("speed",2f); registry.Set("time",3d); registry.Set("mode",Mode.Hard); registry.Set("note","QA");
                Assert.That(god,Is.True); Assert.That(coins,Is.EqualTo(50)); Assert.That(seed,Is.EqualTo(99));
                Assert.That(speed,Is.EqualTo(2)); Assert.That(time,Is.EqualTo(3)); Assert.That(mode,Is.EqualTo(Mode.Hard));
                Assert.That(note,Is.EqualTo("QA")); Assert.That(registry.Read("readonly"),Is.EqualTo(42));
                Assert.Throws<InvalidOperationException>(()=>registry.Set("readonly",10));
            }
        }
        [Test] public void BoundsAndNonFiniteValuesDoNotReachGameSetter()
        {
            var value=1f; var registry=new CheatRegistry(()=>true);
            registry.Register(new[]{CheatOption.Value("speed","Game","Speed",()=>value,v=>value=v,0,5)});
            foreach(var invalid in new[]{-1f,6f,float.NaN,float.PositiveInfinity})
                Assert.Throws<ArgumentOutOfRangeException>(()=>registry.Set("speed",invalid));
            Assert.That(value,Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(()=>registry.Set("speed",2));
        }
        [Test] public void UndefinedEnumAndUnsupportedValueTypesAreRejected()
        {
            var mode=Mode.Normal; var registry=new CheatRegistry(()=>true);
            registry.Register(new[]{CheatOption.Value("mode","Game","Mode",()=>mode,v=>mode=v)});
            Assert.Throws<ArgumentException>(()=>registry.Set("mode",(Mode)88));
            Assert.Throws<NotSupportedException>(()=>CheatOption.Value("bad","Game","Bad",()=>new object()));
            Assert.Throws<ArgumentException>(()=>CheatOption.Value("bad","Game","Bad",()=>true,minimum:0));
        }
        [Test] public void PermissionIsCheckedAtEveryReadWriteAndExecution()
        {
            var allowed=true; int mutations=0; var registry=new CheatRegistry(()=>allowed);
            registry.Register(new[]{CheatOption.Value("value","Game","Value",()=>mutations,v=>mutations=v),
                CheatOption.Button("action","Game","Action",()=>mutations++)});
            allowed=false;
            Assert.Throws<UnauthorizedAccessException>(()=>registry.Set("value",5));
            Assert.Throws<UnauthorizedAccessException>(()=>registry.Execute("action"));
            Assert.Throws<UnauthorizedAccessException>(()=>registry.Read("value"));
            Assert.That(mutations,Is.Zero);
        }
        [Test] public void DestructiveActionRequiresConfirmationAndDisposalRemovesIt()
        {
            int invoked=0; var registry=new CheatRegistry(()=>true);
            var lease=registry.Register(new[]{CheatOption.Button("reset","Save","Reset",()=>invoked++,true)});
            Assert.Throws<InvalidOperationException>(()=>registry.Execute("reset"));
            registry.Execute("reset",true); Assert.That(invoked,Is.EqualTo(1));
            lease.Dispose(); lease.Dispose();
            Assert.Throws<KeyNotFoundException>(()=>registry.Execute("reset",true));
        }
        [Test] public void DuplicateContainerRegistrationIsAtomic()
        {
            var registry=new CheatRegistry(()=>true);
            registry.Register(new[]{CheatOption.Button("exists","A","A",()=>{})});
            Assert.Throws<ArgumentException>(()=>registry.Register(new[]{
                CheatOption.Button("new","A","B",()=>{}),CheatOption.Button("exists","A","C",()=>{})}));
            Assert.That(registry.Snapshot().Count,Is.EqualTo(1));
            Assert.Throws<ArgumentException>(()=>registry.Register(new[]{
                CheatOption.Button("dup","A","B",()=>{}),CheatOption.Button("dup","A","C",()=>{})}));
            Assert.That(registry.Snapshot().Count,Is.EqualTo(1));
        }
        [Test] public void SnapshotIsGroupedSortedAndCannotMutateRegistry()
        {
            var registry=new CheatRegistry(()=>true);
            registry.Register(new[]{CheatOption.Button("b","Z","B",()=>{}),CheatOption.Button("a","A","A",()=>{})});
            var snapshot=registry.Snapshot(); Assert.That(snapshot[0].Category,Is.EqualTo("A"));
            ((CheatOption[])snapshot)[0]=null;
            Assert.That(registry.Snapshot()[0],Is.Not.Null);
        }
        [Test] public void OldDisposedLeaseCannotRemoveNewRegistration()
        {
            var registry=new CheatRegistry(()=>true);
            var old=registry.Register(new[]{CheatOption.Button("a","A","A",()=>{})}); old.Dispose();
            registry.Register(new[]{CheatOption.Button("a","A","A",()=>{})}); old.Dispose();
            Assert.That(registry.Snapshot().Count,Is.EqualTo(1));
        }
        [Test] public void HandlerFailurePropagatesWithoutSwallowingOrAutomaticRetry()
        {
            int calls=0; var registry=new CheatRegistry(()=>true);
            registry.Register(new[]{CheatOption.Button("bad","A","Bad",()=>{calls++;throw new InvalidOperationException("game failure");})});
            Assert.Throws<InvalidOperationException>(()=>registry.Execute("bad")); Assert.That(calls,Is.EqualTo(1));
        }
    }
}
