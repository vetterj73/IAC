using System.Collections.Generic;

using Iac.Provisioning.Configuration;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    public class RepositoryResolverTests
    {
        [Fact]
        public void Built_in_defaults_apply_when_nothing_is_configured()
        {
            ResolvedRepository resolved = RepositoryResolver.Resolve(
                defaults: null,
                new RepositoryOptions { Name = "widget-api" });

            resolved.Visibility.ShouldBe("private");
            resolved.DefaultBranch.ShouldBe("main");
            resolved.ArchiveOnDestroy.ShouldBeTrue();
            resolved.Ruleset.Enforcement.ShouldBe("active");
            resolved.Ruleset.RequirePullRequest.ShouldBeTrue();
            resolved.Ruleset.MinimumApprovals.ShouldBe(1);
            resolved.Ruleset.BlockForcePush.ShouldBeTrue();
            resolved.Ruleset.BlockDeletion.ShouldBeTrue();
            resolved.Merge.AllowSquash.ShouldBeTrue();
            resolved.Merge.AllowMergeCommit.ShouldBeFalse();
            resolved.Files.Codeowners.ShouldBeTrue();
        }

        [Fact]
        public void Defaults_are_inherited_by_an_entry_that_says_nothing()
        {
            RepositoryOptions defaults = new()
            {
                Visibility = "internal",
                Ruleset = new RulesetOptions { MinimumApprovals = 3 },
            };

            ResolvedRepository resolved =
                RepositoryResolver.Resolve(defaults, new RepositoryOptions { Name = "widget-api" });

            resolved.Visibility.ShouldBe("internal");
            resolved.Ruleset.MinimumApprovals.ShouldBe(3);
        }

        [Fact]
        public void An_entry_overrides_the_defaults()
        {
            RepositoryOptions defaults = new()
            {
                Ruleset = new RulesetOptions { MinimumApprovals = 1, RequireSignedCommits = false },
            };

            ResolvedRepository resolved = RepositoryResolver.Resolve(
                defaults,
                new RepositoryOptions
                {
                    Name = "widget-api",
                    Ruleset = new RulesetOptions { MinimumApprovals = 2 },
                });

            resolved.Ruleset.MinimumApprovals.ShouldBe(2);

            // Sibling settings inside the same block still come from the defaults - overriding
            // one ruleset value must not discard the rest of the block.
            resolved.Ruleset.RequireSignedCommits.ShouldBeFalse();
            resolved.Ruleset.BlockForcePush.ShouldBeTrue();
        }

        [Fact]
        public void Lists_replace_rather_than_concatenate()
        {
            // Concatenating would make it impossible to drop an inherited approver or topic
            // from a single repository.
            RepositoryOptions defaults = new()
            {
                Topics = ["platform", "dotnet"],
                Approvers = ["@contoso/platform"],
            };

            ResolvedRepository resolved = RepositoryResolver.Resolve(
                defaults,
                new RepositoryOptions
                {
                    Name = "widget-api",
                    Topics = ["experiment"],
                });

            resolved.Topics.ShouldBe(new[] { "experiment" });
            resolved.Approvers.ShouldBe(new[] { "@contoso/platform" });
        }

        [Fact]
        public void An_entry_without_a_name_is_rejected()
        {
            Should.Throw<ConfigurationException>(() => RepositoryResolver.Resolve(null, new RepositoryOptions()))
                .Message.ShouldContain("missing 'name'");
        }

        [Fact]
        public void Collaborators_default_to_push_permission()
        {
            ResolvedRepository resolved = RepositoryResolver.Resolve(
                defaults: null,
                new RepositoryOptions
                {
                    Name = "widget-api",
                    Collaborators = new CollaboratorOptions
                    {
                        Users = [new CollaboratorEntry { Name = "octocat" }],
                        Teams = [new CollaboratorEntry { Name = "platform", Permission = "admin" }],
                    },
                });

            resolved.UserCollaborators.ShouldHaveSingleItem().Permission.ShouldBe("push");
            resolved.TeamCollaborators.ShouldHaveSingleItem().Permission.ShouldBe("admin");
        }

        [Fact]
        public void ResolveAll_resolves_every_entry_in_file_order()
        {
            IacConfiguration configuration = new()
            {
                Provider = "github",
                Organization = "contoso",
                Defaults = new RepositoryOptions { Visibility = "public" },
                Repositories =
                [
                    new RepositoryOptions { Name = "first" },
                    new RepositoryOptions { Name = "second", Visibility = "private" },
                ],
            };

            IReadOnlyList<ResolvedRepository> resolved = RepositoryResolver.ResolveAll(configuration);

            resolved.Count.ShouldBe(2);
            resolved[0].Name.ShouldBe("first");
            resolved[0].Visibility.ShouldBe("public");
            resolved[1].Visibility.ShouldBe("private");
        }
    }
}
