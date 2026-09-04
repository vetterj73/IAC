using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Pulumi.Testing;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Test doubles for the Pulumi engine. Resources are never created; each one's outputs
    /// simply echo the inputs the provisioner declared, which is exactly what these tests
    /// assert on.
    /// </summary>
    internal sealed class ProviderMocks : IMocks
    {
        /// <summary>Project id handed back for the Azure DevOps project lookup.</summary>
        internal const string ProjectId = "00000000-0000-0000-0000-000000000001";

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            return Task.FromResult<(string?, object)>((args.Name + "-id", args.Inputs));
        }

        public Task<object> CallAsync(MockCallArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            // The Azure DevOps provisioner looks up the project to get its id.
            if (args.Token?.Contains("getProject", StringComparison.OrdinalIgnoreCase) == true)
            {
                Dictionary<string, object> project = new(StringComparer.Ordinal)
                {
                    ["id"] = ProjectId,
                    ["name"] = "example-project",
                    ["description"] = string.Empty,
                    ["visibility"] = "private",
                    ["versionControl"] = "Git",
                    ["workItemTemplate"] = "Agile",
                    ["processTemplateId"] = "00000000-0000-0000-0000-000000000002",
                    ["features"] = ImmutableDictionary<string, string>.Empty,
                };

                return Task.FromResult<object>(project);
            }

            return Task.FromResult<object>(args.Args);
        }
    }
}
