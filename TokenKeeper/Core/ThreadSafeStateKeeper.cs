using TokenRepository.Abstraction;

namespace TokenRepository.Core
{
    public class ThreadSafeStateKeeper<T> : BaseStateKeeper<T>
    {
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
            return base.StageInsert(hash, value);
        }

        protected override TokenOpResult StageDelete(long hash)
        {
            return base.StageDelete(hash);
        }

        protected override TokenOpResult StageModify(long oldHash, long newHash, T value)
        {
            return base.StageModify(oldHash, newHash, value);
        }

        protected override void Prune()
        {
            base.Prune();
        }
    }
}