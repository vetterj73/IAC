using System.Collections.Generic;
using System.Linq;

using Iac.Provisioning;
using Iac.Provisioning.Configuration;
using Iac.Provisioning.GitHub;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    public class GitHubProvisionerTests
    {
        private static readonly GitHubRepositoryProvisioner Provisioner = new();

        [Fact]
        public void The_registry_resolves_github_case_insensitively()
        {
            ProvisionerRegistry registry = new([Provisioner]);

            registry.Resolve("GitHub").ProviderName.ShouldBe("github");
        }

        [Fact]
        public void The_registry_lists_known_providers_when_asked_for_an_unknown_one()
        {
            ProvisionerRegistry registry = new([Provisioner]);

            ConfigurationException exception =
                Should.Throw<ConfigurationException>(() => registry.Resolve("bitbucket"));

            exception.Message.ShouldContain("not supported");
            exception.Message.ShouldContain("github");
        }

        [Fact]
        public void The_registry_explains_a_missing_provider_key()
        {
            ProvisionerRegistry registry = new([Provisioner]);

            Should.Throw<ConfigurationException>(() => registry.Resolve(null))
                .Message.ShouldContain("Set 'provider' in the configuration file");
        }

        [Fact]
        public void The_owner_is_passed_to_the_provider_as_stack_configuration()
        {
            IReadOnlyDictionary<string, string> configuration = Provisioner.BuildStackConfiguration("contoso");

            configuration["github:owner"].ShouldBe("contoso");
        }

        [Fact]
        public void A_plain_private_repository_produces_no_warnings()
        {
            IReadOnlyList<string> warnings = Provisioner.DescribeUnsupportedSettings(
                Resolve(new RepositoryOptions { Name = "widget-api" }));

            warnings.ShouldBeEmpty();
        }

        [Fact]
        public void Secret_scanning_on_a_private_repository_warns_about_advanced_security()
        {
            IReadOnlyList<string> warnings = Provisioner.DescribeUnsupportedSettings(
                Resolve(new RepositoryOptions
                {
                    Name = "widget-api",
                    Visibility = "private",
                    Security = new SecurityOptions { SecretScanning = true },
                }));

            warnings.ShouldContain(warning => warning.Contains("Advanced Security"));
        }

        [Fact]
        public void Status_checks_warn_that_they_must_have_reported_once()
        {
            IReadOnlyList<string> warnings = Provisioner.DescribeUnsupportedSettings(
                Resolve(new RepositoryOptions
                {
                    Name = "widget-api",
                    Ruleset = new RulesetOptions { RequiredStatusChecks = ["build"] },
                }));

            warnings.ShouldContain(warning => warning.Contains("reported at"));
        }

        [Fact]
        public void Required_reviewers_warn_that_the_rule_is_beta()
        {
            IReadOnlyList<string> warnings = Provisioner.DescribeUnsupportedSettings(
                Resolve(new RepositoryOptions
                {
                    Name = "widget-api",
                    Ruleset = new RulesetOptions
                    {
                        RequiredReviewers = [new RequiredReviewerOptions { Id = 42 }],
                    },
                }));

            warnings.ShouldContain(warning => warning.Contains("beta"));
        }

        [Fact]
        public void Codeowners_assigns_every_path_to_the_approvers()
        {
            string content = CodeownersFile.Build(["@contoso/platform", "@octocat"]);

            content.ShouldContain("* @contoso/platform @octocat");
            content.ShouldContain("Managed by the iac CLI");
        }

        [Fact]
        public void Codeowners_with_no_approvers_lists_no_owners()
        {
            string content = CodeownersFile.Build([]);

            content.Split('\n').ShouldNotContain(line => line.StartsWith("* ", System.StringComparison.Ordinal));
        }

        private static ResolvedRepository Resolve(RepositoryOptions options)
        {
            return RepositoryResolver.Resolve(defaults: null, options);
        }
    }
}
