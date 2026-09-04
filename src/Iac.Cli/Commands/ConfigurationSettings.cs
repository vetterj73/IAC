using System.ComponentModel;

using Spectre.Console.Cli;

namespace Iac.Cli.Commands
{
    /// <summary>Options shared by every command that reads a configuration file.</summary>
    internal class ConfigurationSettings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description("Path to the YAML configuration file. Defaults to repositories.yml.")]
        public string ConfigPath { get; init; } = "repositories.yml";

        [CommandOption("-p|--provider <NAME>")]
        [Description("Overrides the 'provider' key in the configuration file, e.g. github.")]
        public string? Provider { get; init; }

        [CommandOption("-r|--repo <NAME>")]
        [Description("Act on a single repository from the file instead of all of them.")]
        public string? Repository { get; init; }
    }
}
