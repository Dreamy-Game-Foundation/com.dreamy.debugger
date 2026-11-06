using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Services.Implementation;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class OptionsWorkflowTests
    {
        private sealed class Model : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler changed;
            [Browsable(false)] public int Subscribers { get; private set; }
            public event PropertyChangedEventHandler PropertyChanged
            {
                add { changed += value; Subscribers++; }
                remove { changed -= value; Subscribers--; }
            }
            public int Value { get; set; }
            [Browsable(false)] public void Raise() => changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            public void Harmless() { }
            [CheatConfirmation] public void Destructive() { }
        }

        [Test] public void NotificationsCoalesceAcrossThreadsAndStopAtLeaseDisposal()
        {
            var registry = new CheatRegistry(() => true);
            var model = new Model();
            var delivered = 0; registry.ValuesChanged += () => delivered++;
            using (registry.RegisterContainer(model, "model"))
            {
                Assert.That(model.Subscribers, Is.EqualTo(1));
                for (var i = 0; i < 100; i++) model.Raise();
                Task.Run(() => model.Raise()).GetAwaiter().GetResult();
                Assert.That(delivered, Is.Zero, "Worker events must not invoke Unity-facing listeners");
                Assert.That(registry.FlushValueNotifications(), Is.True);
                Assert.That(delivered, Is.EqualTo(1));
                Assert.That(registry.FlushValueNotifications(), Is.False);
                Assert.Throws<ArgumentException>(() => registry.RegisterContainer(model, "model"));
                Assert.That(model.Subscribers, Is.EqualTo(1), "Failed duplicate registration must not subscribe");
                Assert.Throws<InvalidOperationException>(() => Task.Run(() => registry.FlushValueNotifications()).GetAwaiter().GetResult());
            }
            Assert.That(model.Subscribers, Is.Zero);
            model.Raise(); Assert.That(registry.FlushValueNotifications(), Is.False);
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test] public void PlainActionsAreImmediateButExplicitAndCardActionsConfirm()
        {
            var model = new Model(); var registry = new CheatRegistry(() => true);
            using (registry.RegisterContainer(model, "model"))
            {
                Assert.That(registry.Snapshot().Single(o => o.Id == "model.Harmless").RequiresConfirmation, Is.False);
                registry.Execute("model.Harmless");
                Assert.Throws<InvalidOperationException>(() => registry.Execute("model.Destructive"));
                registry.Execute("model.Destructive", true);
            }
        }

        [Test] public void ReusedIdsDoNotRetainTheOldOptionIdentity()
        {
            var registry = new CheatRegistry(() => true);
            var old = CheatOption.Button("same", "Test", "Old", () => { });
            var lease = registry.Register(new[] { old }); lease.Dispose();
            var next = CheatOption.Button("same", "Test", "New", () => { });
            using (registry.Register(new[] { next }))
            {
                Assert.That(registry.IsRegistered(old), Is.False);
                Assert.That(registry.IsRegistered(next), Is.True);
            }
        }

        [Test] public void DynamicContainerBatchesNestedChangesIntoOneServiceNotification()
        {
            var service = new OptionsServiceImpl(); var container = new DynamicOptionContainer();
            service.AddContainer(container); var changed = 0; service.OptionsUpdated += (_, __) => changed++;
            var first = OptionDefinition.Create("First", () => 1);
            var second = OptionDefinition.Create("Second", () => 2);
            using (container.BeginUpdate())
            {
                container.AddOption(first);
                using (container.BeginUpdate()) { container.AddOption(second); container.RemoveOption(first); }
                Assert.That(changed, Is.Zero);
            }
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(service.Options, Is.EquivalentTo(new[] { second }));
            service.RemoveContainer(container);
            var before = changed; container.AddOption(first);
            Assert.That(changed, Is.EqualTo(before), "Removed container must release batch listeners");
        }
    }
}
