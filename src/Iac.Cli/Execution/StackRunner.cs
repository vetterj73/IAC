using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Iac.Provisioning;
using Iac.Provisioning.Configuration;

using Pulumi.Automation;

namespace Iac.Cli.Execution
{
    /// <summary>What to do with a stack.</summary>
    public enum StackAction
    {
        /// <summary>Report the changes that would be made, change nothing.</summary>
        Preview,

        /// <summary>Apply the configuration.</summary>
        Apply,

        /// <summary>Remove (or archive) what this stack created.</summary>
        Destroy,
    }

    /// <summary>
    /// Drives Pulumi through the Automation API with the provisioner's program supplied
    /// inline, so there is no separate Pulumi project on disk and no <c>pulumi</c> CLI
    /// invocation to script.
    /// </summary>
    /// <remarks>
    /// One stack per repository, named after the repository. That is what makes runs
    /// idempotent and independent: re-running an unchanged repository reports no changes, and
    /// adding a repository to the configuration never touches the others' state.
    /// </remarks>
    public sealed class StackRunner
    {
        private readonly string _projectName;
        private readonly string? _backendUrl;
        private readonly Action<string> _write;

        public StackRunner(string projectName, string? backendUrl, Action<string> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
            ArgumentNullException.ThrowIfNull(write);

            _projectName = projectName;
            _backendUrl = backendUrl;
            _write = write;
        }

        /// <summary>True when state lives outside Pulumi Cloud, which changes secret handling.</summary>
        private bool IsSelfManagedBackend => !string.IsNullOrWhiteSpace(_backendUrl);

        /// <summary>Runs one repository through the given action.</summary>
        public async Task<StackRunResult> RunAsync(
            IResourceProvisioner provisioner,
            string organization,
            ResolvedRepository repository,
            StackAction action,
            bool refresh,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(provisioner);
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(organization);

            ProvisioningContext context = new()
            {
                Organization = organization,
                Repository = repository,
            };

            PulumiFn program = PulumiFn.Create(() => provisioner.Provision(context));

            ProjectSettings projectSettings = new(_projectName, ProjectRuntimeName.Dotnet);
            if (IsSelfManagedBackend)
            {
                projectSettings.Backend = new ProjectBackend { Url = _backendUrl };
            }

            InlineProgramArgs stackArgs = new(_projectName, repository.Name, program)
            {
                ProjectSettings = projectSettings,
                EnvironmentVariables = BuildEnvironmentVariables(),
            };

            using WorkspaceStack stack =
                await LocalWorkspace.CreateOrSelectStackAsync(stackArgs, cancellationToken).ConfigureAwait(false);

            foreach (KeyValuePair<string, string> setting in provisioner.BuildStackConfiguration(organization))
            {
                await stack
                    .SetConfigAsync(setting.Key, new ConfigValue(setting.Value), cancellationToken)
                    .ConfigureAwait(false);
            }

            return action switch
            {
                StackAction.Preview => await PreviewAsync(stack, refresh, cancellationToken).ConfigureAwait(false),
                StackAction.Apply => await ApplyAsync(stack, refresh, cancellationToken).ConfigureAwait(false),
                StackAction.Destroy => await DestroyAsync(stack, refresh, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown stack action."),
            };
        }

        private async Task<StackRunResult> PreviewAsync(
            WorkspaceStack stack,
            bool refresh,
            CancellationToken cancellationToken)
        {
            PreviewResult result = await stack
                .PreviewAsync(
                    new PreviewOptions { OnStandardOutput = _write, Refresh = refresh },
                    cancellationToken)
                .ConfigureAwait(false);

            return new StackRunResult
            {
                Changes = Describe(result.ChangeSummary),
            };
        }

        private async Task<StackRunResult> ApplyAsync(
            WorkspaceStack stack,
            bool refresh,
            CancellationToken cancellationToken)
        {
            UpResult result = await stack
                .UpAsync(new UpOptions { OnStandardOutput = _write, Refresh = refresh }, cancellationToken)
                .ConfigureAwait(false);

            return new StackRunResult
            {
                Changes = Describe(result.Summary.ResourceChanges),
            };
        }

        private async Task<StackRunResult> DestroyAsync(
            WorkspaceStack stack,
            bool refresh,
            CancellationToken cancellationToken)
        {
            UpdateResult result = await stack
                .DestroyAsync(new DestroyOptions { OnStandardOutput = _write, Refresh = refresh }, cancellationToken)
                .ConfigureAwait(false);

            return new StackRunResult
            {
                Changes = Describe(result.Summary.ResourceChanges),
            };
        }

        /// <summary>
        /// A self-managed backend encrypts stack secrets with a passphrase. This tool stores no
        /// secrets in stack config - the GitHub token is read from the environment by the
        /// provider - so an unset passphrase is defaulted to empty rather than failing the run.
        /// </summary>
        private Dictionary<string, string?> BuildEnvironmentVariables()
        {
            Dictionary<string, string?> variables = new(StringComparer.Ordinal);

            if (IsSelfManagedBackend
                && Environment.GetEnvironmentVariable("PULUMI_CONFIG_PASSPHRASE") is null
                && Environment.GetEnvironmentVariable("PULUMI_CONFIG_PASSPHRASE_FILE") is null)
            {
                variables["PULUMI_CONFIG_PASSPHRASE"] = string.Empty;
            }

            return variables;
        }

        private static string Describe(IImmutableDictionary<OperationType, int>? changes)
        {
            if (changes is null || changes.Count == 0)
            {
                return "no changes";
            }

            IEnumerable<string> parts = changes
                .OrderBy(static change => change.Key)
                .Select(static change =>
                    change.Key.ToString().ToLowerInvariant()
                    + " " + change.Value.ToString(CultureInfo.InvariantCulture));

            return string.Join(", ", parts);
        }
    }

    /// <summary>Outcome of one stack operation.</summary>
    public sealed class StackRunResult
    {
        /// <summary>Human-readable change tally, e.g. <c>create 4, same 1</c>.</summary>
        public required string Changes { get; init; }
    }
}
