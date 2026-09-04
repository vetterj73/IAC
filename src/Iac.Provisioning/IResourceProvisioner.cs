using System.Collections.Generic;

using Iac.Provisioning.Configuration;

namespace Iac.Provisioning
{
    /// <summary>
    /// One destination for repository provisioning. Adding Azure DevOps means adding an
    /// implementation of this interface and registering it - no changes to the CLI, the
    /// configuration model or the Pulumi runner.
    /// </summary>
    public interface IResourceProvisioner
    {
        /// <summary>Value of the configuration file's <c>provider</c> key, e.g. <c>github</c>.</summary>
        string ProviderName { get; }

        /// <summary>Pulumi plugin this provider needs, e.g. <c>github</c>.</summary>
        string PulumiPluginName { get; }

        /// <summary>
        /// Environment variables that must be set for the run to authenticate. Reported to the
        /// operator up front rather than surfacing as an opaque provider error.
        /// </summary>
        IReadOnlyList<string> RequiredEnvironmentVariables { get; }

        /// <summary>
        /// Pulumi stack configuration keys this provider needs, e.g. <c>github:owner</c>.
        /// </summary>
        IReadOnlyDictionary<string, string> BuildStackConfiguration(string organization);

        /// <summary>
        /// Settings present in the configuration that this destination cannot honour. Returned
        /// as human-readable warnings so a config written for GitHub can be pointed at Azure
        /// DevOps without silently losing policy.
        /// </summary>
        IReadOnlyList<string> DescribeUnsupportedSettings(ResolvedRepository repository);

        /// <summary>
        /// Provider-level requirements the configuration fails to meet - for example Azure
        /// DevOps needing a project. Returned rather than thrown so every problem can be
        /// reported at once, and checked before Pulumi is started.
        /// </summary>
        IReadOnlyList<string> DescribeConfigurationProblems(IacConfiguration configuration);

        /// <summary>
        /// Per-repository settings this destination would reject or silently misapply, as
        /// errors rather than warnings.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="DescribeUnsupportedSettings"/>: that reports what will be
        /// ignored, this reports what makes the configuration wrong for this provider.
        /// Approver syntax is the motivating case - GitHub needs CODEOWNERS form and Azure
        /// DevOps needs an identity, and neither can be judged provider-agnostically.
        /// </remarks>
        IReadOnlyList<string> DescribeRepositoryProblems(ResolvedRepository repository);

        /// <summary>
        /// Declares the resources for one repository. Runs inside a Pulumi program, so it
        /// constructs resources rather than calling an API directly - which is what makes a
        /// second run with the same configuration a no-op.
        /// </summary>
        void Provision(ProvisioningContext context);
    }
}
