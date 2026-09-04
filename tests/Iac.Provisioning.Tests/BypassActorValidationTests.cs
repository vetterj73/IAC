using Iac.Provisioning.Configuration;

using Shouldly;

using Xunit;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Bypass actors are the break-glass path around a ruleset, and GitHub is picky about
    /// them: some actor types carry a numeric id and some must not.
    /// </summary>
    public class BypassActorValidationTests
    {
        [Theory]
        [InlineData("RepositoryRole")]
        [InlineData("Team")]
        [InlineData("Integration")]
        [InlineData("User")]
        public void Id_bearing_actor_types_require_an_id(string actorType)
        {
            Should.Throw<ConfigurationException>(() => Parse(actorType, actorId: null))
                .Message.ShouldContain("requires a numeric 'actorId'");
        }

        [Theory]
        [InlineData("OrganizationAdmin")]
        [InlineData("EnterpriseOwner")]
        [InlineData("DeployKey")]
        public void Id_less_actor_types_reject_an_id(string actorType)
        {
            // GitHub ignores an id for these, so accepting one would hide a mistake.
            Should.Throw<ConfigurationException>(() => Parse(actorType, actorId: 1))
                .Message.ShouldContain("has no actorId");
        }

        [Theory]
        [InlineData("OrganizationAdmin")]
        [InlineData("EnterpriseOwner")]
        [InlineData("DeployKey")]
        public void Id_less_actor_types_are_accepted_without_an_id(string actorType)
        {
            Should.NotThrow(() => Parse(actorType, actorId: null));
        }

        [Fact]
        public void An_id_bearing_actor_type_is_accepted_with_an_id()
        {
            Should.NotThrow(() => Parse("User", actorId: 4242));
        }

        [Fact]
        public void An_unknown_actor_type_is_rejected()
        {
            Should.Throw<ConfigurationException>(() => Parse("Wizard", actorId: 1))
                .Message.ShouldContain("bypass actorType 'Wizard' is not one of");
        }

        [Fact]
        public void A_missing_actor_type_is_rejected()
        {
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    ruleset:
                      bypassActors:
                        - bypassMode: always
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("missing 'actorType'");
        }

        [Fact]
        public void An_unknown_bypass_mode_is_rejected()
        {
            string yaml = """
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    ruleset:
                      bypassActors:
                        - actorType: OrganizationAdmin
                          bypassMode: whenever
                """;

            Should.Throw<ConfigurationException>(() => ConfigurationLoader.Parse(yaml))
                .Message.ShouldContain("bypass mode 'whenever' is not one of");
        }

        [Fact]
        public void The_bypass_mode_defaults_to_always()
        {
            ResolvedRepository resolved = RepositoryResolver.Resolve(
                defaults: null,
                new RepositoryOptions
                {
                    Name = "widget-api",
                    Ruleset = new RulesetOptions
                    {
                        BypassActors = [new BypassActorOptions { ActorType = "OrganizationAdmin" }],
                    },
                });

            ResolvedBypassActor actor = resolved.Ruleset.BypassActors.ShouldHaveSingleItem();
            actor.BypassMode.ShouldBe("always");
            actor.ActorId.ShouldBeNull();
        }

        private static IacConfiguration Parse(string actorType, int? actorId)
        {
            string id = actorId is null
                ? string.Empty
                : $"\n          actorId: {actorId.Value}";

            string yaml = $"""
                provider: github
                organization: contoso
                repositories:
                  - name: widget-api
                    ruleset:
                      bypassActors:
                        - actorType: {actorType}
                          bypassMode: always{id}
                """;

            return ConfigurationLoader.Parse(yaml);
        }
    }
}
