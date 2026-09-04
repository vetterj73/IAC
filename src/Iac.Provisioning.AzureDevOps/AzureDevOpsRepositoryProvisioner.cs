using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Iac.Provisioning.Configuration;

using Pulumi;

using Ado = Pulumi.AzureDevOps;

namespace Iac.Provisioning.AzureDevOps
{
    /// <summary>
    /// Declares an Azure DevOps Git repository and the branch policies that stand in for a
    /// GitHub ruleset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Azure DevOps has no single "ruleset" object. Each rule is its own policy resource
    /// scoped to a repository and a branch, so one GitHub ruleset becomes up to four
    /// policies here. There is also no "require a pull request" switch: a blocking policy on
    /// a branch is what stops direct pushes, which is why the minimum-reviewers policy is
    /// created even when no approvals are required.
    /// </para>
    /// <para>
    /// Settings with no Azure DevOps equivalent are reported by
    /// <see cref="DescribeUnsupportedSettings"/> rather than silently dropped.
    /// </para>
    /// </remarks>
    public sealed class AzureDevOpsRepositoryProvisioner : IResourceProvisioner
    {
        /// <summary>Scope match type that tracks the default branch, whatever it is named.</summary>
        private const string DefaultBranchMatch = "DefaultBranch";

        public string ProviderName => "azuredevops";

        public string PulumiPluginName => "azuredevops";

        public IReadOnlyList<string> RequiredEnvironmentVariables => ["AZDO_PERSONAL_ACCESS_TOKEN"];

        /// <summary>
        /// True when Azure DevOps has to be told the author may approve their own pull request:
        /// either it was asked for, or no approvals are required and the policy cannot express
        /// that any other way.
        /// </summary>
        public static bool RequiresSelfApproval(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            return repository.Ruleset.RequirePullRequest && repository.Ruleset.MinimumApprovals == 0;
        }

        public IReadOnlyDictionary<string, string> BuildStackConfiguration(string organization)
        {
            ArgumentNullException.ThrowIfNull(organization);

            // Accept either a bare organization name or a full service URL, so Azure DevOps
            // Server (on-premises) installations work without a separate setting.
            string serviceUrl = organization.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? organization.TrimEnd('/')
                : "https://dev.azure.com/" + organization;

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["azuredevops:orgServiceUrl"] = serviceUrl,
            };
        }

        public IReadOnlyList<string> DescribeConfigurationProblems(IacConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            List<string> problems = [];

            if (string.IsNullOrWhiteSpace(configuration.Project))
            {
                problems.Add(
                    "'project' is required for provider 'azuredevops': repositories live inside a "
                    + "project, and this tool does not create projects.");
            }

            return problems;
        }

        public IReadOnlyList<string> DescribeRepositoryProblems(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            // Azure DevOps takes identities, and a GitHub-shaped approver is reported as a
            // warning by DescribeUnsupportedSettings rather than an error: a bare '@user'
            // still strips to a plausible identity, so refusing the run would be too strict.
            return [];
        }

