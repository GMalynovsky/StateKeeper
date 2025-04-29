using System;
using System.Collections.Generic;
using TokenRepository.Abstraction;

namespace TokenRepository.Core
{
    public abstract class BaseStateKeeper<T> : ITokenInitializer<T>, ITokenMutator<T>
    {
        protected readonly ITokenStateStore<T> Store;

        protected BaseStateKeeper(ITokenStateStore<T> store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public virtual TokenOpResult Seed(long hash, T value)
        {
            if (Store.TryGetTokenId(hash, out _))
                return TokenOpResult.DuplicateHash;

            var id = Guid.NewGuid();
            Store.MapHashToId(hash, id);
            Store.StoreValue(hash, value);
            Store.StoreInitialValue(id, value);

            var state = new TokenState { Initial = hash, Previous = hash, Current = hash };
            Store.SetTokenState(id, state);

            return TokenOpResult.Success;
        }

        public virtual TokenOpResult Stage(long? oldHash, long? newHash, T value)
        {
            if (oldHash.HasValue && !newHash.HasValue)
                return StageDelete(oldHash.Value);

            if (!oldHash.HasValue && newHash.HasValue)
                return StageInsert(newHash.Value, value);

            if (oldHash.HasValue && newHash.HasValue)
                return StageModify(oldHash.Value, newHash.Value, value);

            return TokenOpResult.InvalidInput;
        }

        public virtual void Commit()
        {
            Store.ClearCommittedChanges();

            var stagedChanges = Store.GetAllStagedChanges();

            foreach (var kv in stagedChanges)
            {
                var id = kv.Key;
                var newHash = kv.Value;

                if (!Store.TryGetTokenState(id, out var state))
                    continue; // Skip if state not found

                Store.RecordCommittedChange(id, state.Current, newHash);

                if (state.Current.HasValue && state.Current != newHash)
                {
                    Store.RemoveHashMapping(state.Current.Value);
                }

                state.Previous = state.Current;
                state.Current = newHash;
                Store.SetTokenState(id, state);

                if (newHash.HasValue)
                {
                    Store.MapHashToId(newHash.Value, id);
                }
            }

            Store.ClearStagedChanges();

            Prune();
        }

        public virtual void Discard()
        {
            Store.ClearStagedChanges();
            Prune();
        }

        protected virtual TokenOpResult StageInsert(long hash, T value)
        {
            if (Store.TryGetTokenId(hash, out _))
                return TokenOpResult.DuplicateHash;

            var id = Guid.NewGuid();
            Store.MapHashToId(hash, id);
            Store.StoreValue(hash, value);
            Store.StoreInitialValue(id, value);

            var state = new TokenState { Initial = null, Previous = null, Current = null };
            Store.SetTokenState(id, state);

            Store.StageChange(id, hash);

            return TokenOpResult.Success;
        }

        protected virtual TokenOpResult StageDelete(long hash)
        {
            if (!Store.TryGetTokenId(hash, out var id))
                return TokenOpResult.UnknownHash;

            if (Store.HasStagedChanges(id))
                return TokenOpResult.AlreadyStaged;

            Store.StageChange(id, null);

            return TokenOpResult.Success;
        }

        protected virtual TokenOpResult StageModify(long oldHash, long newHash, T value)
        {
            if (!Store.TryGetTokenId(oldHash, out var id))
                return TokenOpResult.UnknownHash;

            if (Store.TryGetTokenId(newHash, out var otherId) && otherId != id)
                return TokenOpResult.Collision;

            if (Store.HasStagedChanges(id))
                return TokenOpResult.AlreadyStaged;

            if (Store.TryGetValue(newHash, out var existing) && !EqualityComparer<T>.Default.Equals(existing, value))
                return TokenOpResult.Collision;

            Store.MapHashToId(newHash, id);
            Store.StoreValue(newHash, value);
            Store.StageChange(id, newHash);

            return TokenOpResult.Success;
        }

        protected virtual void Prune()
        {
            var liveHashes = new HashSet<long>();

            foreach (var kv in Store.GetAllTokenStates())
            {
                var state = kv.Value;
                if (state.Initial.HasValue) liveHashes.Add(state.Initial.Value);
                if (state.Previous.HasValue) liveHashes.Add(state.Previous.Value);
                if (state.Current.HasValue) liveHashes.Add(state.Current.Value);
            }

            foreach (var hash in Store.GetAllStagedChanges().Values)
            {
                if (hash.HasValue) liveHashes.Add(hash.Value);
            }

            Store.Prune(liveHashes);
        }
    }
}