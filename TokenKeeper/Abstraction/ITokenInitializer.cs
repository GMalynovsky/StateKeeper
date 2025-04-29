namespace TokenRepository.Abstraction
{
    public interface ITokenInitializer<T>
    {
        TokenOpResult Seed(long hash, T value);
    }
}