        public IReadOnlyList<string> DescribeUnsupportedSettings(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            List<string> warnings = [];

            if (repository.Topics.Count > 0)
            {
                warnings.Add("topics are a GitHub concept and are ignored; Azure DevOps has no repository tags.");
            }

            if (repository.Features.Issues || repository.Features.Wiki
                || repository.Features.Projects || repository.Features.Discussions)
            {
                warnings.Add(
                    "features.* are ignored: Azure DevOps provides Boards and Wiki per project, not per "
                    + "repository.");
            }

            warnings.Add(
                $"visibility '{repository.Visibility}' is ignored: Azure DevOps sets visibility on the "
                + "project, and every repository in it inherits that.");

            if (repository.License is not null || repository.GitignoreTemplate is not null)
            {
                warnings.Add(
                    "license and gitignoreTemplate are GitHub repository-creation templates with no "
                    + "Azure DevOps equivalent; add those files yourself.");
            }

            if (repository.Security.SecretScanning || repository.Security.SecretScanningPushProtection
                || repository.Security.VulnerabilityAlerts)
            {
                warnings.Add(
                    "security.* maps to Advanced Security for Azure DevOps, which is licensed and "
                    + "enabled separately and is not configured here.");
            }

            if (repository.Files.Codeowners && repository.Approvers.Count > 0)
            {
                warnings.Add(
                    "Azure DevOps has no CODEOWNERS file. 'approvers' is applied as an automatic-reviewers "
                    + "branch policy instead, which needs Azure DevOps identity ids or emails - a GitHub "
                    + "'@org/team' value will not resolve.");
            }

            IEnumerable<string> githubShaped = repository.Approvers
                .Where(static approver => approver.Contains('/', StringComparison.Ordinal));

            foreach (string approver in githubShaped)
            {
                warnings.Add(
                    $"approver '{approver}' looks like a GitHub team reference. Azure DevOps needs a user "
                    + "identity id or email address here.");
            }

            if (repository.Ruleset.RequiredStatusChecks.Count > 0)
            {
                warnings.Add(
                    "ruleset.requiredStatusChecks names checks by string, which Azure DevOps cannot use - "
                    + "its build-validation policy references a pipeline by numeric id. Use "
                    + "ruleset.buildValidationPipelineIds instead.");
            }

            if (repository.Ruleset.RequireSignedCommits)
            {
                warnings.Add(
                    "ruleset.requireSignedCommits has no Azure DevOps branch-policy equivalent and is "
                    + "ignored.");
            }

            if (repository.Ruleset.BlockForcePush || repository.Ruleset.BlockDeletion)
            {
                warnings.Add(
                    "ruleset.blockForcePush and blockDeletion are branch security permissions in Azure "
                    + "DevOps, not policies, and are not configured here. Note that any blocking policy "
                    + "already prevents direct pushes to the branch.");
            }

            if (repository.Ruleset.AllowAdminBypass || repository.Ruleset.BypassActors.Count > 0)
            {
                warnings.Add(
                    "ruleset bypass is a permission in Azure DevOps ('Bypass policies when completing "
                    + "pull requests'), granted to identities outside this configuration.");
            }

            if (repository.Ruleset.RequiredReviewers.Count > 0)
            {
                warnings.Add(
                    "ruleset.requiredReviewers uses GitHub numeric team ids and is ignored. Use "
                    + "'approvers' with Azure DevOps identities, which becomes an automatic-reviewers "
                    + "policy.");
            }

            if (string.Equals(repository.Ruleset.Enforcement, "evaluate", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    "ruleset.enforcement 'evaluate' has no Azure DevOps equivalent. The policies are "
                    + "created non-blocking instead, which reports but does not prevent completion.");
            }

            if (repository.ArchiveOnDestroy)
            {
                warnings.Add(
                    "archiveOnDestroy is ignored: Azure DevOps has no archive. Destroy disables the "
                    + "repository instead, which is reversible.");
            }

            if (repository.OwnerType == RepositoryOwnerType.User)
            {
                warnings.Add(
                    "ownerType 'user' is a GitHub distinction. In Azure DevOps the equivalent of letting "
                    + "one person work alone is ruleset.allowSelfApproval, which is applied here.");
            }

            if (RequiresSelfApproval(repository) && !repository.Ruleset.AllowSelfApproval)
            {
                warnings.Add(
                    "minimumApprovals is 0, which Azure DevOps cannot express - its minimum-reviewers "
                    + "policy requires at least one. Creating it with one reviewer and self-approval "
                    + "enabled, which keeps the branch protected without blocking a lone author.");
            }

            return warnings;
        }

        public void Provision(ProvisioningContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(context.Project))
            {
                throw new ConfigurationException(
                    "Azure DevOps needs a 'project' in the configuration file: repositories live inside "
                    + "a project, and this tool does not create projects.");
            }

            ResolvedRepository definition = context.Repository;
            string projectName = context.Project;

