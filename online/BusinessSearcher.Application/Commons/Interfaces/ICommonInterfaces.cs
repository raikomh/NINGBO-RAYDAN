using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Identity;

namespace BusinessSearcher.Application.Commons.Interfaces
{
    // CORRECCIÓN: "IUnitOfWortk" → "IUnitOfWork"
    public interface IUnitOfWork : IDisposable
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }

    public interface IStoreUnitOfWork : IUnitOfWork { }

    public interface IRadarUnitOfWork : IUnitOfWork { }

    public interface IOperationsUnitOfWork : IUnitOfWork { }

    // Extrae TenantId del JWT para el contexto multi-tenant
    public interface ICurrentUserService
    {
        Guid   TenantId     { get; }
        string Email        { get; }
        string BusinessName { get; }
        string Plan         { get; }
        TenantType TenantType { get; }
        bool   IsAuthenticated { get; }

        /// <summary>Id del sujeto autenticado (sub del JWT) para cualquier rol.</summary>
        Guid        AccountId { get; }
        /// <summary>Rol de la cuenta autenticada (Client/Store/Admin).</summary>
        AccountRole Role      { get; }

        /// <summary>
        /// Rol operativo (TPV) del usuario dentro del negocio. El dueño del negocio (Tenant)
        /// es Administrador; los sub-usuarios llevan su rol en el claim <c>opsRole</c>.
        /// Null si no es una sesión de negocio.
        /// </summary>
        OperationsRole? OpsRole { get; }
    }

    public interface IPasswordHasher
    {
        string Hash(string password);
        bool   Verify(string password, string hash);
    }

    public interface IJwtTokenService
    {
        string GenerateToken(Guid tenantId, string email, string businessName, string plan, TenantType tenantType);
        /// <summary>Genera un JWT para un Cliente consumidor (rol Client).</summary>
        string GenerateClientToken(Guid clientId, string email, string fullName, string plan);
        /// <summary>
        /// Genera un JWT para un sub-usuario operativo (TPV): sub = id del usuario, claim
        /// <c>tenantId</c> = negocio dueño, claim <c>opsRole</c> = rol operativo, rol de cuenta Store.
        /// </summary>
        string GenerateOperationsUserToken(Guid userId, Guid tenantId, string email, string name, OperationsRole opsRole);
        bool   ValidateToken(string token);
    }

    /// <summary>Genera tokens aleatorios criptográficamente seguros (refresh tokens, reset tokens)</summary>
    public interface ISecureTokenGenerator
    {
        string Generate(int sizeInBytes = 64);
    }

    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string businessName, string resetLink, CancellationToken cancellationToken = default);
        Task SendWelcomeEmailAsync(string toEmail, string businessName, CancellationToken cancellationToken = default);
        Task SendPasswordChangedNotificationAsync(string toEmail, string businessName, CancellationToken cancellationToken = default);
        Task SendEmailVerificationAsync(string toEmail, string businessName, string verificationLink, CancellationToken cancellationToken = default);
    }

    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
        Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
        bool IsValidImageContentType(string contentType);
        /// <summary>
        /// Trae los bytes de un archivo ya subido a partir de su key (la parte después de
        /// "/api/v1/files/" en la URL devuelta por <see cref="UploadAsync"/>). Permite servir
        /// archivos de un bucket privado a través del propio backend, sin exponerlo públicamente.
        /// </summary>
        Task<(Stream Stream, string ContentType)> DownloadAsync(string key, CancellationToken cancellationToken = default);
    }

    public interface IDateTimeService
    {
        DateTime UtcNow { get; }
        DateTime Now    { get; }
    }

    public interface IAvailabilityNotifier
    {
        Task NotifyProductAvailabilityChangedAsync(
            Guid   productId,
            Guid   storeId,
            string storeName,
            string productName,
            bool   isAvailable,
            CancellationToken cancellationToken = default);
    }

    public interface IFcmNotificationService
    {
        Task SendToTokenAsync(
            string token, string title, string body,
            Dictionary<string, string>? data = null,
            CancellationToken cancellationToken = default);

        Task SendToTopicAsync(
            string topic, string title, string body,
            Dictionary<string, string>? data = null,
            CancellationToken cancellationToken = default);
    }

    public interface ITenantSchemaManager
    {
        Task CreateSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task DropSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> SchemaExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }

    /// <summary>Parsea el Excel de catálogo mayorista (columnas: Producto, Cantidad, Precio, Venta Minima)</summary>
    public interface IExcelCatalogParser
    {
        ExcelCatalogParseResult ParseWholesaleCatalog(Stream fileStream, CancellationToken cancellationToken = default);
    }

    /// <summary>Exporta los reportes del TPV/ERP a Excel (.xlsx) usando ClosedXML.</summary>
    public interface IOperationsReportExporter
    {
        byte[] ExportSales(SalesReportDto report);
        byte[] ExportInventory(InventoryReportDto report);
        byte[] ExportExpenses(ExpensesReportDto report);
        byte[] ExportOrdenEntrega(OrdenEntregaDto orden);
    }

    public sealed record CatalogRowDto(int RowNumber, string ProductName, int Quantity, decimal Price, int MinOrderQuantity);

    public sealed record ExcelCatalogParseResult(
        bool IsValid,
        IReadOnlyList<CatalogRowDto> Rows,
        IReadOnlyList<string> Errors);

    /// <summary>Parsea el Excel de importación de ventas (columnas: Código, Producto, Cantidad, Precio)</summary>
    public interface IExcelSalesImportParser
    {
        ExcelSalesImportParseResult ParseSales(Stream fileStream, CancellationToken cancellationToken = default);
    }

    public sealed record SalesImportRowDto(int RowNumber, string? Barcode, string? ProductName, int Quantity, decimal Price);

    public sealed record ExcelSalesImportParseResult(
        bool IsValid,
        IReadOnlyList<SalesImportRowDto> Rows,
        IReadOnlyList<string> Errors);

    /// <summary>Parsea el catálogo de productos de la plaza (Excel). Columnas obligatorias: Código, Producto,
    /// Categoría, Cant. disponible y Precio x unidad (USD). Descripción es opcional.</summary>
    public interface IExcelProductCatalogParser
    {
        ExcelProductCatalogParseResult Parse(Stream fileStream);
    }

    public sealed record ProductCatalogRowDto(
        int RowNumber, string Barcode, string Name, string Category, int Stock, decimal PriceUsd, string? Description,
        byte[]? ImageBytes = null, string? ImageContentType = null);

    public sealed record ExcelProductCatalogParseResult(
        bool IsValid,
        IReadOnlyList<ProductCatalogRowDto> Rows,
        IReadOnlyList<string> Errors);

    public interface IChatNotifier
    {
        Task NotifyNewMessageAsync(
            Guid   tenantId,
            string senderName,
            string text,
            bool   isFromAdmin,
            DateTime sentAt,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Emite en tiempo real (SignalR) los reportes del radar colaborativo a los
    /// clientes suscritos (por ciudad y globalmente).
    /// </summary>
    public interface IRadarNotifier
    {
        Task BroadcastNewReportAsync(ReportDto report, CancellationToken cancellationToken = default);
    }

    /// <summary>Un turno de conversación con el asistente de IA.</summary>
    public record AiMessage(string Role, string Content);   // Role: "user" | "assistant"

    /// <summary>
    /// Proveedor de chat con modelo de lenguaje (GroqCloud). La API key vive
    /// solo en el servidor (config `Groq:ApiKey`), nunca en el cliente.
    /// </summary>
    public interface IAiChatService
    {
        /// <summary>True si hay una API key configurada (permite degradar con elegancia).</summary>
        bool IsConfigured { get; }

        Task<string> CompleteAsync(
            string systemPrompt,
            IReadOnlyList<AiMessage> conversation,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Recibe un paquete de sincronización subido por una instalación local y lo integra en los
    /// datos operativos del tenant online correspondiente (ver <c>EntityIdRewriter</c>).
    /// </summary>
    public interface ISyncIngestService
    {
        Task<SyncIngestResultDto> IngestAsync(
            Guid onlineTenantId, SyncPushEnvelopeDto envelope, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Arma el snapshot completo de los datos operativos de un tenant online para que una
    /// instalación en modo Local lo descargue (pull del Administrador).
    /// </summary>
    public interface ISyncExportService
    {
        Task<SyncPushEnvelopeDto> ExportAsync(Guid onlineTenantId, CancellationToken cancellationToken = default);
    }
}
