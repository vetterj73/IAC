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
}
