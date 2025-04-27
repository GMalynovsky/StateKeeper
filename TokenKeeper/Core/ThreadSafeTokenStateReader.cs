using System;
using System.Collections.Generic;
using System.Linq;
using TokenKeeper.Abstraction;

namespace TokenKeeper.Core
{
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
                return _reader.GetCommittedDiff().ToList();
            }
        }

        public IEnumerable<TokenDiff<T>> GetUncommittedDiff()
        {
            lock (_syncRoot)
            {
                return _reader.GetUncommittedDiff().ToList();
            }
        }

        public IEnumerable<TokenDiff<T>> GetFullDiff()
        {
            lock (_syncRoot)
            {
                return _reader.GetFullDiff().ToList();
            }
        }

        public IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot()
        {
            lock (_syncRoot)
            {
                return _reader.GetFullCurrentSnapshot().ToList();
            }
        }
    }
}