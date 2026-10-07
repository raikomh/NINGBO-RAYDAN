using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.StoreManagement;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using StoreAggregate = BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates.Store;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.StoreManagement.Commands.Store
{
    // ═══════════════════════════════════════════════════════════════
    // SETUP STORE (upsert — crea la tienda si no existe, actualiza si ya existe)
    // Cada tenant tiene exactamente 1 tienda; no hay "create" separado del "update".
    // ═══════════════════════════════════════════════════════════════
    public record SetupStoreCommand(SetupStoreDto Dto) : IRequest<Guid>;

    public class SetupStoreCommandValidator : AbstractValidator<SetupStoreCommand>
    {
        public SetupStoreCommandValidator()
        {
            RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Dto.Phone).NotEmpty();
            RuleFor(x => x.Dto.Address).NotNull();
            RuleFor(x => x.Dto.Address.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Dto.Address.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Dto.Address.State).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Dto.Address.Country).NotEmpty().MaximumLength(100);
        }
    }

    public class SetupStoreCommandHandler : IRequestHandler<SetupStoreCommand, Guid>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly IStoreUnitOfWork    _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public SetupStoreCommandHandler(
            IStoreRepository    storeRepo,
            IStoreUnitOfWork    unitOfWork,
            ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(SetupStoreCommand request, CancellationToken cancellationToken)
        {
            var dto     = request.Dto;
            var address = new Address(
                dto.Address.Street, dto.Address.City,
                dto.Address.State,  dto.Address.Country,
                dto.Address.Latitude, dto.Address.Longitude);
            var phone = new PhoneNumber(dto.Phone);

            var existing = await _storeRepo.GetByTenantIdAsync(_currentUser.TenantId, cancellationToken);

            if (existing is null)
            {
                var store = StoreAggregate.Create(
                    _currentUser.TenantId, dto.Name, address, phone,
                    dto.Description, dto.LogoUrl);

                await _storeRepo.AddAsync(store, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return store.Id;
            }

            existing.Update(dto.Name, address, phone, dto.Description, dto.LogoUrl);
            await _storeRepo.UpdateAsync(existing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ACTIVATE STORE
    // ═══════════════════════════════════════════════════════════════
    public record ActivateStoreCommand : IRequest;

    public class ActivateStoreCommandHandler : IRequestHandler<ActivateStoreCommand>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly IStoreUnitOfWork    _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public ActivateStoreCommandHandler(
            IStoreRepository storeRepo, IStoreUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(ActivateStoreCommand request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByTenantIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            store.Activate();

            await _storeRepo.UpdateAsync(store, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DEACTIVATE STORE
    // ═══════════════════════════════════════════════════════════════
    public record DeactivateStoreCommand : IRequest;

    public class DeactivateStoreCommandHandler : IRequestHandler<DeactivateStoreCommand>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly IStoreUnitOfWork    _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public DeactivateStoreCommandHandler(
            IStoreRepository storeRepo, IStoreUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(DeactivateStoreCommand request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByTenantIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            store.Deactivate();

            await _storeRepo.UpdateAsync(store, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
