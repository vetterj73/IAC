using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>A reviewer the ruleset itself requires, rather than one named in CODEOWNERS.</summary>
    public sealed class RequiredReviewerOptions
    {
        /// <summary>
        /// Numeric id of the team that must review. Find it with
        /// <c>gh api /orgs/{org}/teams/{slug} --jq .id</c>; the provider takes the id, not the slug.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>Reviewer kind. The provider currently supports only <c>Team</c>.</summary>
        public string? Type { get; set; }

        public int? MinimumApprovals { get; set; }

        /// <summary>Paths that trigger this reviewer requirement.</summary>
        public IList<string>? FilePatterns { get; set; }
    }
}
