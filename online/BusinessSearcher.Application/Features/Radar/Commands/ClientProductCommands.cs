using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.ClientProducts
{
    internal static class ClientProductMapper
    {
        public static ClientProductDto ToDto(ClientProduct p) => new(
            p.Id, p.Name, p.Description, p.Price, p.Currency, p.IsAvailable, p.ImageUrl, p.CreatedAt);
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.CreateClientProduct
{
    using BusinessSearcher.Application.Features.Radar.ClientProducts;

    public record CreateClientProductCommand(
        string  Name,
        decimal Price,
        string? Currency,
        string? Description,
        string? ImageUrl,
        double? Latitude,
        double? Longitude,
        string? City) : IRequest<ClientProductDto>;

    public class CreateClientProductCommandValidator : AbstractValidator<CreateClientProductCommand>
    {
        public CreateClientProductCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del producto es requerido.")
                .MinimumLength(2).MaximumLength(200);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        }
    }

    public class CreateClientProductCommandHandler : IRequestHandler<CreateClientProductCommand, ClientProductDto>
    {
        private readonly IClientProductRepository _products;
        private readonly IClientRepository        _clients;
        private readonly IRadarUnitOfWork         _uow;
        private readonly ICurrentUserService      _currentUser;

        public CreateClientProductCommandHandler(
            IClientProductRepository products, IClientRepository clients,
            IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _products = products; _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<ClientProductDto> Handle(CreateClientProductCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            // Al publicar, captura/actualiza la ubicación del vendedor (GPS de la app)
            // para que sus productos aparezcan en el mapa como pin de cliente.
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                client.SetLocation(request.Latitude.Value, request.Longitude.Value, request.City);
                await _clients.UpdateAsync(client, ct);
            }

            var product = ClientProduct.Create(
                client.Id, request.Name, request.Price,
                request.Currency ?? "CUP", request.Description, request.ImageUrl);

            await _products.AddAsync(product, ct);
            await _uow.SaveChangesAsync(ct);

            return ClientProductMapper.ToDto(product);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.UpdateClientProduct
{
    using BusinessSearcher.Application.Features.Radar.ClientProducts;

    public record UpdateClientProductCommand(
        Guid    ProductId,
        string  Name,
        decimal Price,
        string? Currency,
        string? Description,
        string? ImageUrl) : IRequest<ClientProductDto>;

    public class UpdateClientProductCommandValidator : AbstractValidator<UpdateClientProductCommand>
    {
        public UpdateClientProductCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateClientProductCommandHandler : IRequestHandler<UpdateClientProductCommand, ClientProductDto>
    {
        private readonly IClientProductRepository _products;
        private readonly IRadarUnitOfWork         _uow;
        private readonly ICurrentUserService      _currentUser;

        public UpdateClientProductCommandHandler(
            IClientProductRepository products, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _products = products; _uow = uow; _currentUser = currentUser;
        }

        public async Task<ClientProductDto> Handle(UpdateClientProductCommand request, CancellationToken ct)
        {
            var product = await _products.GetByIdAsync(request.ProductId, ct)
                ?? throw new DomainException("Producto no encontrado.");
            if (product.ClientId != _currentUser.AccountId)
                throw new DomainException("No puedes modificar un producto que no es tuyo.");

            product.Update(request.Name, request.Price, request.Currency ?? product.Currency,
                request.Description, request.ImageUrl);

            await _products.UpdateAsync(product, ct);
            await _uow.SaveChangesAsync(ct);

            return ClientProductMapper.ToDto(product);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.SetClientProductAvailability
{
    using BusinessSearcher.Application.Features.Radar.ClientProducts;

    public record SetClientProductAvailabilityCommand(Guid ProductId, bool IsAvailable) : IRequest<ClientProductDto>;

    public class SetClientProductAvailabilityCommandHandler
        : IRequestHandler<SetClientProductAvailabilityCommand, ClientProductDto>
    {
        private readonly IClientProductRepository _products;
        private readonly IRadarUnitOfWork         _uow;
        private readonly ICurrentUserService      _currentUser;

        public SetClientProductAvailabilityCommandHandler(
            IClientProductRepository products, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _products = products; _uow = uow; _currentUser = currentUser;
        }

        public async Task<ClientProductDto> Handle(SetClientProductAvailabilityCommand request, CancellationToken ct)
        {
            var product = await _products.GetByIdAsync(request.ProductId, ct)
                ?? throw new DomainException("Producto no encontrado.");
            if (product.ClientId != _currentUser.AccountId)
                throw new DomainException("No puedes modificar un producto que no es tuyo.");

            product.SetAvailability(request.IsAvailable);
            await _products.UpdateAsync(product, ct);
            await _uow.SaveChangesAsync(ct);

            return ClientProductMapper.ToDto(product);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.DeleteClientProduct
{
    public record DeleteClientProductCommand(Guid ProductId) : IRequest;

    public class DeleteClientProductCommandHandler : IRequestHandler<DeleteClientProductCommand>
    {
        private readonly IClientProductRepository _products;
        private readonly IRadarUnitOfWork         _uow;
        private readonly ICurrentUserService      _currentUser;

        public DeleteClientProductCommandHandler(
            IClientProductRepository products, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _products = products; _uow = uow; _currentUser = currentUser;
        }

        public async Task Handle(DeleteClientProductCommand request, CancellationToken ct)
        {
            var product = await _products.GetByIdAsync(request.ProductId, ct)
                ?? throw new DomainException("Producto no encontrado.");
            if (product.ClientId != _currentUser.AccountId)
                throw new DomainException("No puedes eliminar un producto que no es tuyo.");

            await _products.DeleteAsync(product, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.GetMyClientProducts
{
    using BusinessSearcher.Application.Features.Radar.ClientProducts;

    public record GetMyClientProductsQuery : IRequest<IReadOnlyList<ClientProductDto>>;

    public class GetMyClientProductsQueryHandler
        : IRequestHandler<GetMyClientProductsQuery, IReadOnlyList<ClientProductDto>>
    {
        private readonly IClientProductRepository _products;
        private readonly ICurrentUserService      _currentUser;

        public GetMyClientProductsQueryHandler(IClientProductRepository products, ICurrentUserService currentUser)
        {
            _products = products; _currentUser = currentUser;
        }

        public async Task<IReadOnlyList<ClientProductDto>> Handle(GetMyClientProductsQuery request, CancellationToken ct)
        {
            var products = await _products.GetByClientAsync(_currentUser.AccountId, ct);
            return products.Select(ClientProductMapper.ToDto).ToList();
        }
    }
}
