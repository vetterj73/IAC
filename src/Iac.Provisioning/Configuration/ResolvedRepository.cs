using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>Who owns the repositories, which changes what policies are even possible.</summary>
    public enum RepositoryOwnerType
    {
        /// <summary>An organization: teams, organization admins and evaluate mode all exist.</summary>
        Organization,

        /// <summary>
        /// A single user account. No teams, no organization admin to bypass a ruleset, and no
        /// second person to approve a pull request.
        /// </summary>
        User,
    }

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

    public sealed class ResolvedFeatures
    {
        public required bool Issues { get; init; }

        public required bool Wiki { get; init; }

        public required bool Projects { get; init; }

        public required bool Discussions { get; init; }
    }

    public sealed class ResolvedMerge
    {
        public required bool AllowSquash { get; init; }

        public required bool AllowMergeCommit { get; init; }

        public required bool AllowRebase { get; init; }

        public required bool AllowAutoMerge { get; init; }

        public required bool DeleteBranchOnMerge { get; init; }

        public required bool AllowUpdateBranch { get; init; }
    }

    public sealed class ResolvedSecurity
    {
        public required bool VulnerabilityAlerts { get; init; }

        public required bool SecretScanning { get; init; }

        public required bool SecretScanningPushProtection { get; init; }
    }

    public sealed class ResolvedRuleset
    {
        public required string Enforcement { get; init; }

        public required bool RequirePullRequest { get; init; }

        public required int MinimumApprovals { get; init; }

        public required bool DismissStaleReviewsOnPush { get; init; }

        public required bool RequireCodeOwnerReview { get; init; }

        public required bool RequireLastPushApproval { get; init; }

        public required bool RequireConversationResolution { get; init; }

        public required bool RequireLinearHistory { get; init; }

        public required bool RequireSignedCommits { get; init; }

        public required bool BlockForcePush { get; init; }

        public required bool BlockDeletion { get; init; }

        public required IReadOnlyList<string> RequiredStatusChecks { get; init; }

        public required bool AllowAdminBypass { get; init; }

        public required bool AllowSelfApproval { get; init; }

        public required IReadOnlyList<int> BuildValidationPipelineIds { get; init; }

        public required IReadOnlyList<ResolvedBypassActor> BypassActors { get; init; }

        public required IReadOnlyList<ResolvedRequiredReviewer> RequiredReviewers { get; init; }
    }

    public sealed class ResolvedBypassActor
    {
        /// <summary>Null for actor types that have no id, such as OrganizationAdmin.</summary>
        public required int? ActorId { get; init; }

        public required string ActorType { get; init; }

        public required string BypassMode { get; init; }
    }

    public sealed class ResolvedRequiredReviewer
    {
        /// <summary>Numeric team id.</summary>
        public required int Id { get; init; }

        public required string Type { get; init; }

        public required int MinimumApprovals { get; init; }

        public required IReadOnlyList<string> FilePatterns { get; init; }
    }

    public sealed class ResolvedCollaborator
    {
        public required string Name { get; init; }

        public required string Permission { get; init; }
    }

    public sealed class ResolvedFiles
    {
        public required bool Readme { get; init; }

        public required bool Codeowners { get; init; }

        public required bool PullRequestTemplate { get; init; }
    }
}
