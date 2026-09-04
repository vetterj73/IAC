namespace Iac.Provisioning.Configuration
{
    /// <summary>Resolved per-repository feature toggles.</summary>
    public sealed class ResolvedFeatures
    {
        public required bool Issues { get; init; }

        public required bool Wiki { get; init; }

        public required bool Projects { get; init; }

        public required bool Discussions { get; init; }
    }
}
