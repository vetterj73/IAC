using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Iac.Provisioning;
using Iac.Provisioning.Configuration;
using Iac.Provisioning.GitHub;

using Pulumi;

using Shouldly;

using Xunit;

using Github = Pulumi.Github;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Exercises the resources the GitHub provisioner actually declares, against Pulumi's
    /// mocked engine. This is the layer that a configuration test cannot reach: it catches a
    /// setting that is read correctly but wired to the wrong resource property.
    /// </summary>
    [Collection(PulumiDeploymentCollection.Name)]
    public class GitHubResourceTests
    {
        private static async Task<ImmutableArray<Resource>> DeclareAsync(
            RepositoryOptions options,
            RepositoryOwnerType ownerType = RepositoryOwnerType.Organization)
        {
            ResolvedRepository repository = RepositoryResolver.Resolve(null, options, ownerType);
            GitHubRepositoryProvisioner provisioner = new();

            ProvisioningContext context = new()
            {
                Organization = "contoso",
                Repository = repository,
            };

            return await PulumiTestExtensions.DeclareAsync(() => provisioner.Provision(context));
        }

        [Fact]
        public async Task A_default_repository_declares_the_expected_resources()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform"],
            });

            resources.OfType<Github.Repository>().Count().ShouldBe(1);
            resources.OfType<Github.BranchDefault>().Count().ShouldBe(1);
            resources.OfType<Github.RepositoryRuleset>().Count().ShouldBe(1);
            resources.OfType<Github.RepositoryFile>().Count().ShouldBe(1);

            // No collaborators configured, so no collaborators resource at all - an empty one
            // would take ownership of the repository's collaborator list.
            resources.OfType<Github.RepositoryCollaborators>().ShouldBeEmpty();
        }

        [Fact]
        public async Task Visibility_is_sent_lowercase_because_the_provider_is_case_sensitive()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Visibility = "PRIVATE",
            });

            Github.Repository repository = resources.OfType<Github.Repository>().Single();

            (await repository.Visibility.GetValueAsync()).ShouldBe("private");
        }

        [Fact]
        public async Task The_repository_is_initialised_and_archived_on_destroy()
        {
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            Github.Repository repository = resources.OfType<Github.Repository>().Single();

            // Without an initial commit there is no branch for the ruleset to attach to.
            (await repository.AutoInit.GetValueAsync()).ShouldBe(true);
            (await repository.ArchiveOnDestroy.GetValueAsync()).ShouldBe(true);
        }

        [Fact]
        public async Task The_default_branch_is_renamed_rather_than_assumed()
        {
            // Organizations whose initial branch is 'master' must still end up on the
            // configured branch, which is what Rename does.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                DefaultBranch = "trunk",
            });

            Github.BranchDefault branchDefault = resources.OfType<Github.BranchDefault>().Single();

            (await branchDefault.Branch.GetValueAsync()).ShouldBe("trunk");
            (await branchDefault.Rename.GetValueAsync()).ShouldBe(true);
        }

        [Fact]
        public async Task The_ruleset_targets_the_default_branch_token()
        {
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            Github.RepositoryRuleset ruleset = resources.OfType<Github.RepositoryRuleset>().Single();

            (await ruleset.Target.GetValueAsync()).ShouldBe("branch");
            (await ruleset.Enforcement.GetValueAsync()).ShouldBe("active");

            Github.Outputs.RepositoryRulesetConditions? conditions =
                await ruleset.Conditions.GetValueAsync();

            // ~DEFAULT_BRANCH means the ruleset survives a branch rename.
            conditions!.RefName.Includes.ShouldBe(new[] { "~DEFAULT_BRANCH" });
            conditions.RefName.Excludes.ShouldBeEmpty();
        }

        [Fact]
        public async Task The_pull_request_rule_carries_the_configured_review_policy()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform"],
                Ruleset = new RulesetOptions
                {
                    MinimumApprovals = 2,
                    RequireCodeOwnerReview = true,
                    RequireLastPushApproval = true,
                },
            });

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.PullRequest.ShouldNotBeNull();
            rules.PullRequest!.RequiredApprovingReviewCount.ShouldBe(2);
            rules.PullRequest.RequireCodeOwnerReview.ShouldBe(true);
            rules.PullRequest.RequireLastPushApproval.ShouldBe(true);
            rules.PullRequest.DismissStaleReviewsOnPush.ShouldBe(true);
            rules.PullRequest.RequiredReviewThreadResolution.ShouldBe(true);

            rules.NonFastForward.ShouldBe(true);
            rules.Deletion.ShouldBe(true);
        }

        [Fact]
        public async Task No_pull_request_rule_is_declared_when_one_is_not_required()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { RequirePullRequest = false },
            });

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.PullRequest.ShouldBeNull();
        }

        [Fact]
        public async Task Allowed_merge_methods_are_derived_from_the_merge_settings()
        {
            // The ruleset must not allow a merge method the repository itself forbids.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Merge = new MergeOptions
                {
                    AllowSquash = true,
                    AllowMergeCommit = true,
                    AllowRebase = false,
                },
            });

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.PullRequest!.AllowedMergeMethods.ShouldBe(new[] { "merge", "squash" });
        }

        [Fact]
        public async Task Status_checks_are_declared_with_a_strict_policy_when_configured()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { RequiredStatusChecks = ["build", "test"] },
            });

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.RequiredStatusChecks.ShouldNotBeNull();
            rules.RequiredStatusChecks!.StrictRequiredStatusChecksPolicy.ShouldBe(true);
            rules.RequiredStatusChecks.RequiredChecks
                .Select(static check => check.Context)
                .ShouldBe(new[] { "build", "test" });
        }

        [Fact]
        public async Task Status_checks_are_absent_when_none_are_configured()
        {
            // The default must stay empty: a check cannot be required before it has reported.
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.RequiredStatusChecks.ShouldBeNull();
        }

        [Fact]
        public async Task Admin_bypass_is_declared_without_an_actor_id()
        {
            // OrganizationAdmin has no id and GitHub ignores one if sent.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { AllowAdminBypass = true },
            });

            ImmutableArray<Github.Outputs.RepositoryRulesetBypassActor> actors =
                await resources.OfType<Github.RepositoryRuleset>().Single()
                    .BypassActors.GetValueAsync();

            Github.Outputs.RepositoryRulesetBypassActor actor = actors.ShouldHaveSingleItem();
            actor.ActorType.ShouldBe("OrganizationAdmin");
            actor.ActorId.ShouldBeNull();
            actor.BypassMode.ShouldBe("always");
        }

        [Fact]
        public async Task An_explicit_bypass_actor_keeps_its_id_and_is_normalised()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions
                {
                    BypassActors =
                    [
                        new BypassActorOptions
                        {
                            // Deliberately the wrong casing: the provider is case-sensitive, so
                            // the provisioner must normalise it.
                            ActorType = "user",
                            ActorId = 4242,
                            BypassMode = "PULL_REQUEST",
                        },
                    ],
                },
            });

            Github.Outputs.RepositoryRulesetBypassActor actor =
                (await resources.OfType<Github.RepositoryRuleset>().Single()
                    .BypassActors.GetValueAsync()).ShouldHaveSingleItem();

            actor.ActorType.ShouldBe("User");
            actor.ActorId.ShouldBe(4242);
            actor.BypassMode.ShouldBe("pull_request");
        }

        [Fact]
        public async Task Codeowners_is_committed_to_the_default_branch()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform", "@octocat"],
            });

            Github.RepositoryFile file = resources.OfType<Github.RepositoryFile>().Single();

            (await file.File.GetValueAsync()).ShouldBe(".github/CODEOWNERS");
            (await file.Branch.GetValueAsync()).ShouldBe("main");
            (await file.Content.GetValueAsync()).ShouldContain("* @contoso/platform @octocat");
        }

        [Fact]
        public async Task No_codeowners_file_is_committed_when_there_are_no_approvers()
        {
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            resources.OfType<Github.RepositoryFile>().ShouldBeEmpty();
        }

        [Fact]
        public async Task A_pull_request_template_is_committed_when_asked_for()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Files = new GeneratedFileOptions { Codeowners = false, PullRequestTemplate = true },
            });

            Github.RepositoryFile file = resources.OfType<Github.RepositoryFile>().Single();

            (await file.File.GetValueAsync()).ShouldBe(".github/pull_request_template.md");
        }

        [Fact]
        public async Task Collaborators_are_declared_with_their_permissions()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Collaborators = new CollaboratorOptions
                {
                    Users = [new CollaboratorEntry { Name = "octocat", Permission = "maintain" }],
                    Teams = [new CollaboratorEntry { Name = "platform" }],
                },
            });

            Github.RepositoryCollaborators collaborators =
                resources.OfType<Github.RepositoryCollaborators>().Single();

            Github.Outputs.RepositoryCollaboratorsUser user =
                (await collaborators.Users.GetValueAsync()).ShouldHaveSingleItem();
            user.Username.ShouldBe("octocat");
            user.Permission.ShouldBe("maintain");

            Github.Outputs.RepositoryCollaboratorsTeam team =
                (await collaborators.Teams.GetValueAsync()).ShouldHaveSingleItem();
            team.TeamId.ShouldBe("platform");
            team.Permission.ShouldBe("push");
        }

        [Fact]
        public async Task Security_and_analysis_is_omitted_unless_something_is_switched_on()
        {
            // Sending the block at all is rejected on a repository without Advanced Security.
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            Github.Repository repository = resources.OfType<Github.Repository>().Single();

            (await repository.SecurityAndAnalysis.GetValueAsync()).ShouldBeNull();
        }

        [Fact]
        public async Task Secret_scanning_is_declared_as_a_status_when_enabled()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Visibility = "public",
                Security = new SecurityOptions
                {
                    SecretScanning = true,
                    SecretScanningPushProtection = false,
                },
            });

            Github.Outputs.RepositorySecurityAndAnalysis security =
                (await resources.OfType<Github.Repository>().Single()
                    .SecurityAndAnalysis.GetValueAsync())!;

            security.SecretScanning!.Status.ShouldBe("enabled");
            security.SecretScanningPushProtection!.Status.ShouldBe("disabled");
        }

        [Fact]
        public async Task A_user_owned_repository_still_gets_a_protected_default_branch()
        {
            // The point of the ownerType default: a pull request is required, but zero
            // approvals so the sole owner is not locked out.
            ImmutableArray<Resource> resources = await DeclareAsync(
                new RepositoryOptions { Name = "personal-tools" },
                RepositoryOwnerType.User);

            Github.Outputs.RepositoryRulesetRules rules =
                (await resources.OfType<Github.RepositoryRuleset>().Single().Rules.GetValueAsync())!;

            rules.PullRequest.ShouldNotBeNull();
            rules.PullRequest!.RequiredApprovingReviewCount.ShouldBe(0);
            rules.NonFastForward.ShouldBe(true);
        }
    }
}
