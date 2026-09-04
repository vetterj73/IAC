namespace Iac.Cli.Execution
{
    /// <summary>Outcome of one stack operation.</summary>
    public sealed class StackRunResult
    {
        /// <summary>Human-readable change tally, e.g. <c>create 4, same 1</c>.</summary>
        public required string Changes { get; init; }
    }
}
