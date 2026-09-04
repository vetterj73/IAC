using Iac.Provisioning.Configuration;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Checks the combinations that can only be judged after inheritance - these are the
    /// configurations that would leave a repository permanently unmergeable.
    /// </summary>
    public class ResolvedRepositoryValidationTests
    {
        [Fact]
        public void A_sane_configuration_passes()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions { Name = "widget-api" });

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Fact]
        public void Requiring_code_owner_review_with_no_approvers_is_rejected()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions
            {
                Name = "widget-api",
                Ruleset = new RulesetOptions { RequireCodeOwnerReview = true },
            });

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("CODEOWNERS would have no owners");
        }

        [Fact]
        public void Requiring_code_owner_review_without_writing_codeowners_is_rejected()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform"],
                Ruleset = new RulesetOptions { RequireCodeOwnerReview = true },
                Files = new GeneratedFileOptions { Codeowners = false },
            });

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("files.codeowners is false");
        }

        [Fact]
        public void Approvers_plus_code_owner_review_is_accepted()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions
            {
                Name = "widget-api",
                Approvers = ["@contoso/platform"],
                Ruleset = new RulesetOptions { RequireCodeOwnerReview = true },
            });

            Should.NotThrow(() => ConfigurationValidator.ValidateResolved(resolved));
        }

        [Fact]
        public void Disabling_every_merge_method_is_rejected()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions
            {
                Name = "widget-api",
                Merge = new MergeOptions
                {
                    AllowSquash = false,
                    AllowMergeCommit = false,
                    AllowRebase = false,
                },
            });

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("at least one of merge.allowSquash");
        }

        [Fact]
        public void Linear_history_with_merge_commits_allowed_is_rejected()
        {
            ResolvedRepository resolved = Resolve(new RepositoryOptions
            {
                Name = "widget-api",
                Merge = new MergeOptions { AllowMergeCommit = true },
                Ruleset = new RulesetOptions { RequireLinearHistory = true },
            });

            Should.Throw<ConfigurationException>(() => ConfigurationValidator.ValidateResolved(resolved))
                .Message.ShouldContain("linear-history");
        }

        private static ResolvedRepository Resolve(RepositoryOptions options)
        {
            return RepositoryResolver.Resolve(defaults: null, options);
        }
    }
}
