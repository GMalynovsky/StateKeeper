using System.Collections.Generic;
using System.Linq;

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

    public sealed class TokenStateKeeper :
        ITokenStateKeeper, ITokenStateReader
    {
        private readonly IStateKeeper<string> _core;

        public TokenStateKeeper(IStateKeeper<string> stateKeeper)
        {
            // Initialize the core with a string comparer
            _core = stateKeeper;
        }

        public TokenOpResult Seed(string hash, string value)
        {
            // Parse hash but don't validate if it's positive - existing tests might use negative values
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash))
                return _core.Seed(longHash, value);
            return TokenOpResult.InvalidInput;
        }

        public TokenOpResult Stage(string oldHash, string newHash, string value)
        {
            // Parse hashes but allow any valid long value including negative
            var oldLongHash = !string.IsNullOrEmpty(oldHash) && long.TryParse(oldHash, out var oldLong)
                ? oldLong : (long?) null;
            var newLongHash = !string.IsNullOrEmpty(newHash) && long.TryParse(newHash, out var newLong)
                ? newLong : (long?) null;

            return _core.Stage(oldLongHash, newLongHash, value);
        }

        public void Commit() => _core.Commit();
        public void Discard() => _core.Discard();

        public bool TryGetSnapshot(string hash, out TokenHashSnapshot<string> snapshot)
        {
            if (!string.IsNullOrEmpty(hash) && long.TryParse(hash, out var longHash) &&
                _core.TryGetSnapshot(longHash, out var internalSnapshot))
            {
                snapshot = new TokenHashSnapshot<string>(internalSnapshot.InitialHash?.ToString(),
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
            return _core.GetCommittedDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<string>> GetUncommittedDiff()
        {
            return _core.GetUncommittedDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashDiff<string>> GetFullDiff()
        {
            return _core.GetFullDiff().Select(d => new TokenHashDiff<string>(
                d.LeftHash?.ToString(),
                d.RightHash?.ToString(),
                d.LeftValue,
                d.RightValue
            ));
        }

        public IEnumerable<TokenHashSnapshot<string>> GetFullCurrentSnapshot()
        {
            return _core.GetFullCurrentSnapshot().Select(s => new TokenHashSnapshot<string>(
                s.InitialHash?.ToString(),
                s.PreviousHash?.ToString(),
                s.CurrentHash?.ToString(),
                s.InitialValue,
                s.PreviousValue,
                s.CurrentValue
            ));
        }
    }
}
