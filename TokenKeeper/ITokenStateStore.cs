using System;
using System.Collections.Generic;

namespace TokenKeeper
{
    namespace TokenKeeper.Core
    {
        /// <summary>
        /// Core storage interface for the token state system.
        /// Responsible for storing and retrieving token states, hash mappings, and values.
        /// </summary>
        public interface ITokenStateStore<T>
        {
            // Token state operations
            bool TryGetTokenState(Guid id, out TokenState state);
            void SetTokenState(Guid id, TokenState state);
            IEnumerable<KeyValuePair<Guid, TokenState>> GetAllTokenStates();

            // Hash mapping operations
            bool TryGetTokenId(long hash, out Guid id);
            void MapHashToId(long hash, Guid id);
            void RemoveHashMapping(long hash);

            // Value operations
            bool TryGetValue(long hash, out T value);
            void StoreValue(long hash, T value);

            // Initial value operations (sanctuary)
            bool TryGetInitialValue(Guid id, out T value);
            void StoreInitialValue(Guid id, T value);

            // Staging operations
            void StageChange(Guid id, long? newHash);
            void ClearStagedChanges();
            bool TryGetStagedHash(Guid id, out long? hash);
            bool HasStagedChanges(Guid id);
            IReadOnlyDictionary<Guid, long?> GetAllStagedChanges();

            // History operations
            void RecordCommittedChange(Guid id, long? oldHash, long? newHash);
            void ClearCommittedChanges();
            IEnumerable<Tuple<Guid, long?, long?>> GetCommittedChanges();

            // Prune operations - eliminating unused values
            void Prune(ISet<long> liveHashes);
        }
    }
}
