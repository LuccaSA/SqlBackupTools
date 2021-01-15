using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ParallelAsync
{
    public static partial class AsyncStreamExtensions
    {
        public static AsyncStream<T> AsAsyncStream<T>(this IEnumerable<T> source, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<StreamedValue<T>>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false
            });
            return new AsyncStream<T>(channel, cancellationToken, async () =>
            {
                await AsAsyncStreamInternalAsync(source, cancellationToken, channel);
            });
        }
         
        public static AsyncStream<T> ForEachAsync<T>(this AsyncStream<T> source, Func<StreamedValue<T>, CancellationToken, Task> action)
        {
            var channel = Channel.CreateUnbounded<StreamedValue<T>>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false
            });
            return new AsyncStream<T>(channel,  source.CancellationToken, async () =>
            {
                await ForeachInternalAsync(source, action, channel); 
            });
        }
         

        private static async Task AsAsyncStreamInternalAsync<T>(IEnumerable<T> source, CancellationToken cancellationToken, Channel<StreamedValue<T>> channel)
        {
            try
            {
                foreach (var item in source)
                {
                    await channel.Writer.WriteAsync(new StreamedValue<T>(item, ExecutionStatus.Succeeded), cancellationToken);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }
         
        private static async Task ForeachInternalAsync<T>(AsyncStream<T> source, Func<StreamedValue<T>, CancellationToken, Task> action, Channel<StreamedValue<T>> channel)
        {
            try
            {
                await foreach (var item in source.ChannelReader.ReadAllAsync(source.CancellationToken))
                {
                    await action(item, source.CancellationToken);
                    await channel.Writer.WriteAsync(item, source.CancellationToken);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }
    }
}
