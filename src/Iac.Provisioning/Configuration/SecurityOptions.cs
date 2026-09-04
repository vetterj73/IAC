namespace Iac.Provisioning.Configuration
{
    /// <summary>Security scanning settings. Most require GitHub Advanced Security.</summary>
    public sealed class SecurityOptions
    {
        public bool? VulnerabilityAlerts { get; set; }

        /// <summary>Requires GitHub Advanced Security on private repositories.</summary>
        public bool? SecretScanning { get; set; }

        /// <summary>Requires GitHub Advanced Security on private repositories.</summary>
        public bool? SecretScanningPushProtection { get; set; }
    }
}
