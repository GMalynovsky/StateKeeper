using TokenKeeper.Abstraction;

namespace TokenKeeper.Core
{
    public static class StateKeeperFactory
    {
        public static (ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>) CreateThreadSafe<T>()
        {
            var store = new DictionaryTokenStateStore<T>();

            var stateKeeper = new ThreadSafeStateKeeper<T>(store);

            var reader = new ThreadSafeTokenStateReader<T>(store, stateKeeper.SyncRoot);

            return (stateKeeper, stateKeeper, reader);
        }
    }
}