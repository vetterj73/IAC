using System.Collections.Generic;
using System.Linq;

using Iac.Provisioning;
using Iac.Provisioning.AzureDevOps;
using Iac.Provisioning.Configuration;
using Iac.Provisioning.GitHub;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// The Azure DevOps provisioner's pure logic: how it authenticates, and what it tells the
    /// operator it cannot honour. The warnings matter as much as the resources - they are what
    /// stops a GitHub-shaped configuration quietly losing policy when pointed at Azure DevOps.
    /// </summary>
    public class AzureDevOpsProvisionerTests
    {
        private static readonly AzureDevOpsRepositoryProvisioner Provisioner = new();

        [Fact]
        public void Both_providers_are_registered_and_resolvable()
        {
            ProvisionerRegistry registry = new(
            [
                new GitHubRepositoryProvisioner(),
                new AzureDevOpsRepositoryProvisioner(),
            ]);

            registry.KnownProviders.OrderBy(static name => name, System.StringComparer.Ordinal)
                .ShouldBe(new[] { "azuredevops", "github" });
            registry.Resolve("AzureDevOps").ProviderName.ShouldBe("azuredevops");
        }

        [Fact]
        public void A_bare_organization_name_becomes_a_dev_azure_com_url()
        {
            IReadOnlyDictionary<string, string> configuration =
                Provisioner.BuildStackConfiguration("contoso");

            configuration["azuredevops:orgServiceUrl"].ShouldBe("https://dev.azure.com/contoso");
        }

        [Fact]
        public void A_full_service_url_is_used_as_given()
        {
            // Azure DevOps Server installations are not on dev.azure.com.
            IReadOnlyDictionary<string, string> configuration =
                Provisioner.BuildStackConfiguration("https://tfs.contoso.local/tfs/DefaultCollection/");

            configuration["azuredevops:orgServiceUrl"]
                .ShouldBe("https://tfs.contoso.local/tfs/DefaultCollection");
        }

        [Fact]
        public void A_missing_project_is_a_configuration_problem()
        {
            IacConfiguration configuration = new()
            {
                Provider = "azuredevops",
                Organization = "contoso",
                Repositories = [new RepositoryOptions { Name = "widget-api" }],
            };

            Provisioner.DescribeConfigurationProblems(configuration)
                .ShouldContain(problem => problem.Contains("'project' is required"));
        }

        [Fact]
        public void A_supplied_project_satisfies_the_provider()
        {
            IacConfiguration configuration = new()
            {
                Provider = "azuredevops",
                Organization = "contoso",
                Project = "example-project",
                Repositories = [new RepositoryOptions { Name = "widget-api" }],
            };

            Provisioner.DescribeConfigurationProblems(configuration).ShouldBeEmpty();
        }

        [Fact]
        public void GitHub_never_requires_a_project()
        {
            IacConfiguration configuration = new()
            {
                Provider = "github",
                Organization = "contoso",
                Repositories = [new RepositoryOptions { Name = "widget-api" }],
            };

            new GitHubRepositoryProvisioner().DescribeConfigurationProblems(configuration).ShouldBeEmpty();
        }

        [Fact]
        public void It_asks_for_a_personal_access_token()
        {
            Provisioner.RequiredEnvironmentVariables.ShouldContain("AZDO_PERSONAL_ACCESS_TOKEN");
        }

        [Fact]
        public void Github_only_settings_are_reported_as_unsupported()
        {
            IReadOnlyList<string> warnings = Warnings(new RepositoryOptions
            {
                Name = "widget-api",
                Topics = ["platform"],
                License = "mit",
                Features = new FeatureOptions { Wiki = true },
                Security = new SecurityOptions { SecretScanning = true },
                Ruleset = new RulesetOptions
                {
                    RequiredStatusChecks = ["build"],
                    RequireSignedCommits = true,
                    AllowAdminBypass = true,
                },
            });

            warnings.ShouldContain(warning => warning.Contains("topics are a GitHub concept"));
            warnings.ShouldContain(warning => warning.Contains("license and gitignoreTemplate"));
            warnings.ShouldContain(warning => warning.Contains("features.* are ignored"));
            warnings.ShouldContain(warning => warning.Contains("Advanced Security for Azure DevOps"));
            warnings.ShouldContain(warning => warning.Contains("buildValidationPipelineIds"));
            warnings.ShouldContain(warning => warning.Contains("requireSignedCommits"));
            warnings.ShouldContain(warning => warning.Contains("bypass is a permission"));
        }

        [Fact]
        public void Visibility_is_always_reported_as_project_level()
        {
            Warnings(new RepositoryOptions { Name = "widget-api" })
                .ShouldContain(warning => warning.Contains("sets visibility on the"));
        }

        [Fact]
        public void A_github_team_approver_is_flagged_as_unresolvable()
        {
            IReadOnlyList<string> warnings = Warnings(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform"],
            });

            warnings.ShouldContain(warning => warning.Contains("looks like a GitHub team reference"));
            warnings.ShouldContain(warning => warning.Contains("no CODEOWNERS file"));
        }

        [Fact]
        public void An_email_approver_is_not_flagged()
        {
            IReadOnlyList<string> warnings = Warnings(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["reviewer@contoso.com"],
            });

            warnings.ShouldNotContain(warning => warning.Contains("looks like a GitHub team reference"));
        }

        [Fact]
        public void Coercing_zero_approvals_is_announced()
        {
            // Silently turning "no approvals" into "one reviewer who may be the author" would
            // be a surprising policy change, so it is reported.
            IReadOnlyList<string> warnings = Warnings(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { MinimumApprovals = 0 },
            });

            warnings.ShouldContain(warning => warning.Contains("Azure DevOps cannot express"));
        }

        [Fact]
        public void Requesting_self_approval_explicitly_is_not_announced_as_a_coercion()
        {
            IReadOnlyList<string> warnings = Warnings(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { MinimumApprovals = 1, AllowSelfApproval = true },
            });

            warnings.ShouldNotContain(warning => warning.Contains("Azure DevOps cannot express"));
        }

        [Theory]
        [InlineData(0, true, true)]
        [InlineData(1, true, false)]
        [InlineData(2, true, false)]
        [InlineData(0, false, false)]
        public void Self_approval_is_needed_only_when_a_pull_request_requires_no_approvals(
            int minimumApprovals,
            bool requirePullRequest,
            bool expected)
        {
            ResolvedRepository repository = RepositoryResolver.Resolve(
                defaults: null,
                new RepositoryOptions
                {
                    Name = "widget-api",
                    Ruleset = new RulesetOptions
                    {
                        MinimumApprovals = minimumApprovals,
                        RequirePullRequest = requirePullRequest,
                    },
                });

            AzureDevOpsRepositoryProvisioner.RequiresSelfApproval(repository).ShouldBe(expected);
        }

        [Fact]
        public void Owner_type_user_is_reported_as_a_github_distinction()
        {
            ResolvedRepository repository = RepositoryResolver.Resolve(
                defaults: null,
                new RepositoryOptions { Name = "personal-tools" },
                RepositoryOwnerType.User);

            Provisioner.DescribeUnsupportedSettings(repository)
                .ShouldContain(warning => warning.Contains("ownerType 'user' is a GitHub distinction"));
        }

        [Fact]
        public void An_email_approver_is_not_a_repository_problem_on_azure_devops()
        {
            // The mirror of the GitHub rule: an identity or email is exactly right here, and
            // the shared validator must not have rejected it.
            Provisioner.DescribeRepositoryProblems(
                RepositoryResolver.Resolve(
                    defaults: null,
                    new RepositoryOptions
                    {
                        Name = "widget-api",
                        Approvers = ["reviewer@contoso.com"],
                    })).ShouldBeEmpty();
        }

        private static IReadOnlyList<string> Warnings(RepositoryOptions options)
        {
            return Provisioner.DescribeUnsupportedSettings(
                RepositoryResolver.Resolve(defaults: null, options));
        }
    }
}
