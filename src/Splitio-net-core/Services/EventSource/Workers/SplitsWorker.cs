﻿using Splitio.Services.Cache.Interfaces;
using Splitio.Services.Logger;
using Splitio.Services.Shared.Classes;
using Splitio.Services.SplitFetcher.Interfaces;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Splitio.Services.EventSource.Workers
{
    public class SplitsWorker : ISplitsWorker
    {
        private readonly ISplitLogger _log;
        private readonly ISplitFetcher _splitFetcher;
        private readonly ISplitCache _splitCache;
        private readonly BlockingCollection<long> _queue;

        private CancellationTokenSource _cancellationTokenSource;

        public SplitsWorker(ISplitFetcher splitFetcher,
            ISplitCache splitCache,
            ISplitLogger log = null)
        {
            _splitFetcher = splitFetcher;
            _splitCache = splitCache;
            _log = log ?? WrapperAdapter.GetLogger(typeof(SplitsWorker));
            _queue = new BlockingCollection<long>(new ConcurrentQueue<long>());
        }

        #region Public Methods
        public void AddToQueue(long changeNumber)
        {
            _queue.TryAdd(changeNumber);
        }

        public void KillSplit(long changeNumber, string splitName, string defaultTreatment)
        {
            _splitCache.Kill(changeNumber, splitName, defaultTreatment);
            AddToQueue(changeNumber);
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => Execute(), _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }
        #endregion

        #region Private Mthods
        private void Execute()
        {
            while (true)
            {
                //Wait indefinitely until a segment is queued
                if (_queue.TryTake(out long changeNumber, -1))
                {
                    _log.Debug($"ChangeNumber dequeue: {changeNumber}");

                    if (changeNumber > _splitCache.GetChangeNumber())
                    {
                        // TODO: change this after synchronizer implementation.
                        _splitFetcher.Fetch();
                    }
                }
            }
        }
        #endregion
    }
}
