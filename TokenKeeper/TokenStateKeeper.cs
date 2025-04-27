using System.Collections.Generic;
using System.Linq;
using TokenKeeper.Abstraction;

namespace TokenKeeper
{
    public readonly struct TokenHashSnapshot<TValue>
    {
        public TokenHashSnapshot(string initialHash, string previousHash, string currentHash, TValue initialValue, TValue previousValue, TValue currentValue)
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
        public TValue InitialValue { get; }
        public TValue PreviousValue { get; }
        public TValue CurrentValue { get; }
    }

    public readonly struct TokenHashDiff<TValue>
    {
        public TokenHashDiff(string leftHash, string rightHash, TValue leftValue, TValue rightValue)
        {
            LeftHash = leftHash;
            RightHash = rightHash;
            LeftValue = leftValue;
            RightValue = rightValue;
        }
        public string LeftHash { get; }
        public string RightHash { get; }
        public TValue LeftValue { get; }
        public TValue RightValue { get; }
    }

    public interface ITokenStateKeeper<TValue>
    {
        TokenOpResult Seed(string hash, TValue value);
        TokenOpResult Stage(string oldHash, string newHash, TValue value);
        void Commit();
        void Discard();
    }

    public interface ITokenStateReader<TValue>
    {
        bool TryGetSnapshot(string hash, out TokenHashSnapshot<TValue> snapshot);
        IEnumerable<TokenHashDiff<TValue>> GetCommittedDiff();
        IEnumerable<TokenHashDiff<TValue>> GetUncommittedDiff();
        IEnumerable<TokenHashDiff<TValue>> GetFullDiff();
        IEnumerable<TokenHashSnapshot<TValue>> GetFullCurrentSnapshot();
    }

    public class TokenStateKeeper<TValue> : ITokenStateKeeper<TValue>, ITokenStateReader<TValue> where TValue : class
    {
        private readonly ITokenInitializer<TValue> _initializer;
        private readonly ITokenMutator<TValue> _mutator;
        private readonly ITokenReader<TValue> _reader;

        public TokenStateKeeper(IStateKeeperFactory stateKeeperFactory)
        {
            var (initializer, mutator, reader) = stateKeeperFactory.CreateThreadSafe<TValue>();
            _initializer = initializer;
            _mutator = mutator;
            _reader = reader;
        }

        #region ITokenStateKeeper Implementation

        public TokenOpResult Seed(string hash, TValue value)
        {
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash))
                return _initializer.Seed(longHash, value);
            return TokenOpResult.InvalidInput;
        }

        public TokenOpResult Stage(string oldHash, string newHash, TValue value)
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

        public bool TryGetSnapshot(string hash, out TokenHashSnapshot<TValue> snapshot)
        {
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash) &&
                _reader.TryGetSnapshot(longHash, out var internalSnapshot))
            {
                snapshot = new TokenHashSnapshot<TValue>(
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

        public IEnumerable<TokenHashDiff<TValue>> GetCommittedDiff()
        {
            return _reader.GetCommittedDiff().Select(d => new TokenHashDiff<TValue>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<TValue>> GetUncommittedDiff()
        {
            return _reader.GetUncommittedDiff().Select(d => new TokenHashDiff<TValue>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<TValue>> GetFullDiff()
        {
            return _reader.GetFullDiff().Select(d => new TokenHashDiff<TValue>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashSnapshot<TValue>> GetFullCurrentSnapshot()
        {
            return _reader.GetFullCurrentSnapshot().Select(s => new TokenHashSnapshot<TValue>(
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

    public class TokenStateKeeper : TokenStateKeeper<string>, ITokenStateKeeper<string>, ITokenStateReader<string>
    {
        public TokenStateKeeper(IStateKeeperFactory stateKeeperFactory) : base(stateKeeperFactory)
        {
        }
    }
}