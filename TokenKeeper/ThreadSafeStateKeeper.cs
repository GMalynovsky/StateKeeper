using System;
using TokenKeeper.TokenKeeper.Core;

namespace TokenKeeper.Core
{
    /// <summary>
    /// Thread-safe implementation of the state keeper.
    /// Uses a lock for synchronization.
    /// </summary>
    public class ThreadSafeStateKeeper<T> : BaseStateKeeper<T>
    {
        // Using public field to allow sharing the same lock with reader
        public readonly object SyncRoot = new object();

        public ThreadSafeStateKeeper(ITokenStateStore<T> store) : base(store) { }

        public override TokenOpResult Seed(long hash, T value)
        {
            lock (SyncRoot)
            {
                return base.Seed(hash, value);
            }
        }

        public override TokenOpResult Stage(long? oldHash, long? newHash, T value)
        {
            lock (SyncRoot)
            {
                return base.Stage(oldHash, newHash, value);
            }
        }

        public override void Commit()
        {
            lock (SyncRoot)
            {
                base.Commit();
            }
        }

        public override void Discard()
        {
            lock (SyncRoot)
            {
                base.Discard();
            }
        }

        protected override TokenOpResult StageInsert(long hash, T value)
        {
            // No need to lock here - called from already locked method
            return base.StageInsert(hash, value);
        }

        protected override TokenOpResult StageDelete(long hash)
        {
            // No need to lock here - called from already locked method
            return base.StageDelete(hash);
        }

        protected override TokenOpResult StageModify(long oldHash, long newHash, T value)
        {
            // No need to lock here - called from already locked method
            return base.StageModify(oldHash, newHash, value);
        }

        protected override void Prune()
        {
            // No need to lock here - called from already locked method
            base.Prune();
        }
    }
}