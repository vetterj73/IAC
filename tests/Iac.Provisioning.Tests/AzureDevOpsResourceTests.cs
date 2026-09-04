using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Iac.Provisioning;
using Iac.Provisioning.AzureDevOps;
using Iac.Provisioning.Configuration;

using Pulumi;

using Shouldly;

using Xunit;

using Ado = Pulumi.AzureDevOps;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Exercises the branch policies the Azure DevOps provisioner declares. Azure DevOps has
    /// no single ruleset object, so one GitHub ruleset becomes several policy resources - and
    /// the mapping between them is what these tests pin down.
    /// </summary>
    [Collection(PulumiDeploymentCollection.Name)]
    public class AzureDevOpsResourceTests
    {
        private static async Task<ImmutableArray<Resource>> DeclareAsync(
            RepositoryOptions options,
            RepositoryOwnerType ownerType = RepositoryOwnerType.Organization)
        {
            ResolvedRepository repository = RepositoryResolver.Resolve(null, options, ownerType);
            AzureDevOpsRepositoryProvisioner provisioner = new();

            ProvisioningContext context = new()
            {
                Organization = "contoso",
                Project = "example-project",
                Repository = repository,
            };

            return await PulumiTestExtensions.DeclareAsync(() => provisioner.Provision(context));
        }

        [Fact]
        public async Task A_default_repository_declares_a_repository_and_its_policies()
        {
            ImmutableArray<Resource> resources =
                await DeclareAsync(new RepositoryOptions { Name = "widget-api" });

            resources.OfType<Ado.Git>().Count().ShouldBe(1);
            resources.OfType<Ado.BranchPolicyMinReviewers>().Count().ShouldBe(1);
            resources.OfType<Ado.BranchPolicyCommentResolution>().Count().ShouldBe(1);
            resources.OfType<Ado.BranchPolicyMergeTypes>().Count().ShouldBe(1);

            // No approvers and no pipelines configured.
            resources.OfType<Ado.BranchPolicyAutoReviewers>().ShouldBeEmpty();
            resources.OfType<Ado.BranchPolicyBuildValidation>().ShouldBeEmpty();
        }

        [Fact]
        public async Task The_repository_is_created_with_a_qualified_default_branch_ref()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                DefaultBranch = "main",
            });

            Ado.Git repository = resources.OfType<Ado.Git>().Single();

            // Azure DevOps takes a ref, not a branch name.
            (await repository.DefaultBranch.GetValueAsync()).ShouldBe("refs/heads/main");
            (await repository.ProjectId.GetValueAsync()).ShouldBe(ProviderMocks.ProjectId);

            Ado.Outputs.GitInitialization initialization =
                await repository.Initialization.GetValueAsync();

            // A branch has to exist before a policy can be scoped to it.
            initialization.InitType.ShouldBe("Clean");
        }

        [Fact]
        public async Task The_minimum_reviewers_policy_carries_the_configured_count()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { MinimumApprovals = 2 },
            });

            Ado.Outputs.BranchPolicyMinReviewersSettings settings =
                await resources.OfType<Ado.BranchPolicyMinReviewers>().Single().Settings.GetValueAsync();

            settings.ReviewerCount.ShouldBe(2);
            settings.SubmitterCanVote.ShouldBe(false);
            settings.OnPushResetApprovedVotes.ShouldBe(true);
        }

        [Fact]
        public async Task Zero_approvals_becomes_one_reviewer_who_may_be_the_author()
        {
            // Azure DevOps cannot express "a pull request but no approvals": its policy needs
            // at least one reviewer, and a blocking policy is what forces the pull request in
            // the first place. Self-approval is what keeps a lone author unblocked.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "personal-tools",
                Ruleset = new RulesetOptions { MinimumApprovals = 0 },
            });

            Ado.Outputs.BranchPolicyMinReviewersSettings settings =
                await resources.OfType<Ado.BranchPolicyMinReviewers>().Single().Settings.GetValueAsync();

            settings.ReviewerCount.ShouldBe(1);
            settings.SubmitterCanVote.ShouldBe(true);
        }

        [Fact]
        public async Task A_user_owned_repository_gets_self_approval_by_default()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(
                new RepositoryOptions { Name = "personal-tools" },
                RepositoryOwnerType.User);

            Ado.Outputs.BranchPolicyMinReviewersSettings settings =
                await resources.OfType<Ado.BranchPolicyMinReviewers>().Single().Settings.GetValueAsync();

            settings.ReviewerCount.ShouldBe(1);
            settings.SubmitterCanVote.ShouldBe(true);
        }

        [Fact]
        public async Task Self_approval_can_be_asked_for_explicitly()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { MinimumApprovals = 1, AllowSelfApproval = true },
            });

            Ado.Outputs.BranchPolicyMinReviewersSettings settings =
                await resources.OfType<Ado.BranchPolicyMinReviewers>().Single().Settings.GetValueAsync();

            settings.ReviewerCount.ShouldBe(1);
            settings.SubmitterCanVote.ShouldBe(true);
        }

        [Fact]
        public async Task No_reviewer_policy_is_declared_when_no_pull_request_is_required()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { RequirePullRequest = false },
            });

            resources.OfType<Ado.BranchPolicyMinReviewers>().ShouldBeEmpty();
        }

        [Fact]
        public async Task Merge_types_mirror_the_merge_settings()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Merge = new MergeOptions
                {
                    AllowSquash = true,
                    AllowMergeCommit = true,
                    AllowRebase = true,
                },
            });

            Ado.Outputs.BranchPolicyMergeTypesSettings settings =
                await resources.OfType<Ado.BranchPolicyMergeTypes>().Single().Settings.GetValueAsync();

            settings.AllowSquash.ShouldBe(true);
            settings.AllowBasicNoFastForward.ShouldBe(true);
            settings.AllowRebaseAndFastForward.ShouldBe(true);
        }

        [Fact]
        public async Task Linear_history_removes_the_merge_commit_strategy()
        {
            // Azure DevOps expresses linear history by restricting merge strategies rather
            // than with a dedicated rule.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Merge = new MergeOptions { AllowSquash = true, AllowMergeCommit = true },
                Ruleset = new RulesetOptions { RequireLinearHistory = true },
            });

            Ado.Outputs.BranchPolicyMergeTypesSettings settings =
                await resources.OfType<Ado.BranchPolicyMergeTypes>().Single().Settings.GetValueAsync();

            settings.AllowBasicNoFastForward.ShouldBe(false);
            settings.AllowSquash.ShouldBe(true);
        }

        [Fact]
        public async Task Comment_resolution_is_only_declared_when_required()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { RequireConversationResolution = false },
            });

            resources.OfType<Ado.BranchPolicyCommentResolution>().ShouldBeEmpty();
        }

        [Fact]
        public async Task Approvers_become_an_automatic_reviewers_policy()
        {
            // The Azure DevOps stand-in for CODEOWNERS, which does not exist there.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@octocat", "reviewer@contoso.com"],
                Ruleset = new RulesetOptions { RequireCodeOwnerReview = true },
            });

            Ado.BranchPolicyAutoReviewers policy =
                resources.OfType<Ado.BranchPolicyAutoReviewers>().Single();

            Ado.Outputs.BranchPolicyAutoReviewersSettings settings =
                await policy.Settings.GetValueAsync();

            // The leading '@' is GitHub syntax and is stripped for Azure DevOps identities.
            settings.AutoReviewerIds.ShouldBe(new[] { "octocat", "reviewer@contoso.com" });
            (await policy.Blocking.GetValueAsync()).ShouldBe(true);
        }

        [Fact]
        public async Task Automatic_reviewers_are_advisory_when_code_owner_review_is_not_required()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@octocat"],
                Ruleset = new RulesetOptions { RequireCodeOwnerReview = false },
            });

            Ado.BranchPolicyAutoReviewers policy =
                resources.OfType<Ado.BranchPolicyAutoReviewers>().Single();

            (await policy.Blocking.GetValueAsync()).ShouldBe(false);
        }

        [Fact]
        public async Task Each_build_validation_pipeline_becomes_its_own_policy()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { BuildValidationPipelineIds = [11, 22] },
            });

            ImmutableArray<Ado.BranchPolicyBuildValidation> policies =
                [.. resources.OfType<Ado.BranchPolicyBuildValidation>()];

            policies.Length.ShouldBe(2);

            int[] ids = await Task.WhenAll(policies
                .Select(async policy => (await policy.Settings.GetValueAsync()).BuildDefinitionId));

            ids.ShouldBe(new[] { 11, 22 }, ignoreOrder: true);
        }

        [Fact]
        public async Task Evaluate_enforcement_produces_non_blocking_policies()
        {
            // The closest Azure DevOps equivalent of GitHub's evaluate mode.
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { Enforcement = "evaluate" },
            });

            Ado.BranchPolicyMinReviewers policy =
                resources.OfType<Ado.BranchPolicyMinReviewers>().Single();

            (await policy.Enabled.GetValueAsync()).ShouldBe(true);
            (await policy.Blocking.GetValueAsync()).ShouldBe(false);
        }

        [Fact]
        public async Task Disabled_enforcement_produces_disabled_policies()
        {
            ImmutableArray<Resource> resources = await DeclareAsync(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { Enforcement = "disabled" },
            });

            Ado.BranchPolicyMinReviewers policy =
                resources.OfType<Ado.BranchPolicyMinReviewers>().Single();

            (await policy.Enabled.GetValueAsync()).ShouldBe(false);
        }

        [Fact]
        public async Task Provisioning_without_a_project_fails_with_an_explanation()
        {
            // DescribeConfigurationProblems normally catches this before Pulumi is started;
            // this is the defence in depth. Pulumi wraps anything the program throws in a
            // RunException, so the explanation arrives nested rather than as the top exception.
            AzureDevOpsRepositoryProvisioner provisioner = new();
            ProvisioningContext context = new()
            {
                Organization = "contoso",
                Project = null,
                Repository = RepositoryResolver.Resolve(null, new RepositoryOptions { Name = "widget-api" }),
            };

            RunException exception = await Should.ThrowAsync<RunException>(
                () => PulumiTestExtensions.DeclareAsync(() => provisioner.Provision(context)));

            exception.ToString().ShouldContain("needs a 'project'");
        }
    }
}
