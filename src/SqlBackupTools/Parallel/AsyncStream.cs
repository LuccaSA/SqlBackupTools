using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ParallelAsync
{
    public class AsyncStream<T>
    {
        public AsyncStream(Channel<StreamedValue<T>> channel, CancellationToken cancellationToken, Func<Task> innerTask)
        {
            _channel = channel;
            Task = Task.Run(innerTask);
            CancellationToken = cancellationToken;
        }

        private readonly Channel<StreamedValue<T>> _channel;
        public ChannelReader<StreamedValue<T>> ChannelReader => _channel.Reader;
        public CancellationToken CancellationToken { get; }
        public Task Task { get;}

        public TaskAwaiter GetAwaiter()
        {
            return Task.GetAwaiter();
        }
    }
}