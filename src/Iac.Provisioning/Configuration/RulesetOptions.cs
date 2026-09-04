using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// The branch ruleset applied to the default branch. Maps to a GitHub repository ruleset;
    /// on Azure DevOps the equivalent is a set of branch policies.
    /// </summary>
    public sealed class RulesetOptions
    {
        /// <summary><c>active</c>, <c>evaluate</c> (record but do not block) or <c>disabled</c>.</summary>
        public string? Enforcement { get; set; }

        public bool? RequirePullRequest { get; set; }

        /// <summary>
        /// Approving reviews needed before merge (0-10). Defaults to 1 for an
        /// organization-owned repository and <b>0 for a user-owned one</b>: nobody can approve
        /// their own pull request, so on a personal repository any value above zero leaves the
        /// owner unable to merge.
        /// </summary>
        public int? MinimumApprovals { get; set; }

        public bool? DismissStaleReviewsOnPush { get; set; }

        /// <summary>Requires approval from a CODEOWNERS owner. This is what makes Approvers binding.</summary>
        public bool? RequireCodeOwnerReview { get; set; }

        public bool? RequireLastPushApproval { get; set; }

        public bool? RequireConversationResolution { get; set; }

        public bool? RequireLinearHistory { get; set; }

        public bool? RequireSignedCommits { get; set; }

        /// <summary>Blocks force pushes to the default branch.</summary>
        public bool? BlockForcePush { get; set; }

        /// <summary>Blocks deletion of the default branch.</summary>
        public bool? BlockDeletion { get; set; }

        /// <summary>
        /// Status check contexts that must pass. A check cannot be required until it has
        /// reported at least once, so this stays empty on a brand-new repository - see
        /// docs/git/README.md for the two-pass flow.
        /// </summary>
        public IList<string>? RequiredStatusChecks { get; set; }

        /// <summary>
        /// Lets an organization administrator bypass the ruleset - the usual break-glass path.
        /// Organization-owned repositories only: a user-owned repository has no
        /// <c>OrganizationAdmin</c> actor, so this is rejected there.
        /// </summary>
        public bool? AllowAdminBypass { get; set; }

        /// <summary>
        /// Whether the author of a pull request may count towards its own approvals.
        /// </summary>
        /// <remarks>
        /// Azure DevOps supports this directly (<c>SubmitterCanVote</c>). GitHub does not - it
        /// never permits self-approval - so setting this true is reported as unsupported by
        /// the GitHub provider, where the equivalent is <c>minimumApprovals: 0</c>.
        /// </remarks>
        public bool? AllowSelfApproval { get; set; }

        public IList<BypassActorOptions>? BypassActors { get; set; }

        /// <summary>
        /// Azure DevOps build-validation pipeline ids. The Azure DevOps equivalent of
        /// <see cref="RequiredStatusChecks"/>, which references a pipeline by numeric id
        /// rather than a check by name. Ignored by GitHub.
        /// </summary>
        public IList<int>? BuildValidationPipelineIds { get; set; }

        /// <summary>
        /// Named reviewers enforced by the ruleset itself rather than by CODEOWNERS. GitHub
        /// treats this rule as beta; prefer Approvers plus RequireCodeOwnerReview.
        /// </summary>
        public IList<RequiredReviewerOptions>? RequiredReviewers { get; set; }
    }
}
