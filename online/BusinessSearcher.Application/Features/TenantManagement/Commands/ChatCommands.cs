using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // TENANT SENDS MESSAGE  (tenant → admin)
    // ═══════════════════════════════════════════════════════════════
    public record SendTenantMessageCommand(string Text) : IRequest<Guid>;

    public class SendTenantMessageCommandValidator : AbstractValidator<SendTenantMessageCommand>
    {
        public SendTenantMessageCommandValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty().WithMessage("El mensaje no puede estar vacío.")
                .MaximumLength(2000).WithMessage("El mensaje no puede exceder 2000 caracteres.");
        }
    }

    public class SendTenantMessageCommandHandler : IRequestHandler<SendTenantMessageCommand, Guid>
    {
        private readonly IChatRepository     _chatRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IChatNotifier       _notifier;

        public SendTenantMessageCommandHandler(
            IChatRepository     chatRepo,
            IUnitOfWork         unitOfWork,
            ICurrentUserService currentUser,
            IChatNotifier       notifier)
        {
            _chatRepo    = chatRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
            _notifier    = notifier;
        }

        public async Task<Guid> Handle(SendTenantMessageCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsAuthenticated)
                throw new DomainException("Debes iniciar sesión para enviar mensajes.");

            var message = ChatMessage.FromTenant(
                _currentUser.TenantId,
                _currentUser.BusinessName,
                request.Text);

            await _chatRepo.AddAsync(message, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyNewMessageAsync(
                _currentUser.TenantId,
                _currentUser.BusinessName,
                request.Text,
                isFromAdmin: false,
                message.CreatedAt,
                cancellationToken);

            return message.Id;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN REPLIES  (admin → tenant)
    // ═══════════════════════════════════════════════════════════════
    public record SendAdminReplyCommand(Guid TenantId, string Text) : IRequest<Guid>;

    public class SendAdminReplyCommandValidator : AbstractValidator<SendAdminReplyCommand>
    {
        public SendAdminReplyCommandValidator()
        {
            RuleFor(x => x.TenantId).NotEmpty();
            RuleFor(x => x.Text)
                .NotEmpty().WithMessage("La respuesta no puede estar vacía.")
                .MaximumLength(2000);
        }
    }

    public class SendAdminReplyCommandHandler : IRequestHandler<SendAdminReplyCommand, Guid>
    {
        private readonly IChatRepository     _chatRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IChatNotifier       _notifier;

        public SendAdminReplyCommandHandler(
            IChatRepository     chatRepo,
            IUnitOfWork         unitOfWork,
            ICurrentUserService currentUser,
            IChatNotifier       notifier)
        {
            _chatRepo    = chatRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
            _notifier    = notifier;
        }

        public async Task<Guid> Handle(SendAdminReplyCommand request, CancellationToken cancellationToken)
        {
            var message = ChatMessage.FromAdmin(
                request.TenantId,
                _currentUser.BusinessName,
                request.Text);

            await _chatRepo.AddAsync(message, cancellationToken);
            await _chatRepo.MarkTenantMessagesReadAsync(request.TenantId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyNewMessageAsync(
                request.TenantId,
                _currentUser.BusinessName,
                request.Text,
                isFromAdmin: true,
                message.CreatedAt,
                cancellationToken);

            return message.Id;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MARK ADMIN MESSAGES AS READ  (tenant marks what they've seen)
    // ═══════════════════════════════════════════════════════════════
    public record MarkAdminMessagesReadCommand : IRequest;

    public class MarkAdminMessagesReadCommandHandler : IRequestHandler<MarkAdminMessagesReadCommand>
    {
        private readonly IChatRepository     _chatRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public MarkAdminMessagesReadCommandHandler(
            IChatRepository chatRepo, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _chatRepo    = chatRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(MarkAdminMessagesReadCommand request, CancellationToken cancellationToken)
        {
            await _chatRepo.MarkAdminMessagesReadAsync(_currentUser.TenantId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
