using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// The YAML surface for one repository. Every member is nullable so that a value can be
    /// "not stated here" and inherit from <see cref="IacConfiguration.Defaults"/>. Resolved
    /// into a <see cref="ResolvedRepository"/> before any provider sees it.
    /// </summary>
    public sealed class RepositoryOptions
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Homepage { get; set; }

        /// <summary><c>private</c>, <c>public</c> or <c>internal</c>.</summary>
        public string? Visibility { get; set; }

        public string? DefaultBranch { get; set; }

        public IList<string>? Topics { get; set; }

        /// <summary>GitHub license template key, e.g. <c>mit</c>. Null leaves the repo unlicensed.</summary>
        public string? License { get; set; }

        /// <summary>GitHub .gitignore template name, e.g. <c>VisualStudio</c>.</summary>
        public string? GitignoreTemplate { get; set; }

        /// <summary>
        /// When true, <c>destroy</c> archives the repository instead of deleting it. Leaving
        /// this on is the difference between a recoverable mistake and a lost repository.
        /// </summary>
        public bool? ArchiveOnDestroy { get; set; }

        public FeatureOptions? Features { get; set; }

        public MergeOptions? Merge { get; set; }

        public SecurityOptions? Security { get; set; }

        public RulesetOptions? Ruleset { get; set; }

        /// <summary>
        /// Logins or teams that should approve changes, written into CODEOWNERS. Entries are
        /// used verbatim, so they must be <c>@user</c> or <c>@org/team</c> form.
        /// </summary>
        public IList<string>? Approvers { get; set; }

        public CollaboratorOptions? Collaborators { get; set; }

        public GeneratedFileOptions? Files { get; set; }
    }
}
