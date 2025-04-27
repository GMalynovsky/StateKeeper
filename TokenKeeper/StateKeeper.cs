using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenKeeper
{
    public struct TokenState
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
        IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot();
    }

    public interface IStateKeeper<T>
    {
        void Commit();
        void Discard();
        IEnumerable<TokenDiff<T>> GetCommittedDiff();
        IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot();
        IEnumerable<TokenDiff<T>> GetFullDiff();
        IEnumerable<TokenDiff<T>> GetUncommittedDiff();
        TokenOpResult Seed(long hash, T value);
        TokenOpResult Stage(long? oldHash, long? newHash, T value);
        bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot);
    }

    public abstract class StateKeeper<T> : IStateKeeper<T>
    {
        private readonly Dictionary<Guid, TokenState> _states = new Dictionary<Guid, TokenState>();
        private readonly Dictionary<Guid, long?> _staging = new Dictionary<Guid, long?>();
        private readonly Dictionary<long, Guid> _hash2Id = new Dictionary<long, Guid>();
        private readonly Dictionary<long, T> _pool = new Dictionary<long, T>();

        // The sanctuary for initial values - never modified after seeding
        private readonly Dictionary<Guid, T> _initialValueSanctuary = new Dictionary<Guid, T>();

        // Track the latest committed changes
        private readonly List<Tuple<Guid, long?, long?>> _lastCommittedChanges = new List<Tuple<Guid, long?, long?>>();

        private readonly object _sync = new object();

        public TokenOpResult Seed(long hash, T value)
        {
            lock (_sync)
            {
                if (_hash2Id.ContainsKey(hash)) return TokenOpResult.DuplicateHash;
                var id = Guid.NewGuid();
                _hash2Id[hash] = id;
                _pool[hash] = value;

                // Store the initial value in the sanctuary
                _initialValueSanctuary[id] = value;

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
                // Clear previous committed changes
                _lastCommittedChanges.Clear();

                foreach (var kv in _staging)
                {
                    var id = kv.Key;
                    var hash = kv.Value;
                    var st = _states[id];

                    // Track the change for committed diff
                    _lastCommittedChanges.Add(new Tuple<Guid, long?, long?>(id, st.Current, hash));

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

                // Get initial value from sanctuary
                _initialValueSanctuary.TryGetValue(id, out var initialValue);

                // Get current and previous values from pool
                _pool.TryGetValue(st.Previous.GetValueOrDefault(), out var previousValue);
                _pool.TryGetValue(st.Current.Value, out var currentValue);

                snapshot = new TokenSnapshot<T>(st.Initial, st.Previous, st.Current, initialValue, previousValue, currentValue);
                return true;
            }
        }

        public IEnumerable<TokenDiff<T>> GetCommittedDiff()
        {
            lock (_sync)
            {
                var result = new List<TokenDiff<T>>();

                // Only return the changes from the last commit
                foreach (var change in _lastCommittedChanges)
                {
                    var id = change.Item1;
                    var leftHash = change.Item2;
                    var rightHash = change.Item3;

                    // Skip if no change
                    if (leftHash == rightHash || (!leftHash.HasValue && !rightHash.HasValue)) continue;

                    T leftValue = default, rightValue = default;

                    // Get left value
                    if (leftHash.HasValue)
                    {
                        var st = _states[id];
                        if (st.Initial.HasValue && leftHash == st.Initial && _initialValueSanctuary.TryGetValue(id, out var initialValue))
                        {
                            // Use sanctuary for initial values
                            leftValue = initialValue;
                        }
                        else if (_pool.TryGetValue(leftHash.Value, out var poolValue))
                        {
                            leftValue = poolValue;
                        }
                    }

                    // Get right value
                    if (rightHash.HasValue)
                    {
                        _pool.TryGetValue(rightHash.Value, out rightValue);
                    }

                    result.Add(new TokenDiff<T>(leftHash, rightHash, leftValue, rightValue));
                }

                return result;
            }
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

                    T curVal = default, unVal = default;

                    // Get current value
                    if (st.Current.HasValue)
                    {
                        if (st.Initial.HasValue && st.Current == st.Initial && _initialValueSanctuary.TryGetValue(id, out var initialValue))
                        {
                            curVal = initialValue;
                        }
                        else if (_pool.TryGetValue(st.Current.Value, out var poolValue))
                        {
                            curVal = poolValue;
                        }
                    }

                    // Get staged value
                    if (staged.HasValue)
                    {
                        _pool.TryGetValue(staged.Value, out unVal);
                    }

                    list.Add(new TokenDiff<T>(st.Current, staged, curVal, unVal));
                }
                return list;
            }
        }

        public IEnumerable<TokenDiff<T>> GetFullDiff()
        {
            lock (_sync)
            {
                var result = new List<TokenDiff<T>>();

                foreach (var kv in _states)
                {
                    var id = kv.Key;
                    var st = kv.Value;

                    // For seeded tokens, show initial->current
                    if (st.Initial.HasValue)
                    {
                        // Skip if no change or both null
                        if (st.Initial == st.Current || (!st.Initial.HasValue && !st.Current.HasValue)) continue;

                        // Get initial value from sanctuary
                        _initialValueSanctuary.TryGetValue(id, out var initialValue);

                        // Get current value from pool
                        T currentValue = default;
                        if (st.Current.HasValue)
                        {
                            _pool.TryGetValue(st.Current.Value, out currentValue);
                        }

                        result.Add(new TokenDiff<T>(st.Initial, st.Current, initialValue, currentValue));
                    }
                    // For inserted tokens (no initial hash), show null->current
                    else if (st.Current.HasValue)
                    {
                        // Get current value
                        _pool.TryGetValue(st.Current.Value, out var currentValue);

                        result.Add(new TokenDiff<T>(null, st.Current, default, currentValue));
                    }
                }

                return result;
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
                    T currentValue = default;

                    if (_staging.TryGetValue(id, out var stagedHash))
                    {
                        currentHash = stagedHash;
                        // Only try to get value if hash is not null (not deleted)
                        if (stagedHash.HasValue)
                        {
                            _pool.TryGetValue(stagedHash.Value, out currentValue);
                        }
                    }
                    else if (currentHash.HasValue)
                    {
                        _pool.TryGetValue(currentHash.Value, out currentValue);
                    }

                    // Get initial value from the sanctuary
                    T initialValue = default;
                    if (st.Initial.HasValue && _initialValueSanctuary.TryGetValue(id, out var value))
                    {
                        initialValue = value;
                    }

                    // For previous, use previous if not staged, otherwise use current
                    var previousHash = _staging.ContainsKey(id) ? st.Current : st.Previous;
                    T previousValue = default;
                    if (previousHash.HasValue)
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

        // --------------------------------------------------------- helpers
        private TokenOpResult StageInsert(long hash, T value)
        {
            if (_hash2Id.ContainsKey(hash)) return TokenOpResult.DuplicateHash;
            var id = Guid.NewGuid();
            _hash2Id[hash] = id;
            _pool[hash] = value;

            // For inserted tokens, also store the value in sanctuary
            _initialValueSanctuary[id] = value;

            // For inserted tokens, keep Initial hash null
            _states[id] = new TokenState { Initial = null, Previous = null, Current = null };
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

            // Check for value collision
            if (_pool.TryGetValue(newHash, out var existing) && !EqualityComparer<T>.Default.Equals(existing, value))
                return TokenOpResult.Collision;

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

            // Identify and remove dead hashes from the pool
            var dead = _pool.Keys.Where(h => !live.Contains(h)).ToList();
            foreach (var h in dead)
            {
                _pool.Remove(h);
            }

            // Initial values in sanctuary are never pruned
        }
    }
}
