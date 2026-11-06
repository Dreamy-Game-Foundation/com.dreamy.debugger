using System;

namespace Dreamy.Debugger.Options
{
    /// <summary>Opt in to a confirmation dialog for a plain reflected action. Card grants always confirm.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CheatConfirmationAttribute : Attribute { }

    /// <summary>Keep an option ID stable when renaming its C# member.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class CheatIdAttribute : Attribute
    {
        public string Id { get; }

        public CheatIdAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A cheat needs a stable ID.", nameof(id));
            Id = id;
        }
    }

    /// <summary>Presentation metadata only; the game still owns values and mutations.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public abstract class CheatCardAttribute : Attribute
    {
        public CheatCard Card { get; }
        public string OptionId { get; }
        public string DisplayName { get; }

        protected CheatCardAttribute(string id, string title, CheatCardKind kind, CheatCardRole role)
        {
            Card = new CheatCard(id, title, kind, role);
            switch (role)
            {
                case CheatCardRole.Balance:
                    OptionId = id + ".balance"; DisplayName = title + " balance"; break;
                case CheatCardRole.Amount:
                    OptionId = id + ".amount"; DisplayName = title + " grant amount"; break;
                case CheatCardRole.Owned:
                    OptionId = id + ".owned"; DisplayName = title + " owned"; break;
                default:
                    OptionId = id + (kind == CheatCardKind.Resource ? ".grant" : ".unlock");
                    DisplayName = (kind == CheatCardKind.Resource ? "Add " : "Grant ") + title; break;
            }
        }
    }

    /// <summary>Group a read-only balance, writable amount and confirmed action into one card.</summary>
    public sealed class ResourceCheatAttribute : CheatCardAttribute
    {
        public ResourceCheatAttribute(string id, string title, CheatCardRole role)
            : base(id, title, CheatCardKind.Resource, role) { }
    }

    /// <summary>Group read-only ownership and a grant-only action into one card.</summary>
    public sealed class OwnershipCheatAttribute : CheatCardAttribute
    {
        public OwnershipCheatAttribute(string id, string title, CheatCardRole role)
            : base(id, title, CheatCardKind.Ownership, role) { }
    }
}
