using TokenRepository.Abstraction;
using TokenRepository.Core;

namespace TokenRepository.Tests;

internal static class TokenStateKeeperProvider
{
    private static readonly IStateKeeperFactory Factory = new StateKeeperFactory();

    internal static TokenStateKeeper Create() => new(Factory);

    internal static TokenStateKeeper<T> Create<T>() where T : class => new(Factory);
}
