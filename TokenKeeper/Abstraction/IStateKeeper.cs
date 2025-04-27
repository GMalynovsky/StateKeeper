using System.Collections.Generic;

namespace TokenKeeper.Abstraction
{
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
}
