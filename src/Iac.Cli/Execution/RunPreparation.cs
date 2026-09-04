using System;
using System.Collections.Generic;
using System.Linq;

using Iac.Provisioning;
using Iac.Provisioning.Configuration;
using Iac.Provisioning.GitHub;

namespace Iac.Cli.Execution
{
    /// <summary>Everything a command needs after the configuration has been read and checked.</summary>
    internal sealed class PreparedRun
    {
        public required IResourceProvisioner Provisioner { get; init; }

        public required string Organization { get; init; }

        public required IReadOnlyList<ResolvedRepository> Repositories { get; init; }

        public required StackRunner Runner { get; init; }
    }

    /// <summary>
    /// Shared front half of every command: load, validate, resolve, pick a provider, check the
    /// environment. Kept in one place so create, destroy and validate cannot drift apart.
    /// </summary>
    internal static class RunPreparation
    {
        /// <summary>
        /// The registry is the only place that lists destinations. Adding Azure DevOps means
        /// adding its provisioner here.
        /// </summary>
        public static ProvisionerRegistry CreateRegistry()
        {
            return new ProvisionerRegistry([new GitHubRepositoryProvisioner()]);
        }

        /// <summary>Loads and prepares a run.</summary>
        /// <exception cref="ConfigurationException">
        /// The configuration is unusable, the provider is unknown, the repository filter matches
        /// nothing, or required credentials are absent.
        /// </exception>
        public static PreparedRun Prepare(
            string configPath,
            string? providerOverride,
            string? repositoryFilter,
            Action<string> write,
            bool requireCredentials)
        {
            ArgumentNullException.ThrowIfNull(write);

            IacConfiguration configuration = ConfigurationLoader.Load(configPath);

            ProvisionerRegistry registry = CreateRegistry();
            IResourceProvisioner provisioner = registry.Resolve(providerOverride ?? configuration.Provider);

            if (requireCredentials)
            {
                VerifyCredentials(provisioner);
            }

            IReadOnlyList<ResolvedRepository> repositories = RepositoryResolver.ResolveAll(configuration);
            foreach (ResolvedRepository repository in repositories)
            {
                ConfigurationValidator.ValidateResolved(repository);
            }

            if (!string.IsNullOrWhiteSpace(repositoryFilter))
            {
                repositories = repositories
                    .Where(repository =>
                        string.Equals(repository.Name, repositoryFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (repositories.Count == 0)
                {
                    throw new ConfigurationException(
                        $"No repository named '{repositoryFilter}' is defined in '{configPath}'.");
                }
            }

            // Organization presence is already validated; the null-forgiving read is safe here.
            string organization = configuration.Organization!;
            string projectName = configuration.Backend?.ProjectName
                ?? "iac-" + provisioner.ProviderName;

            StackRunner runner = new(projectName, configuration.Backend?.Url, write);

            return new PreparedRun
            {
                Provisioner = provisioner,
                Organization = organization,
                Repositories = repositories,
                Runner = runner,
            };
        }

        private static void VerifyCredentials(IResourceProvisioner provisioner)
        {
            List<string> missing = provisioner.RequiredEnvironmentVariables
                .Where(static variable =>
                    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
                .ToList();

            if (missing.Count > 0)
            {
                throw new ConfigurationException(
                    $"Provider '{provisioner.ProviderName}' needs these environment variables to be set: "
                    + string.Join(", ", missing)
                    + ". See docs/git for the token scopes required.");
            }
        }
    }
}
