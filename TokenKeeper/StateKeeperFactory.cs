using System;
using TokenKeeper.TokenKeeper.Core;

namespace TokenKeeper.Core
{
    /// <summary>
    /// Factory for creating state keeper implementations with appropriate configuration.
    /// </summary>
    public static class StateKeeperFactory
    {
        /// <summary>
        /// Creates a thread-safe state keeper and reader combination.
        /// </summary>
        public static (ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>) CreateThreadSafe<T>()
        {
            // Create the store first
            var store = new DictionaryTokenStateStore<T>();

            // Create the thread-safe state keeper
            var stateKeeper = new ThreadSafeStateKeeper<T>(store);

            // Create the thread-safe reader using the same lock as the state keeper
            var reader = new ThreadSafeTokenStateReader<T>(store, stateKeeper.SyncRoot);

            // Return all components
            return (stateKeeper, stateKeeper, reader);
        }

        /// <summary>
        /// Creates a non-thread-safe state keeper and reader for testing.
        /// </summary>
        public static (ITokenInitializer<T>, ITokenMutator<T>, ITokenReader<T>) CreateForTesting<T>()
        {
            var store = new DictionaryTokenStateStore<T>();
            var stateKeeper = new TestStateKeeper<T>(store);
            var reader = new TokenStateReader<T>(store);

            return (stateKeeper, stateKeeper, reader);
        }

        // Simple non-thread-safe implementation for testing
        private class TestStateKeeper<T> : BaseStateKeeper<T>
        {
            public TestStateKeeper(ITokenStateStore<T> store) : base(store) { }
        }
    }
}