using System;
using System.Threading;

using Iac.Cli.Execution;
using Iac.Provisioning.Configuration;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Iac.Cli.Commands
{
    /// <summary>
    /// Checks a configuration file and prints the settings each repository would get after
    /// inheritance, without contacting any provider or needing credentials.
    /// </summary>
    internal sealed class ConfigValidateCommand : Command<ConfigurationSettings>
    {
        protected override int Execute(
            CommandContext context,
            ConfigurationSettings settings,
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
                    requireCredentials: false);

                Table table = new();
                table.AddColumn("repository");
                table.AddColumn("vis");
                table.AddColumn("branch");
                table.AddColumn("enforce");
                table.AddColumn("appr");
                table.AddColumn("owners");
                table.AddColumn("approvers");

                foreach (ResolvedRepository repository in run.Repositories)
                {
                    table.AddRow(
                        repository.Name.EscapeMarkup(),
                        repository.Visibility.EscapeMarkup(),
                        repository.DefaultBranch.EscapeMarkup(),
                        repository.Ruleset.Enforcement.EscapeMarkup(),
                        repository.Ruleset.RequirePullRequest
                            ? repository.Ruleset.MinimumApprovals.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : "no PR required",
                        repository.Ruleset.RequireCodeOwnerReview ? "required" : "-",
                        repository.Approvers.Count == 0
                            ? "-"
                            : string.Join(" ", repository.Approvers).EscapeMarkup());
                }

                AnsiConsole.Write(table);

                foreach (ResolvedRepository repository in run.Repositories)
                {
                    foreach (string warning in run.Provisioner.DescribeUnsupportedSettings(repository))
                    {
                        AnsiConsole.MarkupLine(
                            $"[yellow]warning ({repository.Name.EscapeMarkup()}):[/] {warning.EscapeMarkup()}");
                    }
                }

                AnsiConsole.MarkupLine("[green]configuration is valid.[/]");
                return 0;
            }
            catch (ConfigurationException exception)
            {
                AnsiConsole.MarkupLine($"[red]error:[/] {exception.Message.EscapeMarkup()}");
                return 1;
            }
        }
    }
}
