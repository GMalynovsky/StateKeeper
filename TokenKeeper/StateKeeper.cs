using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenKeeper
{
    internal struct TokenState
    {
        public long? Initial;
        public long? Previous;
        public long? Current;
    }

    public readonly struct TokenSnapshot<T>
    {
        public TokenSnapshot(long? initialHash, long? previousHash, long? currentHash, T initialValue, T previousValue, T currentValue)
        {
            InitialHash = initialHash;
            PreviousHash = previousHash;
            CurrentHash = currentHash;
            InitialValue = initialValue;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
        public long? InitialHash { get; }
        public long? PreviousHash { get; }
        public long? CurrentHash { get; }
        public T InitialValue { get; }
        public T PreviousValue { get; }
        public T CurrentValue { get; }
    }

    public readonly struct TokenDiff<T>
    {
        public TokenDiff(long? leftHash, long? rightHash, T leftValue, T rightValue)
        {
            LeftHash = leftHash;
            RightHash = rightHash;
            LeftValue = leftValue;
            RightValue = rightValue;
        }
        public long? LeftHash { get; }
        public long? RightHash { get; }
        public T LeftValue { get; }
        public T RightValue { get; }
    }

    public enum TokenOpResult
    {
        Success,
        DuplicateHash,
        UnknownHash,
        Collision,
        AlreadyStaged,
        InvalidInput
    }

    public interface ITokenInitializer<T>
    {
        TokenOpResult Seed(long hash, T value);
    }

    public interface ITokenMutator<T>
    {
        TokenOpResult Stage(long? oldHash, long? newHash, T value);
        void Commit();
        void Discard();
    }

    public interface ITokenReader<T>
    {
        bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot);
        IEnumerable<TokenDiff<T>> GetCommittedDiff();
        IEnumerable<TokenDiff<T>> GetUncommittedDiff();
        IEnumerable<TokenDiff<T>> GetFullDiff();
    }

    internal abstract class StateKeeper<T> : ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>
    {
        private readonly Dictionary<Guid, TokenState> _states = new Dictionary<Guid, TokenState>();
        private readonly Dictionary<Guid, long?> _staging = new Dictionary<Guid, long?>();
        private readonly Dictionary<long, Guid> _hash2Id = new Dictionary<long, Guid>();
        private readonly Dictionary<long, T> _pool = new Dictionary<long, T>();
        private readonly object _sync = new object();

        public TokenOpResult Seed(long hash, T value)
        {
            lock (_sync)
            {
                if (_hash2Id.ContainsKey(hash)) return TokenOpResult.DuplicateHash;
                var id = Guid.NewGuid();
                _hash2Id[hash] = id;
                _pool[hash] = value;
                _states[id] = new TokenState { Initial = hash, Previous = hash, Current = hash };
                return TokenOpResult.Success;
            }
        }

        public TokenOpResult Stage(long? oldHash, long? newHash, T value)
        {
            lock (_sync)
            {
                if (oldHash.HasValue && !newHash.HasValue) return StageDelete(oldHash.Value);
                if (!oldHash.HasValue && newHash.HasValue) return StageInsert(newHash.Value, value);
                if (oldHash.HasValue && newHash.HasValue) return StageModify(oldHash.Value, newHash.Value, value);
                return TokenOpResult.InvalidInput;
            }
        }

        public void Commit()
        {
            lock (_sync)
            {
                foreach (var kv in _staging)
                {
                    var id = kv.Key;
                    var hash = kv.Value;
                    var st = _states[id];

                    // Remove previous hash mapping if it exists and is changing
                    if (st.Current.HasValue && st.Current != hash)
                    {
                        _hash2Id.Remove(st.Current.Value);
                    }

                    // Update state
                    st.Previous = st.Current;
                    st.Current = hash;

                    // Add new hash mapping if present
                    if (hash.HasValue)
                    {
                        _hash2Id[hash.Value] = id;
                    }

                    _states[id] = st;
                }
                _staging.Clear();
                Prune();
            }
        }

        public void Discard()
        {
            lock (_sync)
            {
                _staging.Clear();
                Prune();
            }
        }

        public bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot)
        {
            lock (_sync)
            {
                // Check if this hash exists in the _hash2Id map
                if (!_hash2Id.TryGetValue(hash, out var id))
                {
                    snapshot = default;
                    return false;
                }

                // Get the token state
                var st = _states[id];

                // Check if this token is staged for deletion
                if (_staging.TryGetValue(id, out var stagedHash) && !stagedHash.HasValue)
                {
                    snapshot = default;
                    return false; // Return false for tokens staged for deletion
                }

                // Check if the token is deleted (not staged)
                if (!st.Current.HasValue)
                {
                    snapshot = default;
                    return false; // Return false for deleted tokens
                }

                // Get values from the pool
                _pool.TryGetValue(st.Initial.GetValueOrDefault(), out var iv);
                _pool.TryGetValue(st.Previous.GetValueOrDefault(), out var pv);
                _pool.TryGetValue(st.Current.Value, out var cv);

                snapshot = new TokenSnapshot<T>(st.Initial, st.Previous, st.Current, iv, pv, cv);
                return true;
            }
        }

        public IEnumerable<TokenDiff<T>> GetCommittedDiff()
        {
            lock (_sync) { return BuildDiff(st => new Tuple<long?, long?>(st.Previous, st.Current)).ToList(); }
        }

        public IEnumerable<TokenDiff<T>> GetUncommittedDiff()
        {
            lock (_sync)
            {
                var list = new List<TokenDiff<T>>();
                foreach (var kv in _states)
                {
                    var id = kv.Key; var st = kv.Value;
                    var staged = _staging.TryGetValue(id, out var u) ? u : st.Current;
                    if (staged == st.Current) continue;
                    _pool.TryGetValue(st.Current.GetValueOrDefault(), out var curVal);
                    _pool.TryGetValue(staged.GetValueOrDefault(), out var unVal);
                    list.Add(new TokenDiff<T>(st.Current, staged, curVal, unVal));
                }
                return list;
            }
        }

        public IEnumerable<TokenDiff<T>> GetFullDiff()
        {
            lock (_sync)
            {
                return BuildDiff(st =>
                {
                    var left = st.Previous ?? st.Initial;
                    var right = st.Current;
                    return new Tuple<long?, long?>(left, right);
                }).ToList();
            }
        }

        public IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot()
        {
            lock (_sync)
            {
                var results = new List<TokenSnapshot<T>>();

                foreach (var state in _states)
                {
                    var id = state.Key;
                    var st = state.Value;

                    // Get staged value if it exists
                    var currentHash = st.Current;
                    T currentValue = default; // Initialize to default

                    if (_staging.TryGetValue(id, out var stagedHash))
                    {
                        currentHash = stagedHash;
                        // Only try to get value if hash is not null (not deleted)
                        if (stagedHash.HasValue)
                        {
                            _pool.TryGetValue(stagedHash.Value, out currentValue);
                        }
                    }
                    else if (currentHash.HasValue) // Only get value if hash is not null
                    {
                        _pool.TryGetValue(currentHash.Value, out currentValue);
                    }

                    // Get initial value - for inserted tokens, initial hash will be null
                    T initialValue = default;
                    if (st.Initial.HasValue)
                    {
                        _pool.TryGetValue(st.Initial.Value, out initialValue);
                    }

                    // For previous, use previous if not staged, otherwise use current
                    var previousHash = _staging.ContainsKey(id) ? st.Current : st.Previous;
                    T previousValue = default; // Initialize to default
                    if (previousHash.HasValue) // Add this check to prevent GetValueOrDefault returning 0 for null
                    {
                        _pool.TryGetValue(previousHash.Value, out previousValue);
                    }

                    results.Add(new TokenSnapshot<T>(
                        st.Initial,
                        previousHash,
                        currentHash,
                        initialValue,
                        previousValue,
                        currentValue
                    ));
                }

                return results;
            }
        }

        private IEnumerable<TokenDiff<T>> BuildDiff(Func<TokenState, Tuple<long?, long?>> proj)
        {
            foreach (var st in _states.Values)
            {
                var pair = proj(st);
                var left = pair.Item1;
                var right = pair.Item2;

                // Skip if both sides are equal or both null
                if (left == right || (!left.HasValue && !right.HasValue)) continue;

                // Get values from pool (with null safety)
                T lv = default;
                T rv = default;

                if (left.HasValue)
                    _pool.TryGetValue(left.Value, out lv);

                if (right.HasValue)
                    _pool.TryGetValue(right.Value, out rv);

                yield return new TokenDiff<T>(left, right, lv, rv);
            }
        }

        // --------------------------------------------------------- helpers
        private TokenOpResult StageInsert(long hash, T value)
        {
            if (_hash2Id.ContainsKey(hash)) return TokenOpResult.DuplicateHash;
            var id = Guid.NewGuid();
            _hash2Id[hash] = id;
            _pool[hash] = value;
            _states[id] = new TokenState();
            _staging[id] = hash;
            return TokenOpResult.Success;
        }

        private TokenOpResult StageDelete(long hash)
        {
            if (!_hash2Id.TryGetValue(hash, out var id)) return TokenOpResult.UnknownHash;
            if (_staging.ContainsKey(id)) return TokenOpResult.AlreadyStaged;
            _staging[id] = null;
            return TokenOpResult.Success;
        }

        private TokenOpResult StageModify(long oldHash, long newHash, T value)
        {
            if (!_hash2Id.TryGetValue(oldHash, out var id)) return TokenOpResult.UnknownHash;
            if (_hash2Id.TryGetValue(newHash, out var other) && other != id) return TokenOpResult.Collision;
            if (_staging.ContainsKey(id)) return TokenOpResult.AlreadyStaged;
            if (_pool.TryGetValue(newHash, out var existing) && !EqualityComparer<T>.Default.Equals(existing, value)) return TokenOpResult.Collision;
            _hash2Id[newHash] = id;
            _pool[newHash] = value;
            _staging[id] = newHash;
            return TokenOpResult.Success;
        }

        private void Prune()
        {
            // Collect all live hashes
            var live = new HashSet<long>();

            // Add all hashes from states (initial, previous, current)
            foreach (var st in _states.Values)
            {
                if (st.Initial.HasValue) live.Add(st.Initial.Value);
                if (st.Previous.HasValue) live.Add(st.Previous.Value);
                if (st.Current.HasValue) live.Add(st.Current.Value);
            }

            // Add all non-null hashes from staging
            foreach (var h in _staging.Values)
            {
                if (h.HasValue) live.Add(h.Value);
            }

            // Identify and remove dead hashes
            var dead = _pool.Keys.Where(h => !live.Contains(h)).ToList();
            foreach (var h in dead)
            {
                _pool.Remove(h);
            }
        }
    }
}
