using System;
using System.Collections.Generic;
using TokenKeeper.TokenKeeper.Core;

namespace TokenKeeper.Core
{
    /// <summary>
    /// Provides read-only access to token state information.
    /// Not thread-safe on its own - relies on external synchronization.
    /// </summary>
    public class TokenStateReader<T> : ITokenReader<T>
    {
        protected readonly ITokenStateStore<T> Store;

        public TokenStateReader(ITokenStateStore<T> store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot)
        {
            // Check if hash exists in mapping
            if (!Store.TryGetTokenId(hash, out var id))
            {
                snapshot = default;
                return false;
            }

            // Get token state
            if (!Store.TryGetTokenState(id, out var st))
            {
                snapshot = default;
                return false;
            }

            // Check if token is staged for deletion
            if (Store.TryGetStagedHash(id, out var stagedHash) && !stagedHash.HasValue)
            {
                snapshot = default;
                return false;
            }

            // Check if token is deleted
            if (!st.Current.HasValue)
            {
                snapshot = default;
                return false;
            }

            // Get initial value
            T initialValue = default;
            Store.TryGetInitialValue(id, out initialValue);

            // Get previous and current values
            T previousValue = default, currentValue = default;

            if (st.Previous.HasValue)
                Store.TryGetValue(st.Previous.Value, out previousValue);

            if (st.Current.HasValue)
                Store.TryGetValue(st.Current.Value, out currentValue);

            snapshot = new TokenSnapshot<T>(
                st.Initial, st.Previous, st.Current,
                initialValue, previousValue, currentValue);

            return true;
        }

        public IEnumerable<TokenDiff<T>> GetCommittedDiff()
        {
            var result = new List<TokenDiff<T>>();

            foreach (var change in Store.GetCommittedChanges())
            {
                var id = change.Item1;
                var leftHash = change.Item2;
                var rightHash = change.Item3;

                // Skip if no change
                if (leftHash == rightHash || (!leftHash.HasValue && !rightHash.HasValue))
                    continue;

                T leftValue = default, rightValue = default;

                // Get left value
                if (leftHash.HasValue)
                {
                    if (Store.TryGetTokenState(id, out var st) &&
                        st.Initial.HasValue && leftHash == st.Initial &&
                        Store.TryGetInitialValue(id, out var initialValue))
                    {
                        leftValue = initialValue;
                    }
                    else
                    {
                        Store.TryGetValue(leftHash.Value, out leftValue);
                    }
                }

                // Get right value
                if (rightHash.HasValue)
                {
                    Store.TryGetValue(rightHash.Value, out rightValue);
                }

                result.Add(new TokenDiff<T>(leftHash, rightHash, leftValue, rightValue));
            }

            return result;
        }

        public IEnumerable<TokenDiff<T>> GetUncommittedDiff()
        {
            var result = new List<TokenDiff<T>>();

            foreach (var kv in Store.GetAllTokenStates())
            {
                var id = kv.Key;
                var st = kv.Value;

                // Get staged hash, or use current if not staged
                var staged = Store.TryGetStagedHash(id, out var hash) ? hash : st.Current;

                // Skip if no change
                if (staged == st.Current)
                    continue;

                T currentValue = default, stagedValue = default;

                // Get current value
                if (st.Current.HasValue)
                {
                    if (st.Initial.HasValue && st.Current == st.Initial &&
                        Store.TryGetInitialValue(id, out var initialValue))
                    {
                        currentValue = initialValue;
                    }
                    else
                    {
                        Store.TryGetValue(st.Current.Value, out currentValue);
                    }
                }

                // Get staged value
                if (staged.HasValue)
                {
                    Store.TryGetValue(staged.Value, out stagedValue);
                }

                result.Add(new TokenDiff<T>(st.Current, staged, currentValue, stagedValue));
            }

            return result;
        }

        public IEnumerable<TokenDiff<T>> GetFullDiff()
        {
            var result = new List<TokenDiff<T>>();

            foreach (var kv in Store.GetAllTokenStates())
            {
                var id = kv.Key;
                var st = kv.Value;

                // For seeded tokens, show initial->current
                if (st.Initial.HasValue)
                {
                    // Skip if no change or both null
                    if (st.Initial == st.Current || (!st.Initial.HasValue && !st.Current.HasValue))
                        continue;

                    // Get initial value
                    T initialValue = default;
                    Store.TryGetInitialValue(id, out initialValue);

                    // Get current value
                    T currentValue = default;
                    if (st.Current.HasValue)
                        Store.TryGetValue(st.Current.Value, out currentValue);

                    result.Add(new TokenDiff<T>(st.Initial, st.Current, initialValue, currentValue));
                }
                // For inserted tokens (no initial hash), show null->current
                else if (st.Current.HasValue)
                {
                    // Get current value
                    T currentValue = default;
                    Store.TryGetValue(st.Current.Value, out currentValue);

                    result.Add(new TokenDiff<T>(null, st.Current, default, currentValue));
                }
            }

            return result;
        }

        public IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot()
        {
            var results = new List<TokenSnapshot<T>>();

            foreach (var kv in Store.GetAllTokenStates())
            {
                var id = kv.Key;
                var st = kv.Value;

                // Get staged value if it exists
                var currentHash = st.Current;
                T currentValue = default;

                if (Store.TryGetStagedHash(id, out var stagedHash))
                {
                    currentHash = stagedHash;

                    // Only try to get value if hash is not null (not deleted)
                    if (stagedHash.HasValue)
                        Store.TryGetValue(stagedHash.Value, out currentValue);
                }
                else if (currentHash.HasValue)
                {
                    Store.TryGetValue(currentHash.Value, out currentValue);
                }

                // Get initial value
                T initialValue = default;
                if (st.Initial.HasValue)
                    Store.TryGetInitialValue(id, out initialValue);

                // For previous, use previous if not staged, otherwise use current
                var previousHash = Store.HasStagedChanges(id) ? st.Current : st.Previous;
                T previousValue = default;

                if (previousHash.HasValue)
                    Store.TryGetValue(previousHash.Value, out previousValue);

                results.Add(new TokenSnapshot<T>(
                    st.Initial, previousHash, currentHash,
                    initialValue, previousValue, currentValue));
            }

            return results;
        }
    }
}