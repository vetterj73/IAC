using System;
using System.Collections.Generic;
using System.Linq;

using Iac.Provisioning.Configuration;

using Pulumi;

using Github = Pulumi.Github;

namespace Iac.Provisioning.GitHub
{
    /// <summary>
    /// Declares a GitHub repository, its default branch, its default-branch ruleset, its
    /// CODEOWNERS file and its collaborators.
    /// </summary>
    /// <remarks>
    /// Every value written here comes from the resolved configuration, so a second run with
    /// the same file produces no changes - Pulumi compares the declared state against what it
    /// recorded last time.
    /// </remarks>
    public sealed class GitHubRepositoryProvisioner : IResourceProvisioner
    {
        /// <summary>Ruleset name as it appears in the repository's Rules settings page.</summary>
        private const string RulesetName = "default-branch-protection";

        /// <summary>
        /// GitHub's placeholder for "whatever the default branch is", so the ruleset does not
        /// have to be rewritten if the default branch is renamed.
        /// </summary>
        private const string DefaultBranchToken = "~DEFAULT_BRANCH";

        /// <summary>Seeded pull request template, when the configuration asks for one.</summary>
        private const string PullRequestTemplate = """
            ## What changed

            ## Why

            ## How this was verified
            """;

        public string ProviderName => "github";

        public string PulumiPluginName => "github";

        public IReadOnlyList<string> RequiredEnvironmentVariables => ["GITHUB_TOKEN"];

        public IReadOnlyDictionary<string, string> BuildStackConfiguration(string organization)
        {
            ArgumentNullException.ThrowIfNull(organization);

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["github:owner"] = organization,
            };
        }

        public IReadOnlyList<string> DescribeConfigurationProblems(IacConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            // GitHub has no project layer; a 'project' key is simply unused rather than wrong.
            return [];
        }

        public IReadOnlyList<string> DescribeRepositoryProblems(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            List<string> problems = [];

            // CODEOWNERS silently ignores an owner without a leading '@'. Writing one would
            // produce a file that looks right and enforces nothing, so it is an error rather
            // than a warning. Only checked when a CODEOWNERS file is actually written.
            if (repository.Files.Codeowners)
            {
                IEnumerable<string> malformed = repository.Approvers
                    .Where(static approver => !approver.StartsWith('@'));

                foreach (string approver in malformed)
                {
                    problems.Add(
                        $"approver '{approver}' must be '@user' or '@org/team': CODEOWNERS ignores an "
                        + "owner without the leading '@', which would leave the review requirement "
                        + "unenforced. (An Azure DevOps identity or email does not work here.)");
                }
            }

            return problems;
        }

        public IReadOnlyList<string> DescribeUnsupportedSettings(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            List<string> warnings = [];

            bool isPrivate = !string.Equals(repository.Visibility, "public", StringComparison.OrdinalIgnoreCase);
            if (isPrivate && (repository.Security.SecretScanning || repository.Security.SecretScanningPushProtection))
            {
                warnings.Add(
                    "security.secretScanning / secretScanningPushProtection on a non-public repository "
                    + "requires GitHub Advanced Security on the organization; without it GitHub rejects "
                    + "the setting.");
            }

            if (repository.Ruleset.RequiredStatusChecks.Count > 0)
            {
                warnings.Add(
                    "ruleset.requiredStatusChecks names checks that must already have reported at "
                    + "least once. On a repository with no workflow runs yet, GitHub will reject them - "
                    + "see docs/git for the two-pass flow.");
            }

            if (repository.Ruleset.RequiredReviewers.Count > 0)
            {
                warnings.Add(
                    "ruleset.requiredReviewers uses a GitHub ruleset rule that the provider documents "
                    + "as beta and subject to change. 'approvers' plus requireCodeOwnerReview is the "
                    + "stable equivalent.");
            }

            if (string.Equals(repository.Ruleset.Enforcement, "evaluate", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    "ruleset.enforcement 'evaluate' records rule violations without blocking, and "
                    + "GitHub only supports it for organization-owned repositories.");
            }

            if (repository.Ruleset.AllowSelfApproval)
            {
                warnings.Add(
                    "ruleset.allowSelfApproval has no effect on GitHub, which never lets the author of "
                    + "a pull request approve it. The equivalent is minimumApprovals: 0, or a "
                    + "bypassActors entry for the author.");
            }

            if (repository.Ruleset.BuildValidationPipelineIds.Count > 0)
            {
                warnings.Add(
                    "ruleset.buildValidationPipelineIds is an Azure DevOps setting and is ignored here. "
                    + "On GitHub, name the checks in ruleset.requiredStatusChecks instead.");
            }

            if (repository.OwnerType == RepositoryOwnerType.User
                && repository.Ruleset.RequirePullRequest
                && repository.Ruleset.MinimumApprovals == 0)
            {
                warnings.Add(
                    "this is a user-owned repository with minimumApprovals: 0, so a pull request is "
                    + "required but the owner can merge it unreviewed. That is the only workable "
                    + "arrangement for a single-person repository.");
            }

            return warnings;
        }

