using Iac.Provisioning.Configuration;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    public class ConfigurationLoaderTests
    {
        private const string MinimalConfiguration = """
            provider: github
            organization: contoso
            repositories:
              - name: widget-api
            """;

        [Fact]
        public void Parse_reads_a_minimal_configuration()
        {
            IacConfiguration configuration = ConfigurationLoader.Parse(MinimalConfiguration);

            configuration.Provider.ShouldBe("github");
            configuration.Organization.ShouldBe("contoso");
            configuration.Repositories.ShouldNotBeNull();
            configuration.Repositories!.Count.ShouldBe(1);
            configuration.Repositories[0].Name.ShouldBe("widget-api");
        }

        [Fact]
        public void Parse_rejects_an_unknown_setting()
        {
            // A typo in a policy file must fail loudly rather than silently leave a rule unset.
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    ruleset:
                      minimumAprovals: 2
                """;

            ConfigurationException exception =
                Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml));

            exception.Message.ShouldContain("unrecognized setting");
        }

        [Fact]
        public void Parse_rejects_an_empty_document()
        {
            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(string.Empty))
                .Message.ShouldContain("empty");
        }

        [Theory]
        [InlineData("organization: contoso\nrepositories:\n  - name: a", "'provider' is required")]
        [InlineData("provider: github\nrepositories:\n  - name: a", "'organization' is required")]
        [InlineData("provider: github\norganization: contoso", "must list at least one repository")]
        public void Parse_reports_missing_required_settings(string yaml, string expected)
        {
            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain(expected);
        }

        [Fact]
        public void Parse_reports_every_problem_at_once()
        {
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    visibility: secret
                    ruleset:
                      enforcement: sometimes
                      minimumApprovals: 99
                """;

            string message = Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml)).Message;

            message.ShouldContain("visibility 'secret'");
            message.ShouldContain("enforcement 'sometimes'");
            message.ShouldContain("minimumApprovals must be between 0 and 10");
        }

        [Fact]
        public void Parse_rejects_duplicate_repository_names()
        {
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                  - name: Widget-API
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("listed more than once");
        }

        [Fact]
        public void Parse_rejects_an_approver_without_an_at_sign()
        {
            // CODEOWNERS silently ignores an owner that is not prefixed with '@', which would
            // leave the repository with an unsatisfiable review requirement.
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    approvers:
                      - platform-team
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("must be '@user' or '@org/team'");
        }

        [Fact]
        public void Parse_rejects_a_name_in_the_defaults_block()
        {
            string yaml = """
                provider: github
                organization: contoso
                defaults:
                  name: oops
                repositories:
                  - name: widget-api
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("does not belong in the defaults block");
        }

        [Fact]
        public void Load_explains_how_to_get_a_configuration_file_when_it_is_missing()
        {
            ConfigurationException exception =
                Should.Throw<ConfigurationException>(() => ConfigurationLoader.Load("no-such-file.yml"));

            exception.Message.ShouldContain("repositories.example.yml");
        }
    }
}
