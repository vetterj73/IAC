using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Testing;

using Xunit;

namespace Iac.Provisioning.Tests
{
    /// <summary>
    /// Pulumi's <c>Deployment.TestAsync</c> uses process-wide state and documents that tests
    /// calling it must run serially. Every resource-graph test class joins this collection so
    /// xUnit does not run them in parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class PulumiDeploymentCollection
    {
        public const string Name = "pulumi-deployment";
    }

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
            return Task.FromResult<(string?, object)>((args.Name + "-id", args.Inputs));
        }

        public Task<object> CallAsync(MockCallArgs args)
        {
            // The Azure DevOps provisioner looks up the project to get its id.
            if (args.Token?.Contains("getProject", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                Dictionary<string, object> project = new()
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

    internal static class PulumiTestExtensions
    {
        /// <summary>
        /// Reads the value out of an <see cref="Output{T}"/>. Pulumi does not expose a public
        /// awaiter, so this hooks the continuation that <c>Apply</c> provides.
        /// </summary>
        internal static Task<T> GetValueAsync<T>(this Output<T> output)
        {
            TaskCompletionSource<T> completion = new();

            output.Apply(value =>
            {
                completion.TrySetResult(value);
                return value;
            });

            return completion.Task;
        }

        /// <summary>Runs a provisioner against the mocked engine and returns what it declared.</summary>
        internal static Task<ImmutableArray<Resource>> DeclareAsync(System.Action declare)
        {
            return Deployment.TestAsync(
                new ProviderMocks(),
                new TestOptions { IsPreview = false },
                declare);
        }
    }
}
