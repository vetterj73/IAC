namespace Iac.Provisioning.Configuration
{
    /// <summary>A resolved collaborator and its permission level.</summary>
    public sealed class ResolvedCollaborator
    {
        public required string Name { get; init; }

        public required string Permission { get; init; }
    }
}
