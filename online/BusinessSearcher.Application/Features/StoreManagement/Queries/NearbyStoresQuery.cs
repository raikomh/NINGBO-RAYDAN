using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.StoreManagement.Queries.NearbyStores
{
    public record NearbyStoreDto(
        Guid    StoreId,
        string  StoreName,
        string  Address,
        string  City,
        double? Latitude,
        double? Longitude,
        double? DistanceKm,
        int     AvailableProducts,
        string? Phone,
        bool    IsOpen);

    /// <summary>Tiendas con productos disponibles cercanas a una ubicación (radar de tiendas).</summary>
    public record GetNearbyStoresQuery(double Latitude, double Longitude, double RadiusKm = 5)
        : IRequest<IReadOnlyList<NearbyStoreDto>>;

    public class GetNearbyStoresQueryHandler : IRequestHandler<GetNearbyStoresQuery, IReadOnlyList<NearbyStoreDto>>
    {
        private readonly ISearchRepository _search;
        public GetNearbyStoresQueryHandler(ISearchRepository search) => _search = search;

        public async Task<IReadOnlyList<NearbyStoreDto>> Handle(GetNearbyStoresQuery request, CancellationToken ct)
        {
            var radius = request.RadiusKm <= 0 ? 5 : request.RadiusKm;

            // Productos disponibles cross-tenant (hasta 500) agrupados por tienda.
            var products = await _search.SearchProductsAsync(
                query: null, city: null, categoryId: null, onlyAvailable: true,
                minPrice: null, maxPrice: null, storeId: null, page: 1, pageSize: 500, cancellationToken: ct);

            return products
                .GroupBy(p => p.StoreId)
                .Select(g =>
                {
                    var s = g.First();
                    double? dist = (s.Latitude.HasValue && s.Longitude.HasValue)
                        ? GeoMath.HaversineKm(request.Latitude, request.Longitude, s.Latitude.Value, s.Longitude.Value)
                        : null;
                    return new NearbyStoreDto(
                        s.StoreId, s.StoreName, s.StoreAddress, s.City,
                        s.Latitude, s.Longitude,
                        dist.HasValue ? Math.Round(dist.Value, 2) : null,
                        g.Count(), s.StorePhone, s.IsStoreOpen);
                })
                .Where(x => x.DistanceKm is null || x.DistanceKm <= radius)
                .OrderBy(x => x.DistanceKm ?? double.MaxValue)
                .ToList();
        }
    }

    /// <summary>Distancia Haversine (km) entre dos coordenadas.</summary>
    internal static class GeoMath
    {
        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
