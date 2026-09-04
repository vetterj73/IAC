using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>A resolved reviewer that the ruleset itself requires.</summary>
    public sealed class ResolvedRequiredReviewer
    {
        /// <summary>Numeric team id.</summary>
        public required int Id { get; init; }

        public required string Type { get; init; }

        public required int MinimumApprovals { get; init; }

        public required IReadOnlyList<string> FilePatterns { get; init; }
    }
}
