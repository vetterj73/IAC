namespace Iac.Provisioning.Configuration
{
    /// <summary>Files this tool seeds into a new repository. All are safe to edit afterwards.</summary>
    public sealed class GeneratedFileOptions
    {
        public bool? Readme { get; set; }

        public bool? Codeowners { get; set; }

        public bool? PullRequestTemplate { get; set; }
    }
}