            Output<string> projectId = Ado.GetProject
                .Invoke(new Ado.GetProjectInvokeArgs { Name = projectName })
                .Apply(static project => project.Id);

            Ado.Git repository = new(
                definition.Name,
                new Ado.GitArgs
                {
                    Name = definition.Name,
                    ProjectId = projectId,
                    DefaultBranch = "refs/heads/" + definition.DefaultBranch,
                    Initialization = new Ado.Inputs.GitInitializationArgs
                    {
                        // A branch must exist before a policy can be scoped to it.
                        InitType = "Clean",
                    },
                });

            // 'evaluate' has no Azure DevOps analogue; the closest is a non-blocking policy,
            // which records violations without preventing completion.
            bool blocking = !string.Equals(
                definition.Ruleset.Enforcement, "evaluate", StringComparison.OrdinalIgnoreCase);
            bool enabled = !string.Equals(
                definition.Ruleset.Enforcement, "disabled", StringComparison.OrdinalIgnoreCase);

            if (definition.Ruleset.RequirePullRequest)
            {
                ProvisionMinimumReviewers(definition, projectId, repository, enabled, blocking);
            }

            if (definition.Ruleset.RequireConversationResolution)
            {
                _ = new Ado.BranchPolicyCommentResolution(
                    $"{definition.Name}-comment-resolution",
                    new Ado.BranchPolicyCommentResolutionArgs
                    {
                        ProjectId = projectId,
                        Enabled = enabled,
                        Blocking = blocking,
                        Settings = new Ado.Inputs.BranchPolicyCommentResolutionSettingsArgs
                        {
                            Scopes =
                            [
                                new Ado.Inputs.BranchPolicyCommentResolutionSettingsScopeArgs
                                {
                                    RepositoryId = repository.Id,
                                    MatchType = DefaultBranchMatch,
                                },
                            ],
                        },
                    });
            }

            ProvisionMergeTypes(definition, projectId, repository, enabled, blocking);

            if (definition.Approvers.Count > 0)
            {
                ProvisionAutoReviewers(definition, projectId, repository, enabled, blocking);
            }

