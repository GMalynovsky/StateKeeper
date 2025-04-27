using System.Collections.Generic;
using System.Linq;
using TokenKeeper.Abstraction;
using TokenKeeper.Core;

namespace TokenKeeper
{
    public readonly struct TokenHashSnapshot<T>
    {
        public TokenHashSnapshot(string initialHash, string previousHash, string currentHash, T initialValue, T previousValue, T currentValue)
        {
            InitialHash = initialHash;
            PreviousHash = previousHash;
            CurrentHash = currentHash;
            InitialValue = initialValue;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
        public string InitialHash { get; }
        public string PreviousHash { get; }
        public string CurrentHash { get; }
        public T InitialValue { get; }
        public T PreviousValue { get; }
        public T CurrentValue { get; }
    }

    public readonly struct TokenHashDiff<T>
    {
        public TokenHashDiff(string leftHash, string rightHash, T leftValue, T rightValue)
        {
            LeftHash = leftHash;
            RightHash = rightHash;
            LeftValue = leftValue;
            RightValue = rightValue;
        }
        public string LeftHash { get; }
        public string RightHash { get; }
        public T LeftValue { get; }
        public T RightValue { get; }
    }

    public interface ITokenStateKeeper
    {
        TokenOpResult Seed(string hash, string value);
        TokenOpResult Stage(string oldHash, string newHash, string value);
        void Commit();
        void Discard();
    }

    public interface ITokenStateReader
    {
        bool TryGetSnapshot(string hash, out TokenHashSnapshot<string> snapshot);
        IEnumerable<TokenHashDiff<string>> GetCommittedDiff();
        IEnumerable<TokenHashDiff<string>> GetUncommittedDiff();
        IEnumerable<TokenHashDiff<string>> GetFullDiff();
        IEnumerable<TokenHashSnapshot<string>> GetFullCurrentSnapshot();
    }

    public class TokenStateKeeper : ITokenStateKeeper, ITokenStateReader
    {
        private readonly ITokenInitializer<string> _initializer;
        private readonly ITokenMutator<string> _mutator;
        private readonly ITokenReader<string> _reader;

        public TokenStateKeeper()
        {
            var (initializer, mutator, reader) = StateKeeperFactory.CreateThreadSafe<string>();
            _initializer = initializer;
            _mutator = mutator;
            _reader = reader;
        }

        #region ITokenStateKeeper Implementation

        public TokenOpResult Seed(string hash, string value)
        {
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash))
                return _initializer.Seed(longHash, value);
            return TokenOpResult.InvalidInput;
        }

        public TokenOpResult Stage(string oldHash, string newHash, string value)
        {
            var oldLongHash = !string.IsNullOrEmpty(oldHash) && long.TryParse(oldHash, out var oldLong)
                ? oldLong : (long?) null;
            var newLongHash = !string.IsNullOrEmpty(newHash) && long.TryParse(newHash, out var newLong)
                ? newLong : (long?) null;

            return _mutator.Stage(oldLongHash, newLongHash, value);
        }

        public void Commit() => _mutator.Commit();

        public void Discard() => _mutator.Discard();

        #endregion

        #region ITokenStateReader Implementation

        public bool TryGetSnapshot(string hash, out TokenHashSnapshot<string> snapshot)
        {
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash) &&
                _reader.TryGetSnapshot(longHash, out var internalSnapshot))
            {
                snapshot = new TokenHashSnapshot<string>(
                    internalSnapshot.InitialHash?.ToString(),
                    internalSnapshot.PreviousHash?.ToString(),
                    internalSnapshot.CurrentHash?.ToString(),
                    internalSnapshot.InitialValue,
                    internalSnapshot.PreviousValue,
                    internalSnapshot.CurrentValue);

                return true;
            }

            snapshot = default;
            return false;
        }

        public IEnumerable<TokenHashDiff<string>> GetCommittedDiff()
        {
            return _reader.GetCommittedDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<string>> GetUncommittedDiff()
        {
            return _reader.GetUncommittedDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<string>> GetFullDiff()
        {
            return _reader.GetFullDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashSnapshot<string>> GetFullCurrentSnapshot()
        {
            return _reader.GetFullCurrentSnapshot().Select(s => new TokenHashSnapshot<string>(
                s.InitialHash?.ToString(),
                s.PreviousHash?.ToString(),
                s.CurrentHash?.ToString(),
                s.InitialValue,
                s.PreviousValue,
                s.CurrentValue
            ));
        }

        #endregion
    }
}
