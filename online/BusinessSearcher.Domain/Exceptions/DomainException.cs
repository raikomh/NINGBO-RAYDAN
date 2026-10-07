namespace BusinessSearcher.Domain.Exceptions
{
    // CORRECCIÓN: "DomainExceptions" → "DomainException" (singular, convención .NET)
    public class DomainException : Exception
    {
        public DomainException() : base() { }
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}
