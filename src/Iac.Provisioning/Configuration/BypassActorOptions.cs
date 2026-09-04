namespace Iac.Provisioning.Configuration
{
    /// <summary>An actor allowed to bypass the ruleset - typically a break-glass admin path.</summary>
    public sealed class BypassActorOptions
    {
        /// <summary>
        /// Numeric id of the actor. Required for <c>RepositoryRole</c>, <c>Team</c>,
        /// <c>Integration</c> and <c>User</c>; it must be <b>omitted</b> for
        /// <c>OrganizationAdmin</c>, <c>EnterpriseOwner</c> and <c>DeployKey</c>, which have no
        /// id - GitHub ignores one if sent.
        /// </summary>
        public int? ActorId { get; set; }

        /// <summary>
        /// One of <c>RepositoryRole</c>, <c>Team</c>, <c>Integration</c>,
        /// <c>OrganizationAdmin</c>, <c>DeployKey</c>, <c>EnterpriseOwner</c>, <c>User</c>.
        /// Case-sensitive.
        /// </summary>
        public string? ActorType { get; set; }

        /// <summary><c>always</c>, <c>pull_request</c> or <c>exempt</c>. Case-sensitive.</summary>
        public string? BypassMode { get; set; }
    }
}
