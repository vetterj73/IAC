using System;
using System.Collections.Generic;
using System.Text;

namespace Iac.Provisioning.GitHub
{
    /// <summary>
    /// Builds the CODEOWNERS file. This is how a list of approvers becomes enforceable on
    /// GitHub: the ruleset's <c>requireCodeOwnerReview</c> rule only has teeth when the
    /// changed paths have an owner.
    /// </summary>
    public static class CodeownersFile
    {
        /// <summary>Path GitHub reads owners from.</summary>
        public const string Path = ".github/CODEOWNERS";

        /// <summary>Renders a CODEOWNERS file assigning every path to <paramref name="approvers"/>.</summary>
        public static string Build(IReadOnlyList<string> approvers)
        {
            ArgumentNullException.ThrowIfNull(approvers);

            StringBuilder builder = new();
            builder.AppendLine("# Managed by the iac CLI - see docs/git in the IAC repository.");
            builder.AppendLine("# Local edits are overwritten on the next 'iac repo create' run.");
            builder.AppendLine();

            if (approvers.Count == 0)
            {
                builder.AppendLine("# No approvers configured.");
                return builder.ToString();
            }

            builder.AppendLine("* " + string.Join(" ", approvers));
            return builder.ToString();
        }
    }
}
