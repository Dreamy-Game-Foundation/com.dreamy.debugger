using System;
using System.ComponentModel;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger
{
    /// <summary>Game containers register once; the lease owns notification subscriptions.</summary>
    public static class CheatRegistrationExtensions
    {
        public static IDisposable RegisterCheats(this DebuggerRuntime runtime, object container, string prefix)
        {
            if (runtime == null || !runtime.isActiveAndEnabled || runtime.Cheats == null)
                throw new InvalidOperationException("Wait for an active debugger runtime before registering cheats.");
            var registration = new RuntimeRegistration(runtime, runtime.Cheats.RegisterContainer(container, prefix));
            return registration;
        }

        public static IDisposable RegisterContainer(this CheatRegistry registry, object container, string prefix)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var lease = registry.Register(AttributedCheatOptions.From(container, prefix));
            try { return new ContainerRegistration(registry, container as INotifyPropertyChanged, lease); }
            catch { lease.Dispose(); throw; }
        }

        private sealed class ContainerRegistration : IDisposable
        {
            private readonly CheatRegistry registry;
            private INotifyPropertyChanged notifier;
            private IDisposable lease;

            public ContainerRegistration(CheatRegistry registry, INotifyPropertyChanged notifier, IDisposable lease)
            {
                this.registry = registry; this.notifier = notifier; this.lease = lease;
                if (notifier != null) notifier.PropertyChanged += OnPropertyChanged;
            }

            private void OnPropertyChanged(object sender, PropertyChangedEventArgs args) => registry.RequestValueRefresh();

            public void Dispose()
            {
                if (lease == null) return;
                var previous = lease; lease = null;
                try { if (notifier != null) notifier.PropertyChanged -= OnPropertyChanged; }
                finally { notifier = null; previous.Dispose(); }
            }
        }

        private sealed class RuntimeRegistration : IDisposable
        {
            private DebuggerRuntime runtime;
            private IDisposable lease;
            public RuntimeRegistration(DebuggerRuntime runtime, IDisposable lease)
            {
                this.runtime = runtime; this.lease = lease;
                runtime.ShuttingDown += Dispose;
            }
            public void Dispose()
            {
                if (lease == null) return;
                runtime.ShuttingDown -= Dispose;
                runtime = null;
                var previous = lease; lease = null; previous.Dispose();
            }
        }
    }
}
