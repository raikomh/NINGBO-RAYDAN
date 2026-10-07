namespace BusinessSearcher.Domain.Exceptions
{
    // Se distingue de DomainException (400) porque mapea a 409 Conflict:
    // el estado actual del recurso impide la operación (ej. tope de tenants
    // ya alcanzado), no un dato de entrada inválido.
    public class ConflictException : Exception
    {
        public ConflictException() : base() { }
        public ConflictException(string message) : base(message) { }
        public ConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}
