using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>The resolved branch ruleset for one repository.</summary>
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
}
