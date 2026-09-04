using System;
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
    /// Removes what a repository's stack created. With <c>archiveOnDestroy</c> left on, the
    /// repository is archived rather than deleted.
    /// </summary>
    internal sealed class RepositoryDestroyCommand : AsyncCommand<RepositoryDestroyCommand.Settings>
    {
        protected override async Task<int> ExecuteAsync(
            CommandContext context,
            Settings settings,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(settings);

            try
            {
                if (string.IsNullOrWhiteSpace(settings.Repository))
                {
                    AnsiConsole.MarkupLine(
                        "[red]error:[/] --repo is required for destroy. Destroying every repository in a "
                        + "configuration file in one command is deliberately not supported.");
                    return 1;
                }

                PreparedRun run = RunPreparation.Prepare(
                    settings.ConfigPath,
                    settings.Provider,
                    settings.Repository,
                    AnsiConsole.WriteLine,
                    requireCredentials: true);

                ResolvedRepository repository = run.Repositories[0];
                string effect = repository.ArchiveOnDestroy
                    ? "archive (history preserved)"
                    : "PERMANENTLY DELETE, including all history";

                AnsiConsole.MarkupLine(
                    $"This will {effect.EscapeMarkup()} "
                    + $"[bold]{run.Organization.EscapeMarkup()}/{repository.Name.EscapeMarkup()}[/].");

                if (!settings.Yes && !Confirm())
                {
                    AnsiConsole.MarkupLine("Cancelled.");
                    return 1;
                }

                StackRunResult result = await run.Runner
                    .RunAsync(
                        run.Provisioner,
                        run.Organization,
                        run.Project,
                        repository,
                        StackAction.Destroy,
                        settings.Refresh,
                        cancellationToken)
                    .ConfigureAwait(false);

                AnsiConsole.MarkupLine($"[green]destroyed:[/] {result.Changes.EscapeMarkup()}");
                return 0;
            }
            catch (ConfigurationException exception)
            {
                AnsiConsole.MarkupLine($"[red]error:[/] {exception.Message.EscapeMarkup()}");
                return 1;
            }
        }

        private static bool Confirm()
        {
            if (Console.IsInputRedirected)
            {
                AnsiConsole.MarkupLine("[red]error:[/] cannot prompt for confirmation here; pass --yes.");
                return false;
            }

            return AnsiConsole.Confirm("Continue?", defaultValue: false);
        }

        internal sealed class Settings : ConfigurationSettings
        {
            [CommandOption("--yes")]
            [Description("Skip the confirmation prompt. Required when running non-interactively.")]
            public bool Yes { get; init; }

            [CommandOption("--refresh")]
            [Description("Reconcile state with the provider before acting.")]
            public bool Refresh { get; init; }
        }
    }
}
