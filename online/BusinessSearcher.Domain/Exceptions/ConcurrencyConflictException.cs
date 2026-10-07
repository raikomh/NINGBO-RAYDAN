namespace BusinessSearcher.Domain.Exceptions
{
    // Se lanza desde la capa de Infraestructura cuando SaveChanges detecta que otra operación
    // concurrente ya modificó la misma fila (choque de token de concurrencia, p.ej. xmin de
    // Postgres en ProductStock). Para cuando el llamador la recibe, la entidad en memoria ya fue
    // recargada con el valor real de la base de datos: el llamador solo necesita volver a aplicar
    // su cambio sobre ese valor fresco y reintentar guardar. No depende de Entity Framework para
    // que la capa Application (que no referencia EF Core) pueda capturarla.
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException() : base() { }
        public ConcurrencyConflictException(string message) : base(message) { }
        public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}
