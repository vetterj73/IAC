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
        /// the organization name; the project is named per repository.
        /// </summary>
        public string? Organization { get; set; }

        public BackendOptions? Backend { get; set; }

        /// <summary>Settings applied to every repository unless the entry overrides them.</summary>
        public RepositoryOptions? Defaults { get; set; }

        public IList<RepositoryOptions>? Repositories { get; set; }
    }

    /// <summary>Where Pulumi keeps its state. State is what makes re-runs idempotent.</summary>
    public sealed class BackendOptions
    {
        /// <summary>
        /// A Pulumi backend URL. <c>file://./.pulumi-state</c> keeps state on this machine
        /// only; <c>azblob://container</c> shares it with the team. Null means Pulumi Cloud
        /// via <c>PULUMI_ACCESS_TOKEN</c>.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Pulumi project name. Defaults to <c>iac-{provider}</c>. Changing it orphans
        /// existing state, so treat it as fixed once repositories have been created.
        /// </summary>
        public string? ProjectName { get; set; }
    }
}
