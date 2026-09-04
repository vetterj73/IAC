namespace Iac.Provisioning.Configuration
{
    /// <summary>One collaborator - a user login or a team slug - and its permission level.</summary>
    public sealed class CollaboratorEntry
    {
        /// <summary>User login, or team slug for a team entry.</summary>
        public string? Name { get; set; }

        /// <summary><c>pull</c>, <c>triage</c>, <c>push</c>, <c>maintain</c> or <c>admin</c>.</summary>
        public string? Permission { get; set; }
    }
}
