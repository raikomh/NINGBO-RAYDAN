using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Common;
using BusinessSearcher.Application.DTOs.StoreManagement;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.StoreManagement.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // GET STORE BY ID
    // ═══════════════════════════════════════════════════════════════
    public record GetStoreByIdQuery(Guid StoreId) : IRequest<StoreDto>;

    public class GetStoreByIdQueryHandler : IRequestHandler<GetStoreByIdQuery, StoreDto>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly ICurrentUserService _currentUser;

        public GetStoreByIdQueryHandler(IStoreRepository storeRepo, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _currentUser = currentUser;
        }

        public async Task<StoreDto> Handle(GetStoreByIdQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByIdWithDetailsAsync(request.StoreId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            if (store.TenantId != _currentUser.TenantId)
                throw new DomainException("No tienes permisos para ver esta tienda.");

            return new StoreDto(
                store.Id,
                store.TenantId,
                store.Name,
                store.Description,
                new AddressDto(
                    store.Address.Street,
                    store.Address.City,
                    store.Address.State,
                    store.Address.Country,
                    store.Address.Latitude,
                    store.Address.Longitude),
                store.Phone.Value,
                store.LogoUrl,
                store.IsActive,
                store.IsOpenNow(),
                store.Schedules.Select(s => new ScheduleDto(
                    s.Id,
                    s.DayOfWeek.ToString(),
                    s.Time.OpenTime.ToString("HH:mm"),
                    s.Time.CloseTime.ToString("HH:mm"),
                    s.IsClosed)));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GET MY STORE (1 tienda por tenant)
    // ═══════════════════════════════════════════════════════════════
    public record GetMyStoreQuery : IRequest<StoreDto?>;

    public class GetMyStoreQueryHandler : IRequestHandler<GetMyStoreQuery, StoreDto?>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly ICurrentUserService _currentUser;

        public GetMyStoreQueryHandler(IStoreRepository storeRepo, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _currentUser = currentUser;
        }

        public async Task<StoreDto?> Handle(GetMyStoreQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByTenantIdAsync(_currentUser.TenantId, cancellationToken);
            if (store is null) return null;

            return new StoreDto(
                store.Id, store.TenantId, store.Name, store.Description,
                new AddressDto(store.Address.Street, store.Address.City,
                    store.Address.State, store.Address.Country,
                    store.Address.Latitude, store.Address.Longitude),
                store.Phone.Value, store.LogoUrl, store.IsActive, store.IsOpenNow(),
                store.Schedules.Select(s => new ScheduleDto(s.Id, s.DayOfWeek.ToString(),
                    s.Time.OpenTime.ToString("HH:mm"), s.Time.CloseTime.ToString("HH:mm"), s.IsClosed)));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GET SCHEDULES BY STORE
    // ═══════════════════════════════════════════════════════════════
    public record GetSchedulesQuery(Guid StoreId) : IRequest<IEnumerable<ScheduleDto>>;

    public class GetSchedulesQueryHandler : IRequestHandler<GetSchedulesQuery, IEnumerable<ScheduleDto>>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly ICurrentUserService _currentUser;

        public GetSchedulesQueryHandler(IStoreRepository storeRepo, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<ScheduleDto>> Handle(GetSchedulesQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByIdWithDetailsAsync(request.StoreId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            if (store.TenantId != _currentUser.TenantId)
                throw new DomainException("No tienes permisos sobre esta tienda.");

            return store.Schedules
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new ScheduleDto(
                    s.Id,
                    s.DayOfWeek.ToString(),
                    s.Time.OpenTime.ToString("HH:mm"),
                    s.Time.CloseTime.ToString("HH:mm"),
                    s.IsClosed));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SEARCH PRODUCTS (público, cross-tenant)
    // ═══════════════════════════════════════════════════════════════
    public record SearchProductsQuery(
        string?  Query         = null,
        string?  City          = null,
        Guid?    CategoryId    = null,
        bool?    OnlyAvailable = true,
        decimal? MinPrice      = null,
        decimal? MaxPrice      = null,
        Guid?    StoreId       = null,
        int      Page          = 1,
        int      PageSize      = 20) : IRequest<PagedResult<SearchProductDto>>;

    public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, PagedResult<SearchProductDto>>
    {
        private readonly ISearchRepository _searchRepo;
        private readonly Domain.BoundedContext.Radar.Repositories.IClientProductRepository _clientProductRepo;

        public SearchProductsQueryHandler(
            ISearchRepository searchRepo,
            Domain.BoundedContext.Radar.Repositories.IClientProductRepository clientProductRepo)
        {
            _searchRepo        = searchRepo;
            _clientProductRepo = clientProductRepo;
        }

        public async Task<PagedResult<SearchProductDto>> Handle(
            SearchProductsQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var page     = Math.Max(request.Page, 1);

            var results = await _searchRepo.SearchProductsAsync(
                request.Query, request.City, request.CategoryId,
                request.OnlyAvailable, request.MinPrice, request.MaxPrice,
                request.StoreId, page, pageSize, cancellationToken);

            var total = await _searchRepo.CountSearchResultsAsync(
                request.Query, request.City, request.CategoryId,
                request.OnlyAvailable, request.MinPrice, request.MaxPrice,
                request.StoreId, cancellationToken);

            var dtos = results.Select(r => new SearchProductDto(
                r.ProductId, r.ProductName, r.ProductDescription,
                r.Price, r.Currency, r.ImageUrl, r.IsAvailable, r.Stock,
                r.CategoryName, r.StoreId, r.StoreName, r.StoreAddress,
                r.City, r.Latitude, r.Longitude, r.StorePhone,
                r.IsStoreOpen, r.StoreLogoUrl, r.MinOrderQuantity,
                SellerType: "Store")).ToList();

            // Mezcla productos de vendedores particulares (clientes). No aplica si se
            // filtra por una tienda concreta o por categoría (los clientes no tienen).
            if (request.StoreId is null && request.CategoryId is null)
            {
                var clientResults = await _clientProductRepo.SearchAsync(
                    request.Query, request.City, request.OnlyAvailable,
                    request.MinPrice, request.MaxPrice, page, pageSize, cancellationToken);

                dtos.AddRange(clientResults.Select(c => new SearchProductDto(
                    c.ProductId, c.Name, c.Description, c.Price, c.Currency, c.ImageUrl,
                    c.IsAvailable, c.IsAvailable ? 1 : 0, CategoryName: null,
                    StoreId: c.SellerId, StoreName: c.SellerName,
                    StoreAddress: FormatSellerAddress(c.SellerStreet, c.City, c.SellerState),
                    City: c.City ?? string.Empty, Latitude: c.Latitude, Longitude: c.Longitude,
                    StorePhone: c.SellerPhoneNumber, IsStoreOpen: true, StoreLogoUrl: null,
                    MinOrderQuantity: 1, SellerType: "Client")));

                total += clientResults.Count;
            }

            return new PagedResult<SearchProductDto>(dtos, total, page, pageSize);
        }

        /// <summary>Arma una dirección de texto legible a partir de Street/City/State del cliente-vendedor, omitiendo los nulos.</summary>
        private static string FormatSellerAddress(string? street, string? city, string? state)
            => string.Join(", ", new[] { street, city, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // ═══════════════════════════════════════════════════════════════
    // GET WHOLESALE CATALOG (autenticado, solo tenants Minorista)
    // ═══════════════════════════════════════════════════════════════
    public record GetWholesaleCatalogQuery(
        string? Query    = null,
        string? City     = null,
        int     Page     = 1,
        int     PageSize = 20) : IRequest<PagedResult<WholesaleCatalogItemDto>>;

    public class GetWholesaleCatalogQueryHandler
        : IRequestHandler<GetWholesaleCatalogQuery, PagedResult<WholesaleCatalogItemDto>>
    {
        private readonly ISearchRepository   _searchRepo;
        private readonly ICurrentUserService _currentUser;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public GetWholesaleCatalogQueryHandler(
            ISearchRepository searchRepo, ICurrentUserService currentUser,
            Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _searchRepo  = searchRepo;
            _currentUser = currentUser;
            _config      = config;
        }

        // El admin (Chat:AdminEmail) puede ver el directorio de mayoristas sin importar
        // su TenantType, para poder ver/probar ambos lados de la plataforma.
        private bool IsAdmin()
        {
            var adminEmail = _config["Chat:AdminEmail"] ?? "admin@businesssearcher.dev";
            return _currentUser.Email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<PagedResult<WholesaleCatalogItemDto>> Handle(
            GetWholesaleCatalogQuery request, CancellationToken cancellationToken)
        {
            if (_currentUser.TenantType != TenantType.Retail && !IsAdmin())
                throw new DomainException("Solo los tenants Minoristas pueden ver el directorio de mayoristas.");

            var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var page     = Math.Max(request.Page, 1);

            var results = await _searchRepo.GetWholesaleCatalogAsync(
                request.Query, request.City, page, pageSize, cancellationToken);

            var total = await _searchRepo.CountWholesaleCatalogAsync(
                request.Query, request.City, cancellationToken);

            var dtos = results.Select(r => new WholesaleCatalogItemDto(
                r.StoreId, r.StoreName, r.StoreAddress, r.City, r.StorePhone,
                r.ProductId, r.ProductName, r.ProductDescription,
                r.Price, r.Currency, r.Stock, r.MinOrderQuantity, r.CategoryName));

            return new PagedResult<WholesaleCatalogItemDto>(dtos, total, page, pageSize);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GET TENANT QUERIES
    // ═══════════════════════════════════════════════════════════════
    public record GetTenantProfileQuery : IRequest<DTOs.TenantManagement.TenantDto>;

    public class GetTenantProfileQueryHandler
        : IRequestHandler<GetTenantProfileQuery, DTOs.TenantManagement.TenantDto>
    {
        private readonly BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository _tenantRepo;
        private readonly ICurrentUserService _currentUser;

        public GetTenantProfileQueryHandler(
            BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository tenantRepo,
            ICurrentUserService currentUser)
        {
            _tenantRepo  = tenantRepo;
            _currentUser = currentUser;
        }

        public async Task<DTOs.TenantManagement.TenantDto> Handle(
            GetTenantProfileQuery request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            return new DTOs.TenantManagement.TenantDto(
                tenant.Id, tenant.BusinessName, tenant.Email.Value,
                tenant.Status.ToString(),
                tenant.IsSubscriptionActive(),
                tenant.CreatedAt, tenant.NextPaymentDate, tenant.GetTotalPaid(),
                tenant.Plan, tenant.Type.ToString(),
                tenant.PhoneNumber, tenant.LastPaymentDate);
        }
    }
}
