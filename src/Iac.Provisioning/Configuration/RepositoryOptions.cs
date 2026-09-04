using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// The YAML surface for one repository. Every member is nullable so that a value can be
    /// "not stated here" and inherit from <see cref="IacConfiguration.Defaults"/>. Resolved
    /// into a <see cref="ResolvedRepository"/> before any provider sees it.
    /// </summary>
    public sealed class RepositoryOptions
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Homepage { get; set; }

        /// <summary><c>private</c>, <c>public</c> or <c>internal</c>.</summary>
        public string? Visibility { get; set; }

        public string? DefaultBranch { get; set; }

        public IList<string>? Topics { get; set; }

        /// <summary>GitHub license template key, e.g. <c>mit</c>. Null leaves the repo unlicensed.</summary>
        public string? License { get; set; }

        /// <summary>GitHub .gitignore template name, e.g. <c>VisualStudio</c>.</summary>
        public string? GitignoreTemplate { get; set; }

        /// <summary>
        /// When true, <c>destroy</c> archives the repository instead of deleting it. Leaving
        /// this on is the difference between a recoverable mistake and a lost repository.
        /// </summary>
        public bool? ArchiveOnDestroy { get; set; }

        public FeatureOptions? Features { get; set; }

        public MergeOptions? Merge { get; set; }

        public SecurityOptions? Security { get; set; }

        public RulesetOptions? Ruleset { get; set; }

        /// <summary>
        /// Logins or teams that should approve changes, written into CODEOWNERS. Entries are
        /// used verbatim, so they must be <c>@user</c> or <c>@org/team</c> form.
        /// </summary>
        public IList<string>? Approvers { get; set; }

        public CollaboratorOptions? Collaborators { get; set; }

        public GeneratedFileOptions? Files { get; set; }
    }

    /// <summary>Per-repository feature toggles. GitHub-specific: Azure DevOps sets these per project.</summary>
    public sealed class FeatureOptions
    {
        public bool? Issues { get; set; }

        public bool? Wiki { get; set; }

        public bool? Projects { get; set; }

        public bool? Discussions { get; set; }
    }

    public sealed class MergeOptions
    {
        public bool? AllowSquash { get; set; }

        public bool? AllowMergeCommit { get; set; }

        public bool? AllowRebase { get; set; }

        public bool? AllowAutoMerge { get; set; }

        public bool? DeleteBranchOnMerge { get; set; }

        public bool? AllowUpdateBranch { get; set; }
    }

    public sealed class SecurityOptions
    {
        public bool? VulnerabilityAlerts { get; set; }

        /// <summary>Requires GitHub Advanced Security on private repositories.</summary>
        public bool? SecretScanning { get; set; }

        /// <summary>Requires GitHub Advanced Security on private repositories.</summary>
        public bool? SecretScanningPushProtection { get; set; }
    }

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

    /// <summary>An actor allowed to bypass the ruleset - typically a break-glass admin path.</summary>
    public sealed class BypassActorOptions
    {
        /// <summary>
        /// Numeric id of the actor. Required for <c>RepositoryRole</c>, <c>Team</c>,
        /// <c>Integration</c> and <c>User</c>; it must be <b>omitted</b> for
        /// <c>OrganizationAdmin</c>, <c>EnterpriseOwner</c> and <c>DeployKey</c>, which have no
        /// id - GitHub ignores one if sent.
        /// </summary>
        public int? ActorId { get; set; }

        /// <summary>
        /// One of <c>RepositoryRole</c>, <c>Team</c>, <c>Integration</c>,
        /// <c>OrganizationAdmin</c>, <c>DeployKey</c>, <c>EnterpriseOwner</c>, <c>User</c>.
        /// Case-sensitive.
        /// </summary>
        public string? ActorType { get; set; }

        /// <summary><c>always</c>, <c>pull_request</c> or <c>exempt</c>. Case-sensitive.</summary>
        public string? BypassMode { get; set; }
    }

    public sealed class RequiredReviewerOptions
    {
        /// <summary>
        /// Numeric id of the team that must review. Find it with
        /// <c>gh api /orgs/{org}/teams/{slug} --jq .id</c>; the provider takes the id, not the slug.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>Reviewer kind. The provider currently supports only <c>Team</c>.</summary>
        public string? Type { get; set; }

        public int? MinimumApprovals { get; set; }

        /// <summary>Paths that trigger this reviewer requirement.</summary>
        public IList<string>? FilePatterns { get; set; }
    }

    public sealed class CollaboratorOptions
    {
        public IList<CollaboratorEntry>? Users { get; set; }

        public IList<CollaboratorEntry>? Teams { get; set; }
    }

    public sealed class CollaboratorEntry
    {
        /// <summary>User login, or team slug for a team entry.</summary>
        public string? Name { get; set; }

        /// <summary><c>pull</c>, <c>triage</c>, <c>push</c>, <c>maintain</c> or <c>admin</c>.</summary>
        public string? Permission { get; set; }
    }

    /// <summary>Files this tool seeds into a new repository. All are safe to edit afterwards.</summary>
    public sealed class GeneratedFileOptions
    {
        public bool? Readme { get; set; }

        public bool? Codeowners { get; set; }

        public bool? PullRequestTemplate { get; set; }
    }
}
