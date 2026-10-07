using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects
{
    /// <summary>
    /// Coordenada geográfica (latitud/longitud) usada por los reportes y las alertas
    /// del radar de disponibilidad. Incluye cálculo de distancia (Haversine).
    /// </summary>
    public sealed class GeoLocation : ValueObject
    {
        private const double EarthRadiusKm = 6371.0;

        public double Latitude  { get; }
        public double Longitude { get; }

        private GeoLocation() { }

        public GeoLocation(double latitude, double longitude)
        {
            if (latitude < -90 || latitude > 90)
                throw new DomainException("La latitud debe estar entre -90 y 90.");
            if (longitude < -180 || longitude > 180)
                throw new DomainException("La longitud debe estar entre -180 y 180.");

            Latitude  = latitude;
            Longitude = longitude;
        }

        /// <summary>Distancia en kilómetros hasta otra coordenada (fórmula de Haversine).</summary>
        public double DistanceKmTo(GeoLocation other)
        {
            if (other is null) throw new DomainException("La coordenada de destino es requerida.");

            var dLat = ToRadians(other.Latitude  - Latitude);
            var dLon = ToRadians(other.Longitude - Longitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        public bool IsWithinKm(GeoLocation other, double radiusKm) => DistanceKmTo(other) <= radiusKm;

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        public override string ToString() => $"({Latitude:0.#####}, {Longitude:0.#####})";

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Latitude;
            yield return Longitude;
        }
    }
}
