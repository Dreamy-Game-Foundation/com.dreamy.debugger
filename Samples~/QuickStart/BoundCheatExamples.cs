using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    /// <summary>Editable template. Bind these delegates to your game's resource service.</summary>
    public sealed class ResourceCheatOptions
    {
        private readonly string id, category, label;
        private readonly Func<long> balance;
        private readonly Action<long> grant;
        private long amount;

        public ResourceCheatOptions(string id, string category, string label, Func<long> balance,
            Action<long> grant, long initialAmount = 100, long maximumAmount = 1000000)
        {
            if (initialAmount < 1 || initialAmount > maximumAmount) throw new ArgumentOutOfRangeException(nameof(initialAmount));
            this.id = id; this.category = category; this.label = label;
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
            this.grant = grant ?? throw new ArgumentNullException(nameof(grant));
            amount = initialAmount; MaximumAmount = maximumAmount;
        }

        public long MaximumAmount { get; }
        public IEnumerable<CheatOption> CreateOptions()
        {
            yield return CheatOption.Value(id + ".balance", category, label + " balance", balance);
            yield return CheatOption.Value(id + ".amount", category, label + " grant amount", () => amount,
                value => amount = value, 1, MaximumAmount);
            yield return CheatOption.Button(id + ".grant", category, "Add " + label, () => grant(amount), true);
        }
    }

    /// <summary>Grant-only ownership template: never revokes a real purchase entitlement.</summary>
    public sealed class OwnershipCheatOptions
    {
        private readonly string id, category, label;
        private readonly Func<bool> owned;
        private readonly Action unlock;

        public OwnershipCheatOptions(string id, string category, string label, Func<bool> owned, Action unlock)
        {
            this.id = id; this.category = category; this.label = label;
            this.owned = owned ?? throw new ArgumentNullException(nameof(owned));
            this.unlock = unlock ?? throw new ArgumentNullException(nameof(unlock));
        }

        public IEnumerable<CheatOption> CreateOptions()
        {
            yield return CheatOption.Value(id + ".owned", category, label + " owned", owned);
            yield return CheatOption.Button(id + ".unlock", category, "Grant " + label, () =>
            {
                if (!owned()) unlock();
            }, true);
        }
    }
}
