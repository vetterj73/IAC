using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// Root of the configuration file. Deliberately provider-agnostic: the only thing that
    /// binds a run to GitHub rather than Azure DevOps is <see cref="Provider"/>.
    /// </summary>
    public sealed class IacConfiguration
    {
        /// <summary>Destination provider key, e.g. <c>github</c> or <c>azuredevops</c>.</summary>
        public string? Provider { get; set; }

        /// <summary>
        /// GitHub organization or user that owns the repositories. For Azure DevOps this is
        /// the organization service URL host portion - the organization name.
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Whether <see cref="Organization"/> is an organization or a single user account:
        /// <c>organization</c> (default) or <c>user</c>.
        /// </summary>
        /// <remarks>
        /// This is not cosmetic. A personal (user-owned) repository has no teams, no
        /// organization administrator to bypass a ruleset, and - because GitHub never lets
        /// anyone approve their own pull request - a required-approval count above zero locks
        /// the sole owner out of their own default branch. Declaring <c>user</c> lowers the
        /// default approval count to zero and turns the impossible combinations into
        /// configuration errors instead of a repository nobody can merge into.
        /// </remarks>
        public string? OwnerType { get; set; }

        /// <summary>
        /// Azure DevOps project that contains the repositories. Required for
        /// <c>provider: azuredevops</c> and ignored by GitHub, which has no project layer.
        /// </summary>
        public string? Project { get; set; }

        public BackendOptions? Backend { get; set; }

        /// <summary>Settings applied to every repository unless the entry overrides them.</summary>
        public RepositoryOptions? Defaults { get; set; }

        public IList<RepositoryOptions>? Repositories { get; set; }
    }
}
