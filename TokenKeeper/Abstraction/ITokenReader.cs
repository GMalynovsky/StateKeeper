using System.Collections.Generic;

namespace TokenRepository.Abstraction
{
    public interface ITokenReader<T>
    {
        bool TryGetSnapshot(long hash, out TokenSnapshot<T> snapshot);
        IEnumerable<TokenDiff<T>> GetCommittedDiff();
        IEnumerable<TokenDiff<T>> GetUncommittedDiff();
        IEnumerable<TokenDiff<T>> GetFullDiff();
        IEnumerable<TokenSnapshot<T>> GetFullCurrentSnapshot();
    }
}
