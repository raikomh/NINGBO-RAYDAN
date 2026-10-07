using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects
{
    public sealed class Email : ValueObject
    {
        // Regex estándar RFC 5322 simplificado
        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Value { get; }

        private Email(string value) => Value = value;

        public static Email Create(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("El email no puede estar vacío.");

            // CORRECCIÓN: validación con Regex en lugar de solo Contains('@') y Contains('.')
            if (!EmailRegex.IsMatch(email))
                throw new DomainException($"El email '{email}' no tiene un formato válido.");

            return new Email(email.Trim().ToLowerInvariant());
        }

        public static implicit operator string(Email email) => email.Value;
        public static explicit operator Email(string value) => Create(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
