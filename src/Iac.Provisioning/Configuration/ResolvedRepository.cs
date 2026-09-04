using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// A repository definition after built-in defaults, file defaults and the entry's own
    /// values have been merged. Everything a provider needs is non-null here, so provider
    /// code contains no default-handling and no null checks.
    /// </summary>
    public sealed class ResolvedRepository
    {
        public required string Name { get; init; }

        /// <summary>Copied from the root configuration; several rules depend on it.</summary>
        public required RepositoryOwnerType OwnerType { get; init; }

        public string? Description { get; init; }

        public string? Homepage { get; init; }

        public required string Visibility { get; init; }

        public required string DefaultBranch { get; init; }

        public required IReadOnlyList<string> Topics { get; init; }

        public string? License { get; init; }

        public string? GitignoreTemplate { get; init; }

        public required bool ArchiveOnDestroy { get; init; }

        public required ResolvedFeatures Features { get; init; }

        public required ResolvedMerge Merge { get; init; }

        public required ResolvedSecurity Security { get; init; }

        public required ResolvedRuleset Ruleset { get; init; }

        public required IReadOnlyList<string> Approvers { get; init; }

        public required IReadOnlyList<ResolvedCollaborator> UserCollaborators { get; init; }

        public required IReadOnlyList<ResolvedCollaborator> TeamCollaborators { get; init; }

        public required ResolvedFiles Files { get; init; }
    }
}
