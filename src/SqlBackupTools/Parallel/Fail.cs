namespace ParallelAsync
{
    public enum Fail
    {
        /// <summary>
        /// Don't fail loop on exception, return a fault task
        /// If loop is canceled, returns a canceled task
        /// All items are yielded : 
        /// Canceled items are yield as ParallelResult with ExecutionState Canceled
        /// Unprocessed items are yield as ParallelResult with ExecutionState NotExecuted 
        /// </summary>
        Default,
        /// <summary>
        /// Fail loop as soon as an exception happens, return a successful task, with exceptions in ParallelizedSummary
        /// If loop is canceled, returns a successful task.
        /// Some canceled items can be yielded as ParallelResult with ExecutionState Canceled or NotExecuted (but not all)
        /// Most unprocessed items are not yielded
        /// </summary>
        Fast,
        /// <summary>
        /// Don't fail loop on exception, return a successful task, with exceptions in ParallelizedSummary
        /// If loop is canceled, returns a successful task.
        /// All items are yielded : 
        /// Canceled items are yield as ParallelResult with ExecutionState Canceled
        /// Unprocessed items are yield as ParallelResult with ExecutionState NotExecuted 
        /// </summary>
        Smart
    }
}