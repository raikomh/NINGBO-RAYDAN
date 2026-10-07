using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace BusinessSearcher.Application.Features.StoreManagement.Commands.Upload
{
    // ═══════════════════════════════════════════════════════════════
    // UPLOAD STORE LOGO
    // ═══════════════════════════════════════════════════════════════
    public record UploadStoreLogoCommand(Guid StoreId, IFormFile File) : IRequest<string>;

    public class UploadStoreLogoCommandValidator : AbstractValidator<UploadStoreLogoCommand>
    {
        private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2MB

        public UploadStoreLogoCommandValidator()
        {
            RuleFor(x => x.StoreId).NotEmpty();
            RuleFor(x => x.File)
                .NotNull().WithMessage("El archivo es requerido.")
                .Must(f => f.Length > 0).WithMessage("El archivo no puede estar vacío.")
                .Must(f => f.Length <= MaxFileSizeBytes).WithMessage("El logo no puede superar 2MB.")
                .Must(f => new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" }
                    .Contains(f.ContentType.ToLowerInvariant()))
                    .WithMessage("Solo se permiten imágenes JPEG, PNG o WEBP.");
        }
    }

    public class UploadStoreLogoCommandHandler : IRequestHandler<UploadStoreLogoCommand, string>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly IStoreUnitOfWork    _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IFileStorageService _fileStorage;

        public UploadStoreLogoCommandHandler(
            IStoreRepository storeRepo, IStoreUnitOfWork unitOfWork,
            ICurrentUserService currentUser, IFileStorageService fileStorage)
        {
            _storeRepo   = storeRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
            _fileStorage = fileStorage;
        }

        public async Task<string> Handle(
            UploadStoreLogoCommand request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByIdWithDetailsAsync(request.StoreId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            if (store.TenantId != _currentUser.TenantId)
                throw new DomainException("No tienes permisos sobre esta tienda.");

            if (!string.IsNullOrEmpty(store.LogoUrl))
            {
                try { await _fileStorage.DeleteAsync(store.LogoUrl, cancellationToken); }
                catch { }
            }

            await using var stream = request.File.OpenReadStream();
            var logoUrl = await _fileStorage.UploadAsync(
                stream,
                request.File.FileName,
                request.File.ContentType,
                $"logos/{store.TenantId:N}",
                cancellationToken);

            store.Update(store.Name, store.Address, store.Phone, store.Description, logoUrl);

            await _storeRepo.UpdateAsync(store, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return logoUrl;
        }
    }
}
