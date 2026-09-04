using Iac.Provisioning.Configuration;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// The personal-repository problem: nobody can approve their own pull request, so a
    /// required-approval count on a single-person repository locks the owner out of their own
    /// default branch. These tests cover the default that avoids it and the guards that refuse
    /// to create the broken combination.
    /// </summary>
    public class OwnerTypeTests
    {
        [Fact]
        public void An_organization_repository_defaults_to_one_approval()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions { Name = "widget-api" });

            resolved.OwnerType.ShouldBe(RepositoryOwnerType.Organization);
            resolved.Ruleset.RequirePullRequest.ShouldBeTrue();
            resolved.Ruleset.MinimumApprovals.ShouldBe(1);
        }

        [Fact]
        public void A_user_repository_defaults_to_zero_approvals()
        {
            // Still requires a pull request - checks run, history stays reviewable - but does
            // not demand an approval the owner cannot give.
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions { Name = "personal-tools" },
                RepositoryOwnerType.User);

            resolved.Ruleset.RequirePullRequest.ShouldBeTrue();
            resolved.Ruleset.MinimumApprovals.ShouldBe(0);

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Fact]
        public void An_explicit_approval_count_still_wins_on_a_user_repository()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Approvers = ["@octocat"],
                    Collaborators = new CollaboratorOptions
                    {
                        Users = [new CollaboratorEntry { Name = "octocat" }],
                    },
                    Ruleset = new RulesetOptions { MinimumApprovals = 1 },
                },
                RepositoryOwnerType.User);

            resolved.Ruleset.MinimumApprovals.ShouldBe(1);
        }

        [Fact]
        public void The_lockout_combination_is_rejected()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions { MinimumApprovals = 1 },
                },
                RepositoryOwnerType.User);

            string message = Should
                .Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message;

            message.ShouldContain("could never merge");

            // The message must name the ways out, not just the problem.
            message.ShouldContain("minimumApprovals to 0");
            message.ShouldContain("add collaborators");
            message.ShouldContain("bypassActors");
        }

        [Fact]
        public void Approvals_are_allowed_on_a_user_repository_that_has_collaborators()
        {
            // Someone else can approve, so the requirement is satisfiable.
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions { MinimumApprovals = 1 },
                    Collaborators = new CollaboratorOptions
                    {
                        Users = [new CollaboratorEntry { Name = "octocat", Permission = "push" }],
                    },
                },
                RepositoryOwnerType.User);

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Fact]
        public void Approvals_are_allowed_on_a_user_repository_with_a_bypass_actor()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions
                    {
                        MinimumApprovals = 1,
                        BypassActors =
                        [
                            new BypassActorOptions { ActorType = "User", ActorId = 4242 },
                        ],
                    },
                },
                RepositoryOwnerType.User);

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Fact]
        public void No_lockout_check_applies_when_no_pull_request_is_required()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions { RequirePullRequest = false, MinimumApprovals = 2 },
                },
                RepositoryOwnerType.User);

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Theory]
        [InlineData("internal", "requires an organization")]
        public void Internal_visibility_is_rejected_for_a_user_repository(string visibility, string expected)
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions { Name = "personal-tools", Visibility = visibility },
                RepositoryOwnerType.User);

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain(expected);
        }

        [Fact]
        public void Team_collaborators_are_rejected_for_a_user_repository()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Collaborators = new CollaboratorOptions
                    {
                        Teams = [new CollaboratorEntry { Name = "platform" }],
                    },
                },
                RepositoryOwnerType.User);

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("has no teams");
        }

        [Fact]
        public void Evaluate_enforcement_is_rejected_for_a_user_repository()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions { Enforcement = "evaluate" },
                },
                RepositoryOwnerType.User);

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("only supported for organization-owned");
        }

        [Fact]
        public void Admin_bypass_is_rejected_for_a_user_repository()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Ruleset = new RulesetOptions { AllowAdminBypass = true },
                },
                RepositoryOwnerType.User);

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("does not exist for a user-owned repository");
        }

        [Fact]
        public void A_team_approver_is_rejected_for_a_user_repository()
        {
            ResolvedRepository resolved = Resolve(
                new RepositoryOptions
                {
                    Name = "personal-tools",
                    Approvers = ["@contoso/platform"],
                },
                RepositoryOwnerType.User);

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("names a team");
        }

        [Theory]
        [InlineData(null, RepositoryOwnerType.Organization)]
        [InlineData("", RepositoryOwnerType.Organization)]
        [InlineData("organization", RepositoryOwnerType.Organization)]
        [InlineData("Organization", RepositoryOwnerType.Organization)]
        [InlineData("user", RepositoryOwnerType.User)]
        [InlineData("USER", RepositoryOwnerType.User)]
        public void Owner_type_is_parsed_case_insensitively(string? value, RepositoryOwnerType expected)
        {
            RepositoryResolver.ParseOwnerType(value).ShouldBe(expected);
        }

        [Fact]
        public void An_unknown_owner_type_is_rejected()
        {
            Should.Throw<ConfigurationException>(() => RepositoryResolver.ParseOwnerType("enterprise"))
                .Message.ShouldContain("not recognized");
        }

        [Fact]
        public void Owner_type_flows_from_the_configuration_file()
        {
            string yaml = """
                provider: github
                organization: octocat
                ownerType: user
                repositories:
                  - name: personal-tools
                """;

            IacConfiguration configuration = ConfigurationLoader.Parse(yaml);
            ResolvedRepository resolved = RepositoryResolver.ResolveAll(configuration)[0];

            resolved.OwnerType.ShouldBe(RepositoryOwnerType.User);
            resolved.Ruleset.MinimumApprovals.ShouldBe(0);
        }

        [Fact]
        public void An_unknown_owner_type_is_reported_by_the_loader()
        {
            string yaml = """
                provider: github
                organization: octocat
                ownerType: enterprise
                repositories:
                  - name: personal-tools
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("ownerType 'enterprise' is not recognized");
        }

        private static ResolvedRepository Resolve(
            RepositoryOptions options,
            RepositoryOwnerType ownerType = RepositoryOwnerType.Organization)
        {
            return RepositoryResolver.Resolve(defaults: null, options, ownerType);
        }
    }
}
