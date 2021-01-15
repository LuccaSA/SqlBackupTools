using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ParallelAsync
{
    public static partial class AsyncStreamExtensions
    {
        public static AsyncStream<TResult> ParallelizeAsync<T, TResult>(this IEnumerable<T> source,
            Func<T, CancellationToken, Task<TResult>> actionAsync, ParallelizeOption option, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            return source.AsAsyncStream(cancellationToken).ParallelizeStreamAsync(actionAsync, option);
        }
        
        public static AsyncStream<TResult> ParallelizeStreamAsync<T, TResult>(this AsyncStream<T> source,
            Func<T, CancellationToken, Task<TResult>> actionAsync, ParallelizeOption option)
        {
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            return ParallelizeStreamInternalAsync(source, actionAsync, option);
        }

        private static AsyncStream<TResult> ParallelizeStreamInternalAsync<T, TResult>(this AsyncStream<T> source, 
            Func<T, CancellationToken, Task<TResult>> actionAsync, ParallelizeOption option)
        {
            var core = new ParallelizeCore(source.CancellationToken, option);
            var monitor = new ParallelMonitor<T>(option.MaxDegreeOfParallelism);
            var channel = Channel.CreateUnbounded<StreamedValue<TResult>>();

            return new AsyncStream<TResult>(channel, source.CancellationToken, async () =>
            {
                try
                {
                    using (core)
                    {
                        await Task.WhenAll(Enumerable.Range(0, option.MaxDegreeOfParallelism)
                            .Select(i => ParallelizeCoreStreamAsync(core, actionAsync, source, channel, i, monitor)));
                    }
                }
                catch (Exception e)
                {
                    channel.Writer.Complete(e);
                    throw;
                }
                finally
                {
                    channel.Writer.Complete();
                }
                ThrowOnErrors(option, core);
            });
        }
         
        private static void ThrowOnErrors(ParallelizeOption option, ParallelizeCore core)
        {
            if (option.FailMode != Fail.Default)
            {
                return;
            }

            if (core.IsFaulted)
            {
                if (core.Exceptions.Count() == 1)
                {
                    throw core.Exceptions.First();
                }

                throw new AggregateException(core.Exceptions);
            }

            if (core.IsCanceled)
            {
                throw new TaskCanceledException();
            }
        }

        private static Task ParallelizeCoreStreamAsync<T,TResult>(ParallelizeCore core,
            Func<T, CancellationToken, Task<TResult>> actionAsync,
            AsyncStream<T> source,
            ChannelWriter<StreamedValue<TResult>> resultsChannel,
            int index,
            ParallelMonitor<T> monitor)
        {
            return Task.Run(async () =>
            {
                while (await source.ChannelReader.WaitToReadAsync()) //returns false when the channel is completed
                {
                    while (source.ChannelReader.TryRead(out StreamedValue<T> streamedValue))
                    {
                        if (streamedValue.Status != ExecutionStatus.Succeeded)
                        {
                            continue;
                        }

                        var item = streamedValue.Item;
                        monitor.SetActive(index, item);
                        if (core.IsLoopBreakRequested)
                        {
                            await YieldNotExecutedAsync(resultsChannel,default, item);
                            monitor.SetInactive(index);
                            if (core.FailMode == Fail.Fast)
                            {
                                return;
                            }
                            break;
                        }

                        TResult result = default;
                        try
                        {
                            result = await actionAsync(item, core.GlobalCancellationToken);
                            await YieldExecutedAsync(resultsChannel, result,item);
                        }
                        catch (TaskCanceledException tce)
                        {
                            await YieldCanceledAsync(resultsChannel, result, item, tce);
                        }
                        catch (Exception e)
                        {
                            await YieldFailedAsync(resultsChannel, result, item, e);
                            core.OnException(e);
                        }
                        monitor.SetInactive(index);
                    }
                }
            });
        }

        private static async Task YieldNotExecutedAsync<T, TSource>(ChannelWriter<StreamedValue<T>> resultsChannel, T item, TSource source)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new StreamedValue<T, TSource>(item, source, ExecutionStatus.Pending));
            }
        }

        private static async Task YieldExecutedAsync<T, TSource>(ChannelWriter<StreamedValue<T>> resultsChannel, T item, TSource source)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new StreamedValue<T, TSource>(item, source, ExecutionStatus.Succeeded));
            }
        }

        private static async Task YieldFailedAsync<T, TSource>(ChannelWriter<StreamedValue<T>> resultsChannel, T item, TSource source, Exception e)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new StreamedValue<T, TSource>(item, source, ExecutionStatus.Faulted, e));
            }
        }

        private static async Task YieldCanceledAsync<T, TSource>(ChannelWriter<StreamedValue<T>> resultsChannel, T item, TSource source, Exception tce)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new StreamedValue<T, TSource>(item, source, ExecutionStatus.Canceled, tce));
            }
        }

    }
}
