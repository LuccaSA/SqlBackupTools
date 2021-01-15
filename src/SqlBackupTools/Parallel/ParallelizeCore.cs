using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelAsync
{
    internal sealed class ParallelizeCore : IDisposable
    { 
        private readonly ConcurrentBag<Exception> _exceptions = new ConcurrentBag<Exception>();
        private bool _isLoopBreakRequested;
        private readonly CancellationToken _shutdownCancellationRequestToken;
        private readonly CancellationTokenSource _exceptionCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _globalCancellationTokenSource;

        public ParallelizeCore(CancellationToken shutdownCancellationToken, ParallelizeOption options)
        {
            _shutdownCancellationRequestToken = shutdownCancellationToken;
            FailMode = options.FailMode;
            _globalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, _exceptionCancellationTokenSource.Token);
        }

        public bool IsLoopBreakRequested => _isLoopBreakRequested || GlobalCancellationToken.IsCancellationRequested;
        public CancellationToken GlobalCancellationToken => _globalCancellationTokenSource.Token;
        public bool IsCanceled => _shutdownCancellationRequestToken.IsCancellationRequested;
        public bool IsFaulted => _exceptions.Count != 0;
        public IEnumerable<Exception> Exceptions => _exceptions;
        public Fail FailMode { get; }

        public void OnException(Exception e)
        {
            if (e is TaskCanceledException)
            {
                return;
            }
            _exceptions.Add(e);
            if (FailMode == Fail.Fast)
            {
                _exceptionCancellationTokenSource.Cancel();
                _isLoopBreakRequested = true;
            }
        }

        public void Dispose()
        {
            _exceptionCancellationTokenSource.Dispose();
            _globalCancellationTokenSource.Dispose();
        }
    }
}