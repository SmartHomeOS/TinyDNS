namespace TinyDNS
{
    public class DomainEqualityComparer : EqualityComparer<String>
    {
        public override bool Equals(string? d1, string? d2)
        {
           return string.Equals(d1, d2, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode(string box) => box.GetHashCode();
    }
}
