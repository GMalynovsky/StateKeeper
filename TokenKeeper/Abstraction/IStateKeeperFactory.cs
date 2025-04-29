namespace TokenRepository.Abstraction
{
    public interface IStateKeeperFactory
    {
        abstract (ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>) CreateThreadSafe<T>();
    }
}