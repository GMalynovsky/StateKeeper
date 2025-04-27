namespace TokenKeeper.Abstraction
{
    public interface IStateKeeperFactory
    {
        abstract (ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>) CreateThreadSafe<T>();
    }
}