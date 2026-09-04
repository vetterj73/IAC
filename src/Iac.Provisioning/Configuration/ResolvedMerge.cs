namespace Iac.Provisioning.Configuration
{
    /// <summary>Resolved merge-strategy settings.</summary>
    public sealed class ResolvedMerge
    {
        public required bool AllowSquash { get; init; }

        public required bool AllowMergeCommit { get; init; }

        public required bool AllowRebase { get; init; }

        public required bool AllowAutoMerge { get; init; }

        public required bool DeleteBranchOnMerge { get; init; }

        public required bool AllowUpdateBranch { get; init; }
    }
}
