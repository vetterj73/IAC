using System.Threading.Tasks;

using Iac.Cli.Commands;

using Spectre.Console.Cli;

namespace Iac.Cli
{
    /// <summary>
    /// Entry point. Commands are grouped by resource type rather than by provider, because the
    /// destination is a configuration setting - the same 'repo create' works against GitHub or
    /// Azure DevOps depending on the file's 'provider' key.
    /// </summary>
    internal static class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            CommandApp app = new();

            app.Configure(config =>
            {
                config.SetApplicationName("iac");
                config.UseStrictParsing();
                config.ValidateExamples();

                config.AddBranch("repo", repo =>
                {
                    repo.SetDescription("Source-control repositories and their branch policies.");

                    repo.AddCommand<RepositoryCreateCommand>("create")
                        .WithDescription("Create or update the repositories in a configuration file.")
                        .WithExample("repo", "create", "--config", "repositories.yml")
                        .WithExample("repo", "create", "--config", "repositories.yml", "--preview");

                    repo.AddCommand<RepositoryDestroyCommand>("destroy")
                        .WithDescription("Archive or delete one repository's resources.")
                        .WithExample("repo", "destroy", "--config", "repositories.yml", "--repo", "my-service");
                });

                config.AddBranch("config", configuration =>
                {
                    configuration.SetDescription("Work with configuration files.");

                    configuration.AddCommand<ConfigValidateCommand>("validate")
                        .WithDescription("Validate a configuration file and show the resolved settings.")
                        .WithExample("config", "validate", "--config", "repositories.yml");
                });
            });

            return await app.RunAsync(args).ConfigureAwait(false);
        }
    }
}
