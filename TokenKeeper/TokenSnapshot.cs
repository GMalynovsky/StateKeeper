namespace TokenKeeper
{
    public readonly struct TokenSnapshot<T>
    {
        public TokenSnapshot(long? initialHash, long? previousHash, long? currentHash, T initialValue, T previousValue, T currentValue)
        {
            InitialHash = initialHash;
            PreviousHash = previousHash;
            CurrentHash = currentHash;
            InitialValue = initialValue;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
        public long? InitialHash { get; }
        public long? PreviousHash { get; }
        public long? CurrentHash { get; }
        public T InitialValue { get; }
        public T PreviousValue { get; }
        public T CurrentValue { get; }
    }
}
