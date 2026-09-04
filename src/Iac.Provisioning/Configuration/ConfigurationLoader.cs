using System;
using System.IO;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Iac.Provisioning.Configuration
{
    /// <summary>Reads and parses the YAML configuration file.</summary>
    public static class ConfigurationLoader
    {
        /// <summary>Loads and validates the configuration at <paramref name="path"/>.</summary>
        /// <exception cref="ConfigurationException">
        /// The file is missing, unparseable, or fails validation.
        /// </exception>
        public static IacConfiguration Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ConfigurationException("No configuration file was specified.");
            }

            if (!File.Exists(path))
            {
                throw new ConfigurationException(
                    $"Configuration file '{path}' was not found. Copy examples/repositories.example.yml "
                    + "and edit it - the real config file is deliberately not committed.");
            }

            string yaml;
            try
            {
                yaml = File.ReadAllText(path);
            }
            catch (IOException exception)
            {
                throw new ConfigurationException($"Could not read '{path}': {exception.Message}", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new ConfigurationException($"Could not read '{path}': {exception.Message}", exception);
            }

            return Parse(yaml);
        }

        /// <summary>Parses and validates YAML content that has already been read.</summary>
        public static IacConfiguration Parse(string yaml)
        {
            ArgumentNullException.ThrowIfNull(yaml);

            // Unmatched properties are an error on purpose: a typo in a policy file should
            // fail loudly rather than silently leave a protection rule unset.
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            IacConfiguration? configuration;
            try
            {
                configuration = deserializer.Deserialize<IacConfiguration>(yaml);
            }
            catch (YamlException exception)
            {
                throw new ConfigurationException(
                    $"Configuration is not valid YAML or has an unrecognized setting: {exception.Message}",
                    exception);
            }

            if (configuration is null)
            {
                throw new ConfigurationException("Configuration file is empty.");
            }

            ConfigurationValidator.Validate(configuration);
            return configuration;
        }
    }
}
