using System;
using System.Collections.Generic;
using System.Linq;
using TokenKeeper.TokenKeeper.Core;

namespace TokenKeeper.Core
{
    /// <summary>
    /// Base implementation of the state keeper, containing core business logic without reader functionality.
    /// This class is not thread-safe and should be extended with concurrency control.
    /// </summary>
    public abstract class BaseStateKeeper<T> : ITokenInitializer<T>, ITokenMutator<T>
    {
        protected readonly ITokenStateStore<T> Store;

        protected BaseStateKeeper(ITokenStateStore<T> store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public virtual TokenOpResult Seed(long hash, T value)
        {
            // Check for duplicate hash
            if (Store.TryGetTokenId(hash, out _))
                return TokenOpResult.DuplicateHash;

            // Create new token
            var id = Guid.NewGuid();
            Store.MapHashToId(hash, id);
            Store.StoreValue(hash, value);
            Store.StoreInitialValue(id, value);

            // Initialize token state
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
            // Clear previous committed changes
            Store.ClearCommittedChanges();

            // Process all staged changes
            var stagedChanges = Store.GetAllStagedChanges();

            foreach (var kv in stagedChanges)
            {
                var id = kv.Key;
                var newHash = kv.Value;

                // Get current state
                if (!Store.TryGetTokenState(id, out var state))
                    continue; // Skip if state not found

                // Record the change for committed diff
                Store.RecordCommittedChange(id, state.Current, newHash);

                // Remove previous hash mapping if it exists and is changing
                if (state.Current.HasValue && state.Current != newHash)
                {
                    Store.RemoveHashMapping(state.Current.Value);
                }

                // Update state
                state.Previous = state.Current;
                state.Current = newHash;
                Store.SetTokenState(id, state);

                // Add new hash mapping if present
                if (newHash.HasValue)
                {
                    Store.MapHashToId(newHash.Value, id);
                }
            }

            // Clear staging
            Store.ClearStagedChanges();

            // Clean up unused values
            Prune();
        }

        public virtual void Discard()
        {
            Store.ClearStagedChanges();
            Prune();
        }

        protected virtual TokenOpResult StageInsert(long hash, T value)
        {
            // Check for duplicate hash
            if (Store.TryGetTokenId(hash, out _))
                return TokenOpResult.DuplicateHash;

            // Create new token
            var id = Guid.NewGuid();
            Store.MapHashToId(hash, id);
            Store.StoreValue(hash, value);
            Store.StoreInitialValue(id, value);

            // Initialize token state (Initial is null for inserted tokens)
            var state = new TokenState { Initial = null, Previous = null, Current = null };
            Store.SetTokenState(id, state);

            // Stage the new hash
            Store.StageChange(id, hash);

            return TokenOpResult.Success;
        }

        protected virtual TokenOpResult StageDelete(long hash)
        {
            // Check if hash exists
            if (!Store.TryGetTokenId(hash, out var id))
                return TokenOpResult.UnknownHash;

            // Check if already staged
            if (Store.HasStagedChanges(id))
                return TokenOpResult.AlreadyStaged;

            // Stage deletion (null hash)
            Store.StageChange(id, null);

            return TokenOpResult.Success;
        }

        protected virtual TokenOpResult StageModify(long oldHash, long newHash, T value)
        {
            // Check if old hash exists
            if (!Store.TryGetTokenId(oldHash, out var id))
                return TokenOpResult.UnknownHash;

            // Check for collision with different token
            if (Store.TryGetTokenId(newHash, out var otherId) && otherId != id)
                return TokenOpResult.Collision;

            // Check if already staged
            if (Store.HasStagedChanges(id))
                return TokenOpResult.AlreadyStaged;

            // Check for value collision
            if (Store.TryGetValue(newHash, out var existing) && !EqualityComparer<T>.Default.Equals(existing, value))
                return TokenOpResult.Collision;

            // Map new hash and store value
            Store.MapHashToId(newHash, id);
            Store.StoreValue(newHash, value);
            Store.StageChange(id, newHash);

            return TokenOpResult.Success;
        }

        protected virtual void Prune()
        {
            // Collect all live hashes
            var liveHashes = new HashSet<long>();

            // Add hashes from states (initial, previous, current)
            foreach (var kv in Store.GetAllTokenStates())
            {
                var state = kv.Value;
                if (state.Initial.HasValue) liveHashes.Add(state.Initial.Value);
                if (state.Previous.HasValue) liveHashes.Add(state.Previous.Value);
                if (state.Current.HasValue) liveHashes.Add(state.Current.Value);
            }

            // Add all non-null hashes from staging
            foreach (var hash in Store.GetAllStagedChanges().Values)
            {
                if (hash.HasValue) liveHashes.Add(hash.Value);
            }

            // Prune dead hashes
            Store.Prune(liveHashes);
        }
    }
}