namespace Iac.Provisioning.Configuration
{
    /// <summary>Where Pulumi keeps its state. State is what makes re-runs idempotent.</summary>
    public sealed class BackendOptions
    {
        /// <summary>
        /// A Pulumi backend URL. <c>file://./.pulumi-state</c> keeps state on this machine
        /// only; <c>azblob://container</c> shares it with the team. Null means Pulumi Cloud
        /// via <c>PULUMI_ACCESS_TOKEN</c>.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Pulumi project name. Defaults to <c>iac-{provider}</c>. Changing it orphans
        /// existing state, so treat it as fixed once repositories have been created.
        /// </summary>
        public string? ProjectName { get; set; }
    }
}
