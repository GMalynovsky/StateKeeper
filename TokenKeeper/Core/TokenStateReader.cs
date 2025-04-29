using System;
using System.Collections.Generic;
using TokenRepository.Abstraction;

namespace TokenRepository.Core
{
    public class TokenStateReader<T> : ITokenReader<T>
    {
        protected readonly ITokenStateStore<T> Store;

        public TokenStateReader(ITokenStateStore<T> store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot)
        {
            if (!Store.TryGetTokenId(hash, out var id))
            {
                snapshot = default;
                return false;
            }

            if (!Store.TryGetTokenState(id, out var st))
            {
                snapshot = default;
                return false;
            }

            if (Store.TryGetStagedHash(id, out var stagedHash) && !stagedHash.HasValue)
            {
                snapshot = default;
                return false;
            }

            if (!st.Current.HasValue)
            {
                snapshot = default;
                return false;
            }

            Store.TryGetInitialValue(id, out var initialValue);

            T previousValue = default, currentValue = default;

            if (st.Previous.HasValue)
                Store.TryGetValue(st.Previous.Value, out previousValue);

            if (st.Current.HasValue)
                Store.TryGetValue(st.Current.Value, out currentValue);

            snapshot = new TokenSnapshot<T>(id,
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

                if (leftHash == rightHash || (!leftHash.HasValue && !rightHash.HasValue))
                    continue;

                T leftValue = default, rightValue = default;

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

                var staged = Store.TryGetStagedHash(id, out var hash) ? hash : st.Current;

                if (staged == st.Current)
                    continue;

                T currentValue = default, stagedValue = default;

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

                if (st.Initial.HasValue)
                {
                    if (st.Initial == st.Current || (!st.Initial.HasValue && !st.Current.HasValue))
                        continue;

                    Store.TryGetInitialValue(id, out var initialValue);

                    T currentValue = default;
                    if (st.Current.HasValue)
                        Store.TryGetValue(st.Current.Value, out currentValue);

                    result.Add(new TokenDiff<T>(st.Initial, st.Current, initialValue, currentValue));
                }
                else if (st.Current.HasValue)
                {
                    Store.TryGetValue(st.Current.Value, out var currentValue);

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

                var currentHash = st.Current;
                T currentValue = default;

                if (Store.TryGetStagedHash(id, out var stagedHash))
                {
                    currentHash = stagedHash;

                    if (stagedHash.HasValue)
                        Store.TryGetValue(stagedHash.Value, out currentValue);
                }
                else if (currentHash.HasValue)
                {
                    Store.TryGetValue(currentHash.Value, out currentValue);
                }

                T initialValue = default;
                if (st.Initial.HasValue)
                    Store.TryGetInitialValue(id, out initialValue);

                var previousHash = Store.HasStagedChanges(id) ? st.Current : st.Previous;
                T previousValue = default;

                if (previousHash.HasValue)
                    Store.TryGetValue(previousHash.Value, out previousValue);

                results.Add(new TokenSnapshot<T>(id,
                    st.Initial, previousHash, currentHash,
                    initialValue, previousValue, currentValue));
            }

            return results;
        }
    }
}