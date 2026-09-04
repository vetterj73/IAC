namespace Iac.Provisioning.Configuration
{
    /// <summary>Which merge strategies a repository permits.</summary>
    public sealed class MergeOptions
    {
        public bool? AllowSquash { get; set; }

        public bool? AllowMergeCommit { get; set; }

        public bool? AllowRebase { get; set; }

        public bool? AllowAutoMerge { get; set; }

        public bool? DeleteBranchOnMerge { get; set; }

        public bool? AllowUpdateBranch { get; set; }
    }
}
