using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.StoreManagement;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.StoreManagement.Commands.Schedule
{
    // ── Add / Update Schedule ─────────────────────────────────────────────────────
    public record AddScheduleCommand(Guid StoreId, AddScheduleDto Dto) : IRequest<Guid>;

    public class AddScheduleCommandValidator : AbstractValidator<AddScheduleCommand>
    {
        private static readonly string[] ValidDays =
            { "Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday" };

        public AddScheduleCommandValidator()
        {
            RuleFor(x => x.StoreId).NotEmpty();
            RuleFor(x => x.Dto.DayOfWeek)
                .NotEmpty()
                .Must(d => ValidDays.Contains(d, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Día inválido. Use: Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday.");
            RuleFor(x => x.Dto.OpenTime)
                .NotEmpty().Matches(@"^\d{2}:\d{2}$").WithMessage("Formato de hora inválido. Use HH:mm.");
            RuleFor(x => x.Dto.CloseTime)
                .NotEmpty().Matches(@"^\d{2}:\d{2}$").WithMessage("Formato de hora inválido. Use HH:mm.");
        }
    }

    public class AddScheduleCommandHandler : IRequestHandler<AddScheduleCommand, Guid>
    {
        private readonly IStoreRepository    _storeRepo;
        private readonly IStoreUnitOfWork    _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public AddScheduleCommandHandler(IStoreRepository storeRepo,
            IStoreUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _storeRepo   = storeRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(AddScheduleCommand request, CancellationToken cancellationToken)
        {
            var store = await _storeRepo.GetByIdWithDetailsAsync(request.StoreId, cancellationToken)
                ?? throw new DomainException("Tienda no encontrada.");

            if (store.TenantId != _currentUser.TenantId)
                throw new DomainException("No tienes permisos sobre esta tienda.");

            var dto      = request.Dto;
            var day      = Enum.Parse<DayOfWeek>(dto.DayOfWeek, ignoreCase: true);
            var time     = ScheduleTime.Create(dto.OpenTime, dto.CloseTime);
            var schedule = store.AddOrUpdateSchedule(day, time, dto.IsClosed);

            await _storeRepo.UpdateAsync(store, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return schedule.Id;
        }
    }
}
