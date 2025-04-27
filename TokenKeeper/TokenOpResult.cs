namespace TokenKeeper
{
    public enum TokenOpResult
    {
        Success,
        DuplicateHash,
        UnknownHash,
        Collision,
        AlreadyStaged,
        InvalidInput
    }
}
