using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Testing;

namespace Iac.Provisioning.Tests
{
    /// <summary>Helpers for reading a declared resource graph back out of Pulumi's test host.</summary>
    internal static class PulumiTestExtensions
    {
        /// <summary>
        /// Reads the value out of an <see cref="Output{T}"/>. Pulumi does not expose a public
        /// awaiter, so this hooks the continuation that <c>Apply</c> provides.
        /// </summary>
        internal static Task<T> GetValueAsync<T>(this Output<T> output)
        {
            ArgumentNullException.ThrowIfNull(output);

            TaskCompletionSource<T> completion = new();

            output.Apply(value =>
            {
                completion.TrySetResult(value);
                return value;
            });

            return completion.Task;
        }

        /// <summary>Runs a provisioner against the mocked engine and returns what it declared.</summary>
        internal static Task<ImmutableArray<Resource>> DeclareAsync(Action declare)
        {
            return Deployment.TestAsync(
                new ProviderMocks(),
                new TestOptions { IsPreview = false },
                declare);
        }
    }
}