        public void Provision(ProvisioningContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ResolvedRepository definition = context.Repository;

            Github.RepositoryArgs repositoryArgs = new()
            {
                Name = definition.Name,
                // Lowercase: the provider validates these values case-sensitively.
                Visibility = definition.Visibility.ToLowerInvariant(),
                Topics = definition.Topics.ToArray(),

                // Without an initial commit there is no default branch, so neither the
                // ruleset nor the CODEOWNERS commit would have anything to attach to.
                AutoInit = true,

                HasIssues = definition.Features.Issues,
                HasWiki = definition.Features.Wiki,
                HasProjects = definition.Features.Projects,
                HasDiscussions = definition.Features.Discussions,

                AllowSquashMerge = definition.Merge.AllowSquash,
                AllowMergeCommit = definition.Merge.AllowMergeCommit,
                AllowRebaseMerge = definition.Merge.AllowRebase,
                AllowAutoMerge = definition.Merge.AllowAutoMerge,
                AllowUpdateBranch = definition.Merge.AllowUpdateBranch,
                DeleteBranchOnMerge = definition.Merge.DeleteBranchOnMerge,

                VulnerabilityAlerts = definition.Security.VulnerabilityAlerts,

                // Archive rather than delete on destroy: a deleted repository takes its
                // history with it, an archived one can be restored.
                ArchiveOnDestroy = definition.ArchiveOnDestroy,
            };

            // Pulumi's Input<T> has no implicit conversion from a null, and an empty string is
            // not the same as "unset" to GitHub, so optional values are assigned only when set.
            if (definition.Description is not null)
            {
                repositoryArgs.Description = definition.Description;
            }

            if (definition.Homepage is not null)
            {
                repositoryArgs.HomepageUrl = definition.Homepage;
            }

            if (definition.GitignoreTemplate is not null)
            {
                repositoryArgs.GitignoreTemplate = definition.GitignoreTemplate;
            }

            if (definition.License is not null)
            {
                repositoryArgs.LicenseTemplate = definition.License;
            }

            Github.Inputs.RepositorySecurityAndAnalysisArgs? securityAndAnalysis =
                BuildSecurityAndAnalysis(definition);
            if (securityAndAnalysis is not null)
            {
                repositoryArgs.SecurityAndAnalysis = securityAndAnalysis;
            }

            Github.Repository repository = new(definition.Name, repositoryArgs);

            // Repository.DefaultBranch is deprecated in the provider; BranchDefault is the
            // supported route. Rename handles organizations whose initial branch is 'master'.
            Github.BranchDefault defaultBranch = new(
                $"{definition.Name}-default-branch",
                new Github.BranchDefaultArgs
                {
                    Repository = repository.Name,
                    Branch = definition.DefaultBranch,
                    Rename = true,
                });

            CustomResourceOptions afterDefaultBranch = new()
            {
                DependsOn = { defaultBranch },
            };

            if (definition.Files.Codeowners && definition.Approvers.Count > 0)
            {
                _ = new Github.RepositoryFile(
                    $"{definition.Name}-codeowners",
                    new Github.RepositoryFileArgs
                    {
                        Repository = repository.Name,
                        Branch = definition.DefaultBranch,
                        File = CodeownersFile.Path,
                        Content = CodeownersFile.Build(definition.Approvers),
                        CommitMessage = "chore: set CODEOWNERS (managed by iac)",
                        OverwriteOnCreate = true,
                    },
                    afterDefaultBranch);
            }

            if (definition.Files.PullRequestTemplate)
            {
                _ = new Github.RepositoryFile(
                    $"{definition.Name}-pull-request-template",
                    new Github.RepositoryFileArgs
                    {
                        Repository = repository.Name,
                        Branch = definition.DefaultBranch,
                        File = ".github/pull_request_template.md",
                        Content = PullRequestTemplate,
                        CommitMessage = "chore: add pull request template (managed by iac)",
                        OverwriteOnCreate = true,
                    },
                    afterDefaultBranch);
            }

            _ = new Github.RepositoryRuleset(
                $"{definition.Name}-ruleset",
                new Github.RepositoryRulesetArgs
                {
                    Name = RulesetName,
                    Repository = repository.Name,
                    Target = "branch",
                    Enforcement = definition.Ruleset.Enforcement.ToLowerInvariant(),
                    Conditions = new Github.Inputs.RepositoryRulesetConditionsArgs
                    {
                        RefName = new Github.Inputs.RepositoryRulesetConditionsRefNameArgs
                        {
                            Includes = new[] { DefaultBranchToken },
                            Excludes = Array.Empty<string>(),
                        },
                    },
                    Rules = BuildRules(definition),
                    BypassActors = BuildBypassActors(definition),
                },
                afterDefaultBranch);

            if (definition.UserCollaborators.Count > 0 || definition.TeamCollaborators.Count > 0)
            {
                _ = new Github.RepositoryCollaborators(
                    $"{definition.Name}-collaborators",
                    new Github.RepositoryCollaboratorsArgs
                    {
                        Repository = repository.Name,
                        Users = definition.UserCollaborators
                            .Select(static user => new Github.Inputs.RepositoryCollaboratorsUserArgs
                            {
                                Username = user.Name,
                                Permission = user.Permission,
                            })
                            .ToList(),
                        Teams = definition.TeamCollaborators
                            .Select(static team => new Github.Inputs.RepositoryCollaboratorsTeamArgs
                            {
                                TeamId = team.Name,
                                Permission = team.Permission,
                            })
                            .ToList(),
                    });
            }
        }

