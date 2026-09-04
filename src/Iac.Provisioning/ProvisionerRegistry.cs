using System;
using System.Collections.Generic;
using System.Linq;

using Iac.Provisioning.Configuration;

namespace Iac.Provisioning
{
    /// <summary>
    /// Maps a configuration <c>provider</c> value to its provisioner. This is the single place
    /// that knows which destinations exist.
    /// </summary>
    public sealed class ProvisionerRegistry
    {
        private readonly Dictionary<string, IResourceProvisioner> _provisioners;

        public ProvisionerRegistry(IEnumerable<IResourceProvisioner> provisioners)
        {
            ArgumentNullException.ThrowIfNull(provisioners);

            _provisioners = provisioners.ToDictionary(
                static provisioner => provisioner.ProviderName,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Provider keys that can be used in a configuration file.</summary>
        public IReadOnlyCollection<string> KnownProviders => _provisioners.Keys;

        /// <summary>Resolves a provider key.</summary>
        /// <exception cref="ConfigurationException">The provider is not registered.</exception>
        public IResourceProvisioner Resolve(string? providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ConfigurationException(
                    "No provider was specified. Set 'provider' in the configuration file. Known "
                    + "providers: " + string.Join(", ", KnownProviders.Order(StringComparer.Ordinal)) + ".");
            }

            if (!_provisioners.TryGetValue(providerName, out IResourceProvisioner? provisioner))
            {
                throw new ConfigurationException(
                    $"Provider '{providerName}' is not supported. Known providers: "
                    + string.Join(", ", KnownProviders.Order(StringComparer.Ordinal)) + ".");
            }

            return provisioner;
        }
    }
}
