using System;
using System.Collections.Generic;
using System.Linq;

namespace Dreamy.Debugger.Options
{
    /// <summary>Main-thread registry. Register a category/container atomically and dispose its lease on owner shutdown.</summary>
    public sealed class CheatRegistry
    {
        private readonly Dictionary<string, CheatOption> options = new Dictionary<string, CheatOption>(StringComparer.Ordinal);
        private readonly Func<bool> canExecute;
        public event Action Changed;
        public CheatRegistry(Func<bool> canExecute) { this.canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute)); }
        public IReadOnlyList<CheatOption> Snapshot() => options.Values.OrderBy(o => o.Category, StringComparer.Ordinal)
            .ThenBy(o => o.DisplayName, StringComparer.Ordinal).ToArray();
        public IDisposable Register(IEnumerable<CheatOption> container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            var list = new List<CheatOption>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in container)
            {
                if (option == null || options.ContainsKey(option.Id) || !ids.Add(option.Id))
                    throw new ArgumentException("Null or duplicate cheat option; registration rolled back.");
                list.Add(option);
            }
            foreach (var option in list) options.Add(option.Id, option);
            Changed?.Invoke();
            return new Registration(this, list);
        }
        public object Read(string id) { Authorize(); return Get(id).Read(); }
        public void Set(string id, object value) { Authorize(); Get(id).Write(value); }
        public void Execute(string id, bool confirmed = false) { Authorize(); Get(id).Invoke(confirmed); }
        private void Authorize() { if (!canExecute()) throw new UnauthorizedAccessException("Debugger access is locked or disabled by build policy."); }
        private CheatOption Get(string id)
        {
            if (!options.TryGetValue(id, out var option)) throw new KeyNotFoundException("Cheat option is not registered: " + id);
            return option;
        }
        private sealed class Registration : IDisposable
        {
            private CheatRegistry owner;
            private readonly List<CheatOption> owned;
            public Registration(CheatRegistry owner, List<CheatOption> owned) { this.owner = owner; this.owned = owned; }
            public void Dispose()
            {
                if (owner == null) return;
                foreach (var option in owned) owner.options.Remove(option.Id);
                var previous=owner; owner = null; owned.Clear(); previous.Changed?.Invoke();
            }
        }
    }
}
