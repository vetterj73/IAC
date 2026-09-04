using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// Shape validation for a loaded configuration. Checks what can be checked without
    /// talking to a provider; anything provider-specific is reported by the provisioner.
    /// </summary>
    public static class ConfigurationValidator
    {
        private static readonly string[] Visibilities = ["private", "public", "internal"];

        private static readonly string[] Enforcements = ["active", "evaluate", "disabled"];

        private static readonly string[] Permissions = ["pull", "triage", "push", "maintain", "admin"];

        private static readonly Regex RepositoryNamePattern =
            new("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        /// <summary>Validates the configuration, reporting every problem found at once.</summary>
        /// <exception cref="ConfigurationException">One or more settings are invalid.</exception>
        public static void Validate(IacConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            List<string> problems = [];

            if (string.IsNullOrWhiteSpace(configuration.Provider))
            {
                problems.Add("'provider' is required (for example: github).");
            }

            if (string.IsNullOrWhiteSpace(configuration.Organization))
            {
                problems.Add("'organization' is required.");
            }

            if (configuration.Repositories is null || configuration.Repositories.Count == 0)
            {
                problems.Add("'repositories' must list at least one repository.");
            }

            ValidateOptions(configuration.Defaults, "defaults", problems, requireName: false);

            if (configuration.Repositories is not null)
            {
                for (int index = 0; index < configuration.Repositories.Count; index++)
                {
                    RepositoryOptions repository = configuration.Repositories[index];
                    string scope = repository.Name ?? $"repositories[{index.ToString(CultureInfo.InvariantCulture)}]";
                    ValidateOptions(repository, scope, problems, requireName: true);
                }

                IEnumerable<string> duplicates = configuration.Repositories
                    .Where(static repository => !string.IsNullOrWhiteSpace(repository.Name))
                    .GroupBy(static repository => repository.Name!, StringComparer.OrdinalIgnoreCase)
                    .Where(static group => group.Count() > 1)
                    .Select(static group => group.Key);

                foreach (string duplicate in duplicates)
                {
                    problems.Add($"Repository '{duplicate}' is listed more than once.");
                }
            }

            if (problems.Count > 0)
            {
                throw new ConfigurationException(
                    "Configuration is not valid:" + Environment.NewLine
                    + string.Join(Environment.NewLine, problems.Select(static problem => "  - " + problem)));
            }
        }

        private static void ValidateOptions(
            RepositoryOptions? options,
            string scope,
            List<string> problems,
            bool requireName)
        {
            if (options is null)
            {
                return;
            }

            if (requireName)
            {
                if (string.IsNullOrWhiteSpace(options.Name))
                {
                    problems.Add($"{scope}: 'name' is required.");
                }
                else if (!RepositoryNamePattern.IsMatch(options.Name))
                {
                    problems.Add(
                        $"{scope}: repository names may contain only letters, digits, '.', '_' and '-'.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.Name))
            {
                problems.Add($"{scope}: 'name' does not belong in the defaults block.");
            }

            if (options.Visibility is not null
                && !Visibilities.Contains(options.Visibility, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add(
                    $"{scope}: visibility '{options.Visibility}' is not one of "
                    + string.Join(", ", Visibilities) + ".");
            }

            ValidateRuleset(options.Ruleset, scope, problems);
            ValidateCollaborators(options.Collaborators, scope, problems);
            ValidateApprovers(options.Approvers, scope, problems);
        }

        private static void ValidateRuleset(RulesetOptions? ruleset, string scope, List<string> problems)
        {
            if (ruleset is null)
            {
                return;
            }

            if (ruleset.Enforcement is not null
                && !Enforcements.Contains(ruleset.Enforcement, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add(
                    $"{scope}: ruleset enforcement '{ruleset.Enforcement}' is not one of "
                    + string.Join(", ", Enforcements) + ".");
            }

            if (ruleset.MinimumApprovals is < 0 or > 10)
            {
                problems.Add($"{scope}: ruleset minimumApprovals must be between 0 and 10.");
            }
        }

        /// <summary>
        /// Validation that only makes sense after inheritance has been applied - an entry can
        /// inherit its approvers from the defaults block, so these combinations cannot be
        /// judged from the raw file.
        /// </summary>
        /// <exception cref="ConfigurationException">The resolved repository is unusable.</exception>
        public static void ValidateResolved(ResolvedRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            List<string> problems = [];

            // Requiring code-owner review with nobody listed blocks every pull request
            // permanently: CODEOWNERS would be empty, so no approval can ever satisfy it.
            if (repository.Ruleset.RequireCodeOwnerReview && repository.Approvers.Count == 0)
            {
                problems.Add(
                    "ruleset.requireCodeOwnerReview is true but 'approvers' is empty. CODEOWNERS "
                    + "would have no owners, so no pull request could ever be approved. Add "
                    + "approvers, or set requireCodeOwnerReview to false.");
            }

            if (repository.Ruleset.RequireCodeOwnerReview && !repository.Files.Codeowners)
            {
                problems.Add(
                    "ruleset.requireCodeOwnerReview is true but files.codeowners is false, so no "
                    + "CODEOWNERS file would be written and every pull request would be blocked.");
            }

            if (!repository.Merge.AllowSquash
                && !repository.Merge.AllowMergeCommit
                && !repository.Merge.AllowRebase)
            {
                problems.Add("at least one of merge.allowSquash, allowMergeCommit or allowRebase must be true.");
            }

            if (repository.Ruleset.RequireLinearHistory && repository.Merge.AllowMergeCommit)
            {
                problems.Add(
                    "ruleset.requireLinearHistory is true but merge.allowMergeCommit is also true - "
                    + "a merge commit can never satisfy a linear-history rule.");
            }

            if (problems.Count > 0)
            {
                throw new ConfigurationException(
                    $"Repository '{repository.Name}' is not valid:" + Environment.NewLine
                    + string.Join(Environment.NewLine, problems.Select(static problem => "  - " + problem)));
            }
        }

        private static void ValidateCollaborators(
            CollaboratorOptions? collaborators,
            string scope,
            List<string> problems)
        {
            if (collaborators is null)
            {
                return;
            }

            IEnumerable<CollaboratorEntry> all =
                (collaborators.Users ?? []).Concat(collaborators.Teams ?? []);

            foreach (CollaboratorEntry entry in all)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    problems.Add($"{scope}: a collaborator entry is missing 'name'.");
                }

                if (entry.Permission is not null
                    && !Permissions.Contains(entry.Permission, StringComparer.OrdinalIgnoreCase))
                {
                    problems.Add(
                        $"{scope}: collaborator permission '{entry.Permission}' is not one of "
                        + string.Join(", ", Permissions) + ".");
                }
            }
        }

        private static void ValidateApprovers(IList<string>? approvers, string scope, List<string> problems)
        {
            if (approvers is null)
            {
                return;
            }

            foreach (string approver in approvers)
            {
                if (!approver.StartsWith('@'))
                {
                    problems.Add(
                        $"{scope}: approver '{approver}' must be '@user' or '@org/team' - CODEOWNERS "
                        + "requires the leading '@'.");
                }
            }
        }
    }
}
