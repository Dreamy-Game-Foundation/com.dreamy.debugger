using System;
namespace Dreamy.Debugger.Options
{
    public enum CheatCardKind { Resource, Ownership }
    public enum CheatCardRole { Balance, Amount, Action, Owned }
    /// <summary>Optional view metadata; does not own gameplay state or invoke services.</summary>
    public sealed class CheatCard
    {
        public string Id { get; }
        public string Title { get; }
        public CheatCardKind Kind { get; }
        public CheatCardRole Role { get; }
        public CheatCard(string id, string title, CheatCardKind kind, CheatCardRole role)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Card needs a stable ID and title.");
            if (!Enum.IsDefined(typeof(CheatCardKind), kind) || !Enum.IsDefined(typeof(CheatCardRole), role)) throw new ArgumentException("Unknown card metadata.");
            if (kind == CheatCardKind.Resource && role == CheatCardRole.Owned || kind == CheatCardKind.Ownership && role != CheatCardRole.Owned && role != CheatCardRole.Action) throw new ArgumentException("Role is incompatible with card kind.");
            Id = id; Title = title; Kind = kind; Role = role;
        }
    }
}
