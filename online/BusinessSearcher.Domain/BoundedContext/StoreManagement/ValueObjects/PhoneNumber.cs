using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects
{
    public sealed class PhoneNumber : ValueObject
    {
        private static readonly Regex PhoneRegex =
            new(@"^\+?[0-9\s\-\(\)]{7,20}$", RegexOptions.Compiled);

        public string Value { get; }

        private PhoneNumber() { Value = string.Empty; }

        public PhoneNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("El número de teléfono es requerido.");

            var cleaned = value.Trim();
            if (!PhoneRegex.IsMatch(cleaned))
                throw new DomainException($"El número de teléfono '{cleaned}' no tiene un formato válido.");

            Value = cleaned;
        }

        public static implicit operator string(PhoneNumber phone) => phone.Value;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }

    public sealed class ScheduleTime : ValueObject
    {
        public TimeOnly OpenTime  { get; }
        public TimeOnly CloseTime { get; }

        private ScheduleTime() { }

        public ScheduleTime(TimeOnly openTime, TimeOnly closeTime)
        {
            if (closeTime <= openTime)
                throw new DomainException("La hora de cierre debe ser mayor a la hora de apertura.");

            OpenTime  = openTime;
            CloseTime = closeTime;
        }

        /// <summary>Crea desde strings "HH:mm" (ej: "08:00", "22:30")</summary>
        public static ScheduleTime Create(string openTime, string closeTime)
        {
            if (!TimeOnly.TryParseExact(openTime,  "HH:mm", out var open))
                throw new DomainException($"Formato de hora de apertura inválido: '{openTime}'. Use HH:mm.");
            if (!TimeOnly.TryParseExact(closeTime, "HH:mm", out var close))
                throw new DomainException($"Formato de hora de cierre inválido: '{closeTime}'. Use HH:mm.");

            return new ScheduleTime(open, close);
        }

        public bool IsOpenAt(TimeOnly time) => time >= OpenTime && time <= CloseTime;

        public bool IsOpenNow() => IsOpenAt(TimeOnly.FromDateTime(DateTime.Now));

        public int DurationInMinutes => (int)(CloseTime.ToTimeSpan() - OpenTime.ToTimeSpan()).TotalMinutes;

        public override string ToString() => $"{OpenTime:HH\\:mm} - {CloseTime:HH\\:mm}";

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return OpenTime;
            yield return CloseTime;
        }
    }
}