            foreach (int pipelineId in definition.Ruleset.BuildValidationPipelineIds)
            {
                _ = new Ado.BranchPolicyBuildValidation(
                    $"{definition.Name}-build-validation-{pipelineId.ToString(CultureInfo.InvariantCulture)}",
                    new Ado.BranchPolicyBuildValidationArgs
                    {
                        ProjectId = projectId,
                        Enabled = enabled,
                        Blocking = blocking,
                        Settings = new Ado.Inputs.BranchPolicyBuildValidationSettingsArgs
                        {
                            DisplayName = "Build validation",
                            BuildDefinitionId = pipelineId,
                            ValidDuration = 720,
                            QueueOnSourceUpdateOnly = true,
                            ManualQueueOnly = false,
                            Scopes =
                            [
                                new Ado.Inputs.BranchPolicyBuildValidationSettingsScopeArgs
                                {
                                    RepositoryId = repository.Id,
                                    MatchType = DefaultBranchMatch,
                                },
                            ],
                        },
                    });
            }
        }

        /// <summary>
        /// Azure DevOps has no "require a pull request" flag - a blocking policy on the branch
        /// is what forces one. Its minimum-reviewers policy also insists on at least one
        /// reviewer, so a configured zero becomes one reviewer plus self-approval: the branch
        /// stays protected and a lone author is not locked out.
        /// </summary>
        private static void ProvisionMinimumReviewers(
            ResolvedRepository definition,
            Output<string> projectId,
            Ado.Git repository,
            bool enabled,
            bool blocking)
        {
            int reviewerCount = Math.Max(definition.Ruleset.MinimumApprovals, 1);
            bool submitterCanVote =
                definition.Ruleset.AllowSelfApproval || RequiresSelfApproval(definition);

            _ = new Ado.BranchPolicyMinReviewers(
                $"{definition.Name}-min-reviewers",
                new Ado.BranchPolicyMinReviewersArgs
                {
                    ProjectId = projectId,
                    Enabled = enabled,
                    Blocking = blocking,
                    Settings = new Ado.Inputs.BranchPolicyMinReviewersSettingsArgs
                    {
                        ReviewerCount = reviewerCount,
                        SubmitterCanVote = submitterCanVote,
                        LastPusherCannotApprove = definition.Ruleset.RequireLastPushApproval,
                        OnPushResetApprovedVotes = definition.Ruleset.DismissStaleReviewsOnPush,
                        AllowCompletionWithRejectsOrWaits = false,
                        Scopes =
                        [
                            new Ado.Inputs.BranchPolicyMinReviewersSettingsScopeArgs
                            {
                                RepositoryId = repository.Id,
                                MatchType = DefaultBranchMatch,
                            },
                        ],
                    },
                });
        }

        /// <summary>
        /// Azure DevOps expresses linear history by restricting merge strategies rather than
        /// with a dedicated rule, so the merge settings and requireLinearHistory both land here.
        /// </summary>
        private static void ProvisionMergeTypes(
            ResolvedRepository definition,
            Output<string> projectId,
            Ado.Git repository,
            bool enabled,
            bool blocking)
        {
            bool allowMergeCommit = definition.Merge.AllowMergeCommit
                && !definition.Ruleset.RequireLinearHistory;

            _ = new Ado.BranchPolicyMergeTypes(
                $"{definition.Name}-merge-types",
                new Ado.BranchPolicyMergeTypesArgs
                {
                    ProjectId = projectId,
                    Enabled = enabled,
                    Blocking = blocking,
                    Settings = new Ado.Inputs.BranchPolicyMergeTypesSettingsArgs
                    {
                        AllowSquash = definition.Merge.AllowSquash,
                        AllowBasicNoFastForward = allowMergeCommit,
                        AllowRebaseAndFastForward = definition.Merge.AllowRebase,
                        AllowRebaseWithMerge = false,
                        Scopes =
                        [
                            new Ado.Inputs.BranchPolicyMergeTypesSettingsScopeArgs
                            {
                                RepositoryId = repository.Id,
                                MatchType = DefaultBranchMatch,
                            },
                        ],
                    },
                });
        }

        /// <summary>
        /// The Azure DevOps stand-in for CODEOWNERS: an automatic-reviewers policy that adds
        /// the named identities to every pull request on the branch.
        /// </summary>
        private static void ProvisionAutoReviewers(
            ResolvedRepository definition,
            Output<string> projectId,
            Ado.Git repository,
            bool enabled,
            bool blocking)
        {
            // Identities are taken as configured, minus any leading '@' carried over from
            // GitHub-shaped configuration.
            string[] reviewerIds = definition.Approvers
                .Select(static approver => approver.TrimStart('@'))
                .ToArray();

            _ = new Ado.BranchPolicyAutoReviewers(
                $"{definition.Name}-auto-reviewers",
                new Ado.BranchPolicyAutoReviewersArgs
                {
                    ProjectId = projectId,
                    Enabled = enabled,

                    // Blocking only when the configuration actually requires code-owner
                    // approval; otherwise the reviewers are added but not mandatory.
                    Blocking = blocking && definition.Ruleset.RequireCodeOwnerReview,
                    Settings = new Ado.Inputs.BranchPolicyAutoReviewersSettingsArgs
                    {
                        AutoReviewerIds = reviewerIds,
                        MinimumNumberOfReviewers = Math.Max(definition.Ruleset.MinimumApprovals, 1),
                        SubmitterCanVote =
                            definition.Ruleset.AllowSelfApproval || RequiresSelfApproval(definition),
                        Message = "Added by the iac CLI from the configuration's approvers list.",
                        Scopes =
                        [
                            new Ado.Inputs.BranchPolicyAutoReviewersSettingsScopeArgs
                            {
                                RepositoryId = repository.Id,
                                MatchType = DefaultBranchMatch,
                            },
                        ],
                    },
                });
        }
    }
}
