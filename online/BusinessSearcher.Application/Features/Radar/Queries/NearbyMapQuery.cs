using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Application.Features.StoreManagement.Queries.NearbyStores; // GeoMath
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries.NearbyMap
{
    /// <summary>
    /// Mapa combinado: tiendas mayoristas (verde) + minoristas (rojo) + clientes-vendedores
    /// con productos disponibles (azul). Los clientes sin productos disponibles no aparecen.
    /// </summary>
    public record GetNearbyMapQuery(double Latitude, double Longitude, double RadiusKm = 5)
        : IRequest<IReadOnlyList<NearbyMapEntryDto>>;

    public class GetNearbyMapQueryHandler : IRequestHandler<GetNearbyMapQuery, IReadOnlyList<NearbyMapEntryDto>>
    {
        private readonly ISearchRepository        _search;
        private readonly IClientProductRepository _clientProducts;

        public GetNearbyMapQueryHandler(ISearchRepository search, IClientProductRepository clientProducts)
        {
            _search         = search;
            _clientProducts = clientProducts;
        }

        public async Task<IReadOnlyList<NearbyMapEntryDto>> Handle(GetNearbyMapQuery request, CancellationToken ct)
        {
            var radius  = request.RadiusKm <= 0 ? 5 : request.RadiusKm;
            var entries = new List<NearbyMapEntryDto>();

            // ── Tiendas (mayoristas/minoristas) ──
            var products = await _search.SearchProductsAsync(
                query: null, city: null, categoryId: null, onlyAvailable: true,
                minPrice: null, maxPrice: null, storeId: null, page: 1, pageSize: 500, cancellationToken: ct);

            foreach (var g in products.GroupBy(p => p.StoreId))
            {
                var s = g.First();
                if (!s.Latitude.HasValue || !s.Longitude.HasValue) continue;

                var dist = GeoMath.HaversineKm(request.Latitude, request.Longitude, s.Latitude.Value, s.Longitude.Value);
                if (dist > radius) continue;

                entries.Add(new NearbyMapEntryDto(
                    Type: s.TenantType == TenantType.Wholesale ? "Wholesale"
                        : s.TenantType == TenantType.Retail    ? "Retail"
                        : "Retail", // por defecto minorista si el tipo no está disponible
                    Id: s.StoreId, Name: s.StoreName,
                    Latitude: s.Latitude.Value, Longitude: s.Longitude.Value,
                    DistanceKm: Math.Round(dist, 2), AvailableProducts: g.Count(),
                    Address: s.StoreAddress, Phone: s.StorePhone));
            }

            // ── Clientes-vendedores con productos disponibles ──
            var sellers = await _clientProducts.GetAvailableWithLocationAsync(ct);
            foreach (var seller in sellers)
            {
                var dist = GeoMath.HaversineKm(request.Latitude, request.Longitude, seller.Latitude, seller.Longitude);
                if (dist > radius) continue;

                entries.Add(new NearbyMapEntryDto(
                    Type: "Client", Id: seller.ClientId, Name: seller.FullName,
                    Latitude: seller.Latitude, Longitude: seller.Longitude,
                    DistanceKm: Math.Round(dist, 2), AvailableProducts: seller.AvailableProducts,
                    Address: seller.City, Phone: null));
            }

            return entries.OrderBy(e => e.DistanceKm ?? double.MaxValue).ToList();
        }
    }
}
