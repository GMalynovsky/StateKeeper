namespace TokenKeeper.Abstraction
{
    public interface ITokenMutator<T>
    {
        TokenOpResult Stage(long? oldHash, long? newHash, T value);
        void Commit();
        void Discard();
    }
}
