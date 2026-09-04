namespace Iac.Provisioning.Configuration
{
    /// <summary>Which files are seeded into the repository.</summary>
    public sealed class ResolvedFiles
    {
        public required bool Readme { get; init; }

        public required bool Codeowners { get; init; }

        public required bool PullRequestTemplate { get; init; }
    }
}