        private static Github.Inputs.RepositorySecurityAndAnalysisArgs? BuildSecurityAndAnalysis(
            ResolvedRepository definition)
        {
            // Sending this block at all on a repository without Advanced Security is rejected,
            // so omit it entirely unless something is actually being switched on.
            if (!definition.Security.SecretScanning && !definition.Security.SecretScanningPushProtection)
            {
                return null;
            }

            return new Github.Inputs.RepositorySecurityAndAnalysisArgs
            {
                SecretScanning = new Github.Inputs.RepositorySecurityAndAnalysisSecretScanningArgs
                {
                    Status = ToStatus(definition.Security.SecretScanning),
                },
                SecretScanningPushProtection =
                    new Github.Inputs.RepositorySecurityAndAnalysisSecretScanningPushProtectionArgs
                    {
                        Status = ToStatus(definition.Security.SecretScanningPushProtection),
                    },
            };
        }

        private static Github.Inputs.RepositoryRulesetRulesArgs BuildRules(ResolvedRepository definition)
        {
            ResolvedRuleset ruleset = definition.Ruleset;

            Github.Inputs.RepositoryRulesetRulesArgs rules = new()
            {
                Deletion = ruleset.BlockDeletion,
                NonFastForward = ruleset.BlockForcePush,
                RequiredLinearHistory = ruleset.RequireLinearHistory,
                RequiredSignatures = ruleset.RequireSignedCommits,
            };

            if (ruleset.RequirePullRequest)
            {
                Github.Inputs.RepositoryRulesetRulesPullRequestArgs pullRequest = new()
                {
                    RequiredApprovingReviewCount = ruleset.MinimumApprovals,
                    DismissStaleReviewsOnPush = ruleset.DismissStaleReviewsOnPush,
                    RequireCodeOwnerReview = ruleset.RequireCodeOwnerReview,
                    RequireLastPushApproval = ruleset.RequireLastPushApproval,
                    RequiredReviewThreadResolution = ruleset.RequireConversationResolution,
                    AllowedMergeMethods = BuildAllowedMergeMethods(definition.Merge),
                };

                if (ruleset.RequiredReviewers.Count > 0)
                {
                    pullRequest.RequiredReviewers = ruleset.RequiredReviewers
                        .Select(static reviewer =>
                            new Github.Inputs.RepositoryRulesetRulesPullRequestRequiredReviewerArgs
                            {
                                MinimumApprovals = reviewer.MinimumApprovals,
                                FilePatterns = reviewer.FilePatterns.ToArray(),
                                Reviewer =
                                    new Github.Inputs.RepositoryRulesetRulesPullRequestRequiredReviewerReviewerArgs
                                    {
                                        Id = reviewer.Id,
                                        Type = reviewer.Type,
                                    },
                            })
                        .ToList();
                }

                rules.PullRequest = pullRequest;
            }

            if (ruleset.RequiredStatusChecks.Count > 0)
            {
                rules.RequiredStatusChecks = new Github.Inputs.RepositoryRulesetRulesRequiredStatusChecksArgs
                {
                    StrictRequiredStatusChecksPolicy = true,
                    RequiredChecks = ruleset.RequiredStatusChecks
                        .Select(static check =>
                            new Github.Inputs.RepositoryRulesetRulesRequiredStatusChecksRequiredCheckArgs
                            {
                                Context = check,
                            })
                        .ToList(),
                };
            }

            return rules;
        }

