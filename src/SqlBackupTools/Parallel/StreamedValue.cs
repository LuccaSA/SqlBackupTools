using System;

namespace ParallelAsync
{
    public class StreamedValue<T>
    {
        public StreamedValue(T item, ExecutionStatus status)
        {
            Item = item;
            Status = status;
            Exception = null;
        }
        public StreamedValue(T item, ExecutionStatus status, Exception exception)
        {
            Item = item;
            Status = status;
            Exception = exception;
        }
        public T Item { get; }
        public ExecutionStatus Status { get; }
        public Exception Exception { get; }
    }

    public class StreamedValue<T, TSource> : StreamedValue<T>
    {
        public StreamedValue(T item, TSource source, ExecutionStatus status)
        : base(item, status)
        {
            ItemSource = source;
        }
        public StreamedValue(T item, TSource source, ExecutionStatus status, Exception exception)
            : base(item, status, exception)
        {
            ItemSource = source;
        }
        public TSource ItemSource { get; }
    }
}