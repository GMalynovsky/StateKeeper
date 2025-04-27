using System;
using System.Collections.Generic;
using System.Linq;
using TokenKeeper.Abstraction;

namespace TokenKeeper.Core
{
    internal class DictionaryTokenStateStore<T> : ITokenStateStore<T>
    {
        private readonly Dictionary<Guid, TokenState> _states = new Dictionary<Guid, TokenState>();
        private readonly Dictionary<Guid, long?> _staging = new Dictionary<Guid, long?>();
        private readonly Dictionary<long, Guid> _hash2Id = new Dictionary<long, Guid>();
        private readonly Dictionary<long, T> _pool = new Dictionary<long, T>();
        private readonly Dictionary<Guid, T> _initialValueSanctuary = new Dictionary<Guid, T>();
        private readonly List<Tuple<Guid, long?, long?>> _committedChanges = new List<Tuple<Guid, long?, long?>>();

        public bool TryGetTokenState(Guid id, out TokenState state)
        {
            return _states.TryGetValue(id, out state);
        }

        public void SetTokenState(Guid id, TokenState state)
        {
            _states[id] = state;
        }

        public IEnumerable<KeyValuePair<Guid, TokenState>> GetAllTokenStates()
        {
            return _states;
        }

        public bool TryGetTokenId(long hash, out Guid id)
        {
            return _hash2Id.TryGetValue(hash, out id);
        }

        public void MapHashToId(long hash, Guid id)
        {
            _hash2Id[hash] = id;
        }

        public void RemoveHashMapping(long hash)
        {
            _hash2Id.Remove(hash);
        }

        public bool TryGetValue(long hash, out T value)
        {
            return _pool.TryGetValue(hash, out value);
        }

        public void StoreValue(long hash, T value)
        {
            _pool[hash] = value;
        }

        public bool TryGetInitialValue(Guid id, out T value)
        {
            return _initialValueSanctuary.TryGetValue(id, out value);
        }

        public void StoreInitialValue(Guid id, T value)
        {
            _initialValueSanctuary[id] = value;
        }

        public void StageChange(Guid id, long? newHash)
        {
            _staging[id] = newHash;
        }

        public void ClearStagedChanges()
        {
            _staging.Clear();
        }

        public bool TryGetStagedHash(Guid id, out long? hash)
        {
            if (_staging.TryGetValue(id, out hash))
                return true;

            hash = null;
            return false;
        }

        public bool HasStagedChanges(Guid id)
        {
            return _staging.ContainsKey(id);
        }

        public IReadOnlyDictionary<Guid, long?> GetAllStagedChanges()
        {
            return _staging;
        }

        public void RecordCommittedChange(Guid id, long? oldHash, long? newHash)
        {
            _committedChanges.Add(new Tuple<Guid, long?, long?>(id, oldHash, newHash));
        }

        public void ClearCommittedChanges()
        {
            _committedChanges.Clear();
        }

        public IEnumerable<Tuple<Guid, long?, long?>> GetCommittedChanges()
        {
            return _committedChanges;
        }

        public void Prune(ISet<long> liveHashes)
        {
            var deadHashes = _pool.Keys.Where(h => !liveHashes.Contains(h)).ToList();
            foreach (var hash in deadHashes)
            {
                _pool.Remove(hash);
            }
        }
    }
}
