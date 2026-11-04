using System;

namespace Dreamy.Debugger.Reporting
{
    /// <summary>Bounded immutable text-only report. Screenshot and durable outbox arrive in a later phase.</summary>
    public sealed class DiagnosticReport
    {
        public string Id { get; }
        public string Message { get; }
        public string BuildId { get; }
        public bool IsSynthetic { get; }
        public DateTime CreatedUtc { get; }
        public DiagnosticReport(string message, string buildId, bool isSynthetic = false)
            : this(Guid.NewGuid().ToString("N"), message, buildId, isSynthetic, DateTime.UtcNow) { }
        internal DiagnosticReport(string id, string message, string buildId, bool isSynthetic, DateTime createdUtc)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Report message is required.", nameof(message));
            if (message.Length > 16384) throw new ArgumentException("Report message exceeds 16384 characters.", nameof(message));
            if (buildId == null || buildId.Length > 256) throw new ArgumentException("Invalid build ID.", nameof(buildId));
            if (!Guid.TryParseExact(id,"N",out _)) throw new ArgumentException("Invalid report ID.");
            Id = id; Message = message; BuildId = buildId;
            IsSynthetic = isSynthetic; CreatedUtc = createdUtc.ToUniversalTime();
        }
    }
}
