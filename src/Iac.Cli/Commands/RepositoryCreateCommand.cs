using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Iac.Cli.Execution;
using Iac.Provisioning.Configuration;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Iac.Cli.Commands
{
    /// <summary>
    /// Creates or updates every repository in the configuration. Safe to re-run: Pulumi
    /// compares the declared state with what it recorded, so an unchanged file is a no-op.
    /// </summary>
    internal sealed class RepositoryCreateCommand : AsyncCommand<RepositoryCreateCommand.Settings>
    {
        internal sealed class Settings : ConfigurationSettings
        {
            [CommandOption("--preview")]
            [Description("Show what would change without changing anything.")]
            public bool Preview { get; init; }

            [CommandOption("--refresh")]
            [Description("Reconcile state with the provider before acting, catching drift made in the UI.")]
            public bool Refresh { get; init; }
        }

        protected override async Task<int> ExecuteAsync(
            CommandContext context,
            Settings settings,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(settings);

            try
            {
                PreparedRun run = RunPreparation.Prepare(
                    settings.ConfigPath,
                    settings.Provider,
                    settings.Repository,
                    AnsiConsole.WriteLine,
                    requireCredentials: true);

                AnsiConsole.MarkupLine(
                    $"Provider [bold]{run.Provisioner.ProviderName.EscapeMarkup()}[/], organization "
                    + $"[bold]{run.Organization.EscapeMarkup()}[/], "
                    + $"{run.Repositories.Count} repositor{(run.Repositories.Count == 1 ? "y" : "ies")}.");

                StackAction action = settings.Preview ? StackAction.Preview : StackAction.Apply;

                foreach (ResolvedRepository repository in run.Repositories)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]== {repository.Name.EscapeMarkup()}[/]");

                    foreach (string warning in run.Provisioner.DescribeUnsupportedSettings(repository))
                    {
                        AnsiConsole.MarkupLine($"[yellow]warning:[/] {warning.EscapeMarkup()}");
                    }

                    StackRunResult result = await run.Runner
                        .RunAsync(
                            run.Provisioner,
                            run.Organization,
                            repository,
                            action,
                            settings.Refresh,
                            cancellationToken)
                        .ConfigureAwait(false);

                    AnsiConsole.MarkupLine(
                        $"[green]{(settings.Preview ? "preview" : "applied")}:[/] {result.Changes.EscapeMarkup()}");
                }

                if (!settings.Preview)
                {
                    PrintManualSteps(run.Repositories);
                }

                return 0;
            }
            catch (ConfigurationException exception)
            {
                AnsiConsole.MarkupLine($"[red]error:[/] {exception.Message.EscapeMarkup()}");
                return 1;
            }
        }

        /// <summary>
        /// Things this tool deliberately cannot do, printed after a successful run so they are
        /// not discovered later by surprise.
        /// </summary>
        private static void PrintManualSteps(IReadOnlyList<ResolvedRepository> repositories)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Manual steps that cannot be automated at creation time[/]");
            AnsiConsole.MarkupLine(
                "  1. Required status checks: a check can only be required once it has reported at "
                + "least once. Add a workflow, let it run, then list it under "
                + "ruleset.requiredStatusChecks and re-run this command.");
            AnsiConsole.MarkupLine(
                "  2. Actions secrets and variables, environments and deployment protection rules.");
            AnsiConsole.MarkupLine(
                "  3. Teams must already exist in the organization before they can be collaborators "
                + "or code owners.");
            AnsiConsole.MarkupLine("  See docs/git for the full list.");

            foreach (ResolvedRepository repository in repositories)
            {
                if (repository.Ruleset.RequirePullRequest && repository.Ruleset.MinimumApprovals == 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]note:[/] {repository.Name.EscapeMarkup()} requires a pull request but zero "
                        + "approvals, so anyone with write access can self-merge.");
                }
            }
        }
    }
}
