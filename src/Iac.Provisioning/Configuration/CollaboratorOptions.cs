using System.Collections.Generic;

namespace Iac.Provisioning.Configuration
{
    /// <summary>Users and teams granted access to the repository. Both must already exist.</summary>
    public sealed class CollaboratorOptions
    {
        public IList<CollaboratorEntry>? Users { get; set; }

        public IList<CollaboratorEntry>? Teams { get; set; }
    }
}
