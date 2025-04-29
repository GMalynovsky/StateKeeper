namespace TokenRepository.Tests
{
    public class DocumentItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime LastModified { get; set; }
        public int Version { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is not DocumentItem other)
                return false;

            return Id == other.Id &&
                   Title == other.Title &&
                   Content == other.Content &&
                   LastModified.Equals(other.LastModified) &&
                   Version == other.Version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Title, Content, LastModified, Version);
        }

        public override string ToString()
        {
            return $"Document {Id}: {Title} (v{Version})";
        }
    }
}