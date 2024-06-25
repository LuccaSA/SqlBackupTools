using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace SqlBackupTools.SerilogAsync
{
    internal sealed class BackgroundWorkerSink : ILogEventSink, IDisposable
    {
        private readonly ILogEventSink _wrappedSink;
        private readonly Channel<LogEvent> _channel;
        private readonly Task _worker;

        public BackgroundWorkerSink(ILogEventSink wrappedSink)
        {
            _wrappedSink = wrappedSink ?? throw new ArgumentNullException(nameof(wrappedSink));
            _channel = Channel.CreateUnbounded<LogEvent>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleWriter = false,
                SingleReader = false
            });

            _worker = Task.Factory
                .StartNew(PumpAsync, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .Unwrap();
        }

        public void Emit(LogEvent logEvent)
        {
            _channel.Writer.TryWrite(logEvent);
        }

        public void Dispose()
        {
            // Prevent any more events from being added
            _channel.Writer.Complete(); 

            // Allow queued events to be flushed
            _worker
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            (_wrappedSink as IDisposable)?.Dispose();
        }

        private async Task PumpAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync())
                {
                    var logEvent = await _channel.Reader.ReadAsync();
                    try
                    {
                        _wrappedSink.Emit(logEvent);
                    }
                    catch (Exception ex)
                    {
                        SelfLog.WriteLine("{0} failed to emit event to wrapped sink: {1}", typeof(BackgroundWorkerSink), ex);
                    }
                }
            }
            catch (Exception fatal)
            {
                SelfLog.WriteLine("{0} fatal error in worker thread: {1}", typeof(BackgroundWorkerSink), fatal);
            }
        }
    }
}
