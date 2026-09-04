using System;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// Raised for a configuration file that cannot be used: unreadable, unparseable, or
    /// missing something required. Carries a message meant to be shown to the operator.
    /// </summary>
    public sealed class ConfigurationException : Exception
    {
        public ConfigurationException()
        {
        }

        public ConfigurationException(string message)
            : base(message)
        {
        }

        public ConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
