namespace Iac.Provisioning.Configuration
{
    /// <summary>Resolved security scanning settings.</summary>
    public sealed class ResolvedSecurity
    {
        public required bool VulnerabilityAlerts { get; init; }

        public required bool SecretScanning { get; init; }

        public required bool SecretScanningPushProtection { get; init; }
    }
}
