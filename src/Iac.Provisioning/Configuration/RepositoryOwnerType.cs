namespace Iac.Provisioning.Configuration
{
    /// <summary>Who owns the repositories, which changes what policies are even possible.</summary>
    public enum RepositoryOwnerType
    {
        /// <summary>An organization: teams, organization admins and evaluate mode all exist.</summary>
        Organization,

        /// <summary>
        /// A single user account. No teams, no organization admin to bypass a ruleset, and no
        /// second person to approve a pull request.
        /// </summary>
        User,
    }
}
