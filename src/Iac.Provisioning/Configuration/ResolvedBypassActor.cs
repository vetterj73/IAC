namespace Iac.Provisioning.Configuration
{
    /// <summary>A resolved ruleset bypass actor.</summary>
    public sealed class ResolvedBypassActor
    {
        /// <summary>Null for actor types that have no id, such as OrganizationAdmin.</summary>
        public required int? ActorId { get; init; }

        public required string ActorType { get; init; }

        public required string BypassMode { get; init; }
    }
}
