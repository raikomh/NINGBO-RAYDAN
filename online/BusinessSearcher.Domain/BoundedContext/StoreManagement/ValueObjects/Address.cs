using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects
{
    public sealed class Address : ValueObject
    {
        public string Street    { get; }
        public string City      { get; }
        public string State     { get; }
        public string Country   { get; }
        public double? Latitude  { get; }
        public double? Longitude { get; }

        private Address() { Street = City = State = Country = string.Empty; }

        public Address(string street, string city, string state, string country,
                       double? latitude = null, double? longitude = null)
        {
            if (string.IsNullOrWhiteSpace(street))  throw new DomainException("La calle es requerida.");
            if (string.IsNullOrWhiteSpace(city))    throw new DomainException("La ciudad es requerida.");
            if (string.IsNullOrWhiteSpace(state))   throw new DomainException("El estado/departamento es requerido.");
            if (string.IsNullOrWhiteSpace(country)) throw new DomainException("El país es requerido.");

            if (latitude.HasValue && (latitude < -90 || latitude > 90))
                throw new DomainException("La latitud debe estar entre -90 y 90.");
            if (longitude.HasValue && (longitude < -180 || longitude > 180))
                throw new DomainException("La longitud debe estar entre -180 y 180.");

            Street    = street.Trim();
            City      = city.Trim();
            State     = state.Trim();
            Country   = country.Trim();
            Latitude  = latitude;
            Longitude = longitude;
        }

        public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

        public override string ToString() => $"{Street}, {City}, {State}, {Country}";

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
            yield return State;
            yield return Country;
        }
    }
}
