namespace TokenRepository
{
    public readonly struct TokenDiff<T>
    {
        public TokenDiff(long? leftHash, long? rightHash, T leftValue, T rightValue)
        {
            LeftHash = leftHash;
            RightHash = rightHash;
            LeftValue = leftValue;
            RightValue = rightValue;
        }
        public long? LeftHash { get; }
        public long? RightHash { get; }
        public T LeftValue { get; }
        public T RightValue { get; }
    }
}
