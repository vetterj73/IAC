using Iac.Provisioning.Configuration;

namespace Iac.Provisioning
{
    /// <summary>Everything a provisioner needs for a single repository.</summary>
    public sealed class ProvisioningContext
    {
        /// <summary>Owning GitHub organization or user, or Azure DevOps organization.</summary>
        public required string Organization { get; init; }

        /// <summary>
        /// Azure DevOps project that contains the repository. Null for providers with no
        /// project layer, such as GitHub.
        /// </summary>
        public string? Project { get; init; }

        public required ResolvedRepository Repository { get; init; }
    }
}
