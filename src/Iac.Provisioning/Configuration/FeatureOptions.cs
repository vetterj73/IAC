namespace Iac.Provisioning.Configuration
{
    /// <summary>Per-repository feature toggles. GitHub-specific: Azure DevOps sets these per project.</summary>
    public sealed class FeatureOptions
    {
        public bool? Issues { get; set; }

        public bool? Wiki { get; set; }

        public bool? Projects { get; set; }

        public bool? Discussions { get; set; }
    }
}
