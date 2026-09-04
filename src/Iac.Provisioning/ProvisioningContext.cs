using Iac.Provisioning.Configuration;

namespace Iac.Provisioning
{
    /// <summary>Everything a provisioner needs for a single repository.</summary>
    public sealed class ProvisioningContext
    {
        /// <summary>Owning GitHub organization or user, or Azure DevOps organization.</summary>
        public required string Organization { get; init; }

        public required ResolvedRepository Repository { get; init; }
    }
}
