using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Community
{
    internal static class CommunityMapper
    {
        public static CommunityQuestionDto ToDto(CommunityQuestion q) => new(
            q.Id, q.AskerName, q.Text, q.City, q.ProductName, q.CreatedAt,
            q.Answers.OrderBy(a => a.CreatedAt)
                .Select(a => new CommunityAnswerDto(a.Id, a.AnswererName, a.Text, a.CreatedAt)).ToList());
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.AskQuestion
{
    using BusinessSearcher.Application.Features.Radar.Community;

    public record AskQuestionCommand(string Text, string? City, string? ProductName) : IRequest<CommunityQuestionDto>;

    public class AskQuestionCommandValidator : AbstractValidator<AskQuestionCommand>
    {
        public AskQuestionCommandValidator()
        {
            RuleFor(x => x.Text).NotEmpty().WithMessage("Escribe tu pregunta.").MaximumLength(500);
        }
    }

    public class AskQuestionCommandHandler : IRequestHandler<AskQuestionCommand, CommunityQuestionDto>
    {
        private readonly ICommunityQuestionRepository _questions;
        private readonly IClientRepository            _clients;
        private readonly IRadarUnitOfWork             _uow;
        private readonly ICurrentUserService          _currentUser;

        public AskQuestionCommandHandler(
            ICommunityQuestionRepository questions, IClientRepository clients,
            IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _questions = questions; _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<CommunityQuestionDto> Handle(AskQuestionCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var question = CommunityQuestion.Create(client.Id, client.FullName, request.Text, request.City, request.ProductName);
            await _questions.AddAsync(question, ct);
            await _uow.SaveChangesAsync(ct);

            return CommunityMapper.ToDto(question);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.AnswerQuestion
{
    using BusinessSearcher.Application.Features.Radar.Community;

    public record AnswerQuestionCommand(Guid QuestionId, string Text) : IRequest<CommunityQuestionDto>;

    public class AnswerQuestionCommandValidator : AbstractValidator<AnswerQuestionCommand>
    {
        public AnswerQuestionCommandValidator()
        {
            RuleFor(x => x.Text).NotEmpty().WithMessage("Escribe tu respuesta.").MaximumLength(500);
        }
    }

    public class AnswerQuestionCommandHandler : IRequestHandler<AnswerQuestionCommand, CommunityQuestionDto>
    {
        private readonly ICommunityQuestionRepository _questions;
        private readonly IClientRepository            _clients;
        private readonly IRadarUnitOfWork             _uow;
        private readonly ICurrentUserService          _currentUser;

        public AnswerQuestionCommandHandler(
            ICommunityQuestionRepository questions, IClientRepository clients,
            IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _questions = questions; _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<CommunityQuestionDto> Handle(AnswerQuestionCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var question = await _questions.GetByIdAsync(request.QuestionId, ct)
                ?? throw new DomainException("Pregunta no encontrada.");

            question.AddAnswer(client.Id, client.FullName, request.Text);
            await _questions.UpdateAsync(question, ct);
            await _uow.SaveChangesAsync(ct);

            return CommunityMapper.ToDto(question);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.RecentQuestions
{
    using BusinessSearcher.Application.Features.Radar.Community;

    public record GetRecentQuestionsQuery(string? City, int Limit = 30) : IRequest<IReadOnlyList<CommunityQuestionDto>>;

    public class GetRecentQuestionsQueryHandler : IRequestHandler<GetRecentQuestionsQuery, IReadOnlyList<CommunityQuestionDto>>
    {
        private readonly ICommunityQuestionRepository _questions;
        public GetRecentQuestionsQueryHandler(ICommunityQuestionRepository questions) => _questions = questions;

        public async Task<IReadOnlyList<CommunityQuestionDto>> Handle(GetRecentQuestionsQuery request, CancellationToken ct)
        {
            var list = await _questions.GetRecentAsync(request.City, Math.Clamp(request.Limit, 1, 100), ct);
            return list.Select(CommunityMapper.ToDto).ToList();
        }
    }
}
