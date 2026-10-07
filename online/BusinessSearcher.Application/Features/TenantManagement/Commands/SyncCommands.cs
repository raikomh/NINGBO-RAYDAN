using BusinessSearcher.Application.Commons;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands
{
    // ── Command ───────────────────────────────────────────────────────────────────
    // Recibe un paquete de sincronización de una instalación local. No hay JWT de usuario (llamada
    // servidor a servidor): el tenant se resuelve por el hash de la API key recibida en el header
    // X-Sync-Api-Key (ver SyncController.Ingest, [AllowAnonymous]), nunca por un id confiado del cliente.
    public record IngestTenantSyncPushCommand(string? ApiKey, SyncPushEnvelopeDto Envelope)
        : IRequest<SyncIngestResultDto>;

    // ── Handler ───────────────────────────────────────────────────────────────────
    public class IngestTenantSyncPushCommandHandler
        : IRequestHandler<IngestTenantSyncPushCommand, SyncIngestResultDto>
    {
        private readonly ITenantRepository  _tenantRepo;
        private readonly ISyncIngestService _ingestService;
        private readonly ISyncLogRepository _syncLogRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<IngestTenantSyncPushCommandHandler> _logger;

        public IngestTenantSyncPushCommandHandler(
            ITenantRepository tenantRepo,
            ISyncIngestService ingestService,
            ISyncLogRepository syncLogRepo,
            IUnitOfWork unitOfWork,
            ILogger<IngestTenantSyncPushCommandHandler> logger)
        {
            _tenantRepo    = tenantRepo;
            _ingestService = ingestService;
            _syncLogRepo   = syncLogRepo;
            _unitOfWork    = unitOfWork;
            _logger        = logger;
        }

        public async Task<SyncIngestResultDto> Handle(
            IngestTenantSyncPushCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new UnauthorizedAccessException("Falta el header X-Sync-Api-Key.");

            var tenant = await _tenantRepo.GetBySyncApiKeyHashAsync(
                SyncApiKeyHasher.Hash(request.ApiKey), cancellationToken)
                ?? throw new UnauthorizedAccessException("API key de sincronización inválida.");

            var syncLog = SyncLog.StartPull(tenant.Id);
            await _syncLogRepo.AddAsync(syncLog, cancellationToken);

            try
            {
                var result = await _ingestService.IngestAsync(tenant.Id, request.Envelope, cancellationToken);

                syncLog.Complete(result.ItemsSent, result.ItemsAccepted);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Sync ingest OK para tenant online {TenantId}: {Sent} recibidos, {Accepted} aceptados.",
                    tenant.Id, result.ItemsSent, result.ItemsAccepted);

                return result;
            }
            catch (Exception ex)
            {
                syncLog.Fail(ex.Message);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Sync ingest falló para tenant online {TenantId}.", tenant.Id);
                throw;
            }
        }
    }

    // ── Command (Export) ─────────────────────────────────────────────────────────
    // Arma el snapshot completo del tenant online para que una instalación local lo descargue
    // (pull del Administrador). Misma autenticación por API key que el ingest.
    public record ExportTenantSyncDataCommand(string? ApiKey) : IRequest<SyncPushEnvelopeDto>;

    // ── Handler (Export) ─────────────────────────────────────────────────────────
    public class ExportTenantSyncDataCommandHandler
        : IRequestHandler<ExportTenantSyncDataCommand, SyncPushEnvelopeDto>
    {
        private readonly ITenantRepository  _tenantRepo;
        private readonly ISyncExportService _exportService;
        private readonly ISyncLogRepository _syncLogRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExportTenantSyncDataCommandHandler> _logger;

        public ExportTenantSyncDataCommandHandler(
            ITenantRepository tenantRepo,
            ISyncExportService exportService,
            ISyncLogRepository syncLogRepo,
            IUnitOfWork unitOfWork,
            ILogger<ExportTenantSyncDataCommandHandler> logger)
        {
            _tenantRepo    = tenantRepo;
            _exportService = exportService;
            _syncLogRepo   = syncLogRepo;
            _unitOfWork    = unitOfWork;
            _logger        = logger;
        }

        public async Task<SyncPushEnvelopeDto> Handle(
            ExportTenantSyncDataCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new UnauthorizedAccessException("Falta el header X-Sync-Api-Key.");

            var tenant = await _tenantRepo.GetBySyncApiKeyHashAsync(
                SyncApiKeyHasher.Hash(request.ApiKey), cancellationToken)
                ?? throw new UnauthorizedAccessException("API key de sincronización inválida.");

            var syncLog = SyncLog.StartPush(tenant.Id, null);
            await _syncLogRepo.AddAsync(syncLog, cancellationToken);

            try
            {
                var envelope = await _exportService.ExportAsync(tenant.Id, cancellationToken);

                syncLog.Complete(envelope.TotalItems, envelope.TotalItems);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Sync export OK para tenant online {TenantId}: {Total} items.",
                    tenant.Id, envelope.TotalItems);

                return envelope;
            }
            catch (Exception ex)
            {
                syncLog.Fail(ex.Message);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Sync export falló para tenant online {TenantId}.", tenant.Id);
                throw;
            }
        }
    }

    // ── Command: generar API key de sincronización ─────────────────────────────────
    // Autoservicio: el dueño del negocio, autenticado con su JWT normal (el mismo con el que
    // entra a la web), genera aquí su propia key. Nunca hay que pegarla a mano en ningún lado
    // desde este lado: la instalación Local la obtiene automáticamente al emparejarse
    // (POST /api/v1/sync/connect en el repo local, con las credenciales del dueño). Solo se
    // persiste el hash (Tenant.SyncApiKeyHash); el valor en claro se devuelve una única vez.
    public record GenerateTenantSyncApiKeyCommand : IRequest<SyncApiKeyResultDto>;

    public class GenerateTenantSyncApiKeyCommandHandler
        : IRequestHandler<GenerateTenantSyncApiKeyCommand, SyncApiKeyResultDto>
    {
        private readonly ITenantRepository     _tenantRepo;
        private readonly ICurrentUserService   _currentUser;
        private readonly ISecureTokenGenerator _tokenGenerator;
        private readonly IUnitOfWork           _unitOfWork;
        private readonly IConfiguration        _config;

        public GenerateTenantSyncApiKeyCommandHandler(
            ITenantRepository tenantRepo, ICurrentUserService currentUser,
            ISecureTokenGenerator tokenGenerator, IUnitOfWork unitOfWork, IConfiguration config)
        {
            _tenantRepo     = tenantRepo;
            _currentUser    = currentUser;
            _tokenGenerator = tokenGenerator;
            _unitOfWork     = unitOfWork;
            _config         = config;
        }

        public async Task<SyncApiKeyResultDto> Handle(
            GenerateTenantSyncApiKeyCommand request, CancellationToken cancellationToken)
        {
            if (DeploymentMode.IsLocal(_config))
                throw new DomainException(
                    "La API key de sincronización se genera desde el backend online (tu cuenta en la nube), no desde una instalación Local.");

            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Negocio no encontrado.");

            var rawKey = $"bsk_{_tokenGenerator.Generate(32)}";
            tenant.SetSyncApiKeyHash(SyncApiKeyHasher.Hash(rawKey));

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SyncApiKeyResultDto(rawKey);
        }
    }

    // ── Command: revocar API key de sincronización ──────────────────────────────────
    public record RevokeTenantSyncApiKeyCommand : IRequest;

    public class RevokeTenantSyncApiKeyCommandHandler : IRequestHandler<RevokeTenantSyncApiKeyCommand>
    {
        private readonly ITenantRepository   _tenantRepo;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork         _unitOfWork;

        public RevokeTenantSyncApiKeyCommandHandler(
            ITenantRepository tenantRepo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
        {
            _tenantRepo  = tenantRepo;
            _currentUser = currentUser;
            _unitOfWork  = unitOfWork;
        }

        public async Task Handle(RevokeTenantSyncApiKeyCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Negocio no encontrado.");

            tenant.RevokeSyncApiKey();
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
