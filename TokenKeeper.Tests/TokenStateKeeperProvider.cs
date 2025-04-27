using TokenKeeper.Core;

namespace TokenKeeper.Tests;
internal static class TokenStateKeeperProvider
{
    internal static TokenStateKeeper Create() => new TokenStateKeeper(new StateKeeperFactory());
}
