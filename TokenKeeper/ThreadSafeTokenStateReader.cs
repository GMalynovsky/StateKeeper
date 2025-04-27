using System;
using System.Collections.Generic;
using System.Linq;
using TokenKeeper.TokenKeeper.Core;

namespace TokenKeeper.Core
{
    /// <summary>
    /// Thread-safe implementation of the token state reader.
    /// Uses the provided lock for synchronization.
    /// </summary>
    public class ThreadSafeTokenStateReader<T> : ITokenReader<T>
    {
        private readonly TokenStateReader<T> _reader;
        private readonly object _syncRoot;

        public ThreadSafeTokenStateReader(ITokenStateStore<T> store, object syncRoot)
        {
            _reader = new TokenStateReader<T>(store);
            _syncRoot = syncRoot ?? throw new ArgumentNullException(nameof(syncRoot));
        }

        public bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot)
        {
            lock (_syncRoot)
            {
                return _reader.TryGetSnapshot(hash, out snapshot);
            }
        }

        public IEnumerable<TokenDiff<T>> GetCommittedDiff()
        {
            lock (_syncRoot)
            {
                return _reader.GetCommittedDiff().ToList(); // Make a copy while locked
            }
        }

        public IEnumerable<TokenDiff<T>> GetUncommittedDiff()
        {
            lock (_syncRoot)
            {
                return _reader.GetUncommittedDiff().ToList(); // Make a copy while locked
            }
        }

        public IEnumerable<TokenDiff<T>> GetFullDiff()
        {
            lock (_syncRoot)
            {
                return _reader.GetFullDiff().ToList(); // Make a copy while locked
            }
        }

        public IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot()
        {
            lock (_syncRoot)
            {
                return _reader.GetFullCurrentSnapshot().ToList(); // Make a copy while locked
            }
        }
    }
}