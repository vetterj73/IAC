using System.Collections.Generic;

using Iac.Provisioning;
using Iac.Provisioning.Configuration;

namespace Iac.Cli.Execution
{
    /// <summary>Everything a command needs after the configuration has been read and checked.</summary>
    internal sealed class PreparedRun
    {
        public required IResourceProvisioner Provisioner { get; init; }

        public required string Organization { get; init; }

        /// <summary>Azure DevOps project, or null for providers without a project layer.</summary>
        public string? Project { get; init; }

        public required IReadOnlyList<ResolvedRepository> Repositories { get; init; }

        public required StackRunner Runner { get; init; }
    }
}