        private static List<Github.Inputs.RepositoryRulesetBypassActorArgs> BuildBypassActors(
            ResolvedRepository definition)
        {
            List<Github.Inputs.RepositoryRulesetBypassActorArgs> actors = [];

            if (definition.Ruleset.AllowAdminBypass)
            {
                // OrganizationAdmin carries no actor id - GitHub ignores one if sent.
                actors.Add(new Github.Inputs.RepositoryRulesetBypassActorArgs
                {
                    ActorType = "OrganizationAdmin",
                    BypassMode = "always",
                });
            }

            foreach (ResolvedBypassActor actor in definition.Ruleset.BypassActors)
            {
                Github.Inputs.RepositoryRulesetBypassActorArgs args = new()
                {
                    ActorType = CanonicalActorType(actor.ActorType),
                    BypassMode = actor.BypassMode.ToLowerInvariant(),
                };

                // Assigned only when present: Input<int> cannot take a null, and the id-less
                // actor types must not carry one.
                if (actor.ActorId is int actorId)
                {
                    args.ActorId = actorId;
                }

                actors.Add(args);
            }

            return actors;
        }

        /// <summary>
        /// The provider validates actor types case-sensitively in CamelCase, while the rest of
        /// the configuration is lowercase. Accept any casing from the file and emit the exact
        /// spelling the provider wants.
        /// </summary>
        private static string CanonicalActorType(string actorType)
        {
            string[] canonical =
            [
                "RepositoryRole", "Team", "Integration", "OrganizationAdmin", "DeployKey",
                "EnterpriseOwner", "User",
            ];

            return canonical.FirstOrDefault(
                candidate => candidate.Equals(actorType, StringComparison.OrdinalIgnoreCase))
                ?? actorType;
        }

        /// <summary>
        /// The ruleset can only allow merge methods the repository itself allows, so these are
        /// derived from the merge settings rather than configured twice.
        /// </summary>
        private static string[] BuildAllowedMergeMethods(ResolvedMerge merge)
        {
            List<string> methods = [];

            if (merge.AllowMergeCommit)
            {
                methods.Add("merge");
            }

            if (merge.AllowSquash)
            {
                methods.Add("squash");
            }

            if (merge.AllowRebase)
            {
                methods.Add("rebase");
            }

            return [.. methods];
        }

        private static string ToStatus(bool enabled)
        {
            return enabled ? "enabled" : "disabled";
        }
    }
}
