using BusinessSearcher.Application.Commons.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Commons.Behaviors
{
    // ── ValidationBehavior ────────────────────────────────────────────────────────
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
            => _validators = validators;

        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context  = new ValidationContext<TRequest>(request);
            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Any())
                throw new ValidationException(failures);

            return await next();
        }
    }

    // ── LoggingBehavior ───────────────────────────────────────────────────────────
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
        private readonly IDateTimeService _dateTime;

        public LoggingBehavior(
            ILogger<LoggingBehavior<TRequest, TResponse>> logger,
            IDateTimeService dateTime)
        {
            _logger   = logger;
            _dateTime = dateTime;
        }

        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var start       = _dateTime.UtcNow;

            _logger.LogInformation("[START] {RequestName} | {DateTime}", requestName, start);

            try
            {
                var response = await next();
                var elapsed  = (_dateTime.UtcNow - start).TotalMilliseconds;
                _logger.LogInformation("[END] {RequestName} | {Elapsed}ms", requestName, elapsed);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERROR] {RequestName} | {Message}", requestName, ex.Message);
                throw;
            }
        }
    }

    // ── TransactionBehavior ───────────────────────────────────────────────────────
    // CORRECCIÓN: Eliminado "using System.Windows.Input" (era de WPF, no compila en Web API)
    // CORRECCIÓN: Ahora usa IUnitOfWork para gestionar la transacción correctamente
    public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ITransactionalCommand
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

        public TransactionBehavior(
            IUnitOfWork unitOfWork,
            ILogger<TransactionBehavior<TRequest, TResponse>> logger)
        {
            _unitOfWork = unitOfWork;
            _logger     = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                var response = await next();
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return response;
            }
            catch
            {
                _logger.LogWarning("Transacción revertida para {Request}", typeof(TRequest).Name);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }

    // Marker interface para Commands que necesitan transacción
    public interface ITransactionalCommand { }
}
