using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects
{
    public sealed class Money : ValueObject
    {
        public decimal Amount   { get; }
        public string  Currency { get; }

        private Money() { Currency = string.Empty; }

        public Money(decimal amount, string currency = "COP")
        {
            if (amount < 0)
                throw new DomainException("El monto no puede ser negativo.");
            if (string.IsNullOrWhiteSpace(currency))
                throw new DomainException("La moneda es requerida.");
            if (currency.Length != 3)
                throw new DomainException("La moneda debe ser un código ISO de 3 letras (ej: COP, USD).");

            Amount   = amount;
            Currency = currency.ToUpperInvariant();
        }

        public Money Add(Money other)
        {
            if (Currency != other.Currency)
                throw new DomainException($"No se pueden sumar monedas distintas: {Currency} y {other.Currency}.");
            return new Money(Amount + other.Amount, Currency);
        }

        public Money Subtract(Money other)
        {
            if (Currency != other.Currency)
                throw new DomainException($"No se pueden restar monedas distintas: {Currency} y {other.Currency}.");
            if (Amount < other.Amount)
                throw new DomainException("El resultado no puede ser negativo.");
            return new Money(Amount - other.Amount, Currency);
        }

        public Money Multiply(decimal factor)
        {
            if (factor < 0)
                throw new DomainException("El factor de multiplicación no puede ser negativo.");
            return new Money(Amount * factor, Currency);
        }

        public static Money operator +(Money a, Money b) => a.Add(b);
        public static Money operator -(Money a, Money b) => a.Subtract(b);
        public static Money operator *(Money a, decimal factor) => a.Multiply(factor);

        public static Money Zero(string currency = "COP") => new(0, currency);

        public bool IsGreaterThan(Money other) => Amount > other.Amount;
        public bool IsZero() => Amount == 0;

        public override string ToString() => $"{Amount:N2} {Currency}";

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }
}
