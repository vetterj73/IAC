namespace Iac.Cli.Execution
{
    /// <summary>What to do with a stack.</summary>
    public enum StackAction
    {
        /// <summary>Report the changes that would be made, change nothing.</summary>
        Preview,

        /// <summary>Apply the configuration.</summary>
        Apply,

        /// <summary>Remove (or archive) what this stack created.</summary>
        Destroy,
    }
}
