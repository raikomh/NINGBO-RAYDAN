using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>Proveedor del negocio (TPV: Supplier).</summary>
    public class Supplier : Entity, IAggregateRoot
    {
        public Guid    TenantId { get; private set; }
        public string  Name     { get; private set; } = default!;
        public string? Contact  { get; private set; }
        public string  Phone    { get; private set; } = default!;
        public string? Email    { get; private set; }
        public string? Address  { get; private set; }
        public double? Latitude { get; private set; }
        public double? Longitude{ get; private set; }
        public string? Category { get; private set; }
        public bool    IsActive { get; private set; } = true;

        private Supplier() { }
        private Supplier(Guid id) : base(id) { }

        public static Supplier Create(Guid tenantId, string name, string phone, string? email = null,
            string? contact = null, string? address = null, string? category = null,
            double? latitude = null, double? longitude = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El proveedor debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del proveedor es requerido.");
            return new Supplier
            {
                TenantId = tenantId, Name = name.Trim(), Phone = (phone ?? "").Trim(),
                Email = email?.Trim(), Contact = contact?.Trim(), Address = address?.Trim(),
                Category = category?.Trim(), Latitude = latitude, Longitude = longitude
            };
        }

        public static Supplier Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string name, string? contact, string phone, string? email, string? address,
            double? latitude, double? longitude, string? category, bool isActive)
        {
            var supplier = new Supplier(id)
            {
                TenantId = tenantId, Name = name, Phone = phone, Email = email, Contact = contact,
                Address = address, Category = category, Latitude = latitude, Longitude = longitude, IsActive = isActive
            };
            supplier.CreatedAt = createdAt;
            supplier.UpdatedAt = updatedAt;
            return supplier;
        }

        public void Update(string name, string phone, string? email, string? contact,
            string? address, string? category, double? latitude, double? longitude)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del proveedor es requerido.");
            Name = name.Trim(); Phone = (phone ?? "").Trim(); Email = email?.Trim();
            Contact = contact?.Trim(); Address = address?.Trim(); Category = category?.Trim();
            Latitude = latitude; Longitude = longitude; SetUpdated();
        }

        public void Deactivate() { IsActive = false; SetUpdated(); }
    }

    /// <summary>Almacén/depósito del negocio (TPV: Warehouse).</summary>
    public class Warehouse : Entity, IAggregateRoot
    {
        public Guid    TenantId    { get; private set; }
        public string  Name        { get; private set; } = default!;
        public string? Location    { get; private set; }
        public string? Description { get; private set; }
        public bool    IsActive    { get; private set; } = true;

        private Warehouse() { }
        private Warehouse(Guid id) : base(id) { }

        public static Warehouse Create(Guid tenantId, string name, string? location = null, string? description = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El almacén debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del almacén es requerido.");
            return new Warehouse { TenantId = tenantId, Name = name.Trim(), Location = location?.Trim(), Description = description?.Trim() };
        }

        public static Warehouse Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string name, string? location, string? description, bool isActive)
        {
            var warehouse = new Warehouse(id) { TenantId = tenantId, Name = name, Location = location, Description = description, IsActive = isActive };
            warehouse.CreatedAt = createdAt;
            warehouse.UpdatedAt = updatedAt;
            return warehouse;
        }

        public void Update(string name, string? location, string? description)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del almacén es requerido.");
            Name = name.Trim(); Location = location?.Trim(); Description = description?.Trim(); SetUpdated();
        }

        public void Deactivate() { IsActive = false; SetUpdated(); }
    }

    /// <summary>Gasto operativo del negocio (TPV: Expense).</summary>
    public class Expense : Entity, IAggregateRoot
    {
        public Guid        TenantId    { get; private set; }
        public ExpenseType Type        { get; private set; }
        public decimal     Amount      { get; private set; }
        public decimal?    AmountUSD   { get; private set; }
        public string      Description { get; private set; } = default!;
        public Guid?       UserId      { get; private set; }
        public Guid?       RegisterId  { get; private set; }
        public DateTime    Date        { get; private set; }

        private Expense() { }
        private Expense(Guid id) : base(id) { }

        public static Expense Create(Guid tenantId, ExpenseType type, decimal amount, string description,
            decimal? amountUsd = null, Guid? userId = null, Guid? registerId = null, DateTime? date = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El gasto debe pertenecer a un negocio.");
            if (amount < 0) throw new DomainException("El monto no puede ser negativo.");
            return new Expense
            {
                TenantId = tenantId, Type = type, Amount = amount, AmountUSD = amountUsd,
                Description = (description ?? "").Trim(), UserId = userId, RegisterId = registerId,
                Date = date ?? DateTime.UtcNow
            };
        }

        public static Expense Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            ExpenseType type, decimal amount, decimal? amountUsd, string description,
            Guid? userId, Guid? registerId, DateTime date)
        {
            var expense = new Expense(id)
            {
                TenantId = tenantId, Type = type, Amount = amount, AmountUSD = amountUsd,
                Description = description, UserId = userId, RegisterId = registerId, Date = date
            };
            expense.CreatedAt = createdAt;
            expense.UpdatedAt = updatedAt;
            return expense;
        }
    }

    /// <summary>Registro de tasa de cambio diaria CUP/USD (TPV: ExchangeRateLog).</summary>
    public class ExchangeRateLog : Entity, IAggregateRoot
    {
        public Guid     TenantId { get; private set; }
        public decimal  Rate     { get; private set; }
        public DateTime Date     { get; private set; }

        private ExchangeRateLog() { }

        public static ExchangeRateLog Create(Guid tenantId, decimal rate, DateTime? date = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La tasa debe pertenecer a un negocio.");
            if (rate <= 0) throw new DomainException("La tasa de cambio debe ser mayor que cero.");
            return new ExchangeRateLog { TenantId = tenantId, Rate = rate, Date = date ?? DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Configuración salarial de un rol operativo (TPV: RoleSalaryConfig): salario fijo y % de venta
    /// que cobran los trabajadores con ese rol al registrar el gasto de nómina. Uno por (Tenant, Role).
    /// </summary>
    public class RoleSalaryConfig : Entity, IAggregateRoot
    {
        public Guid           TenantId        { get; private set; }
        public OperationsRole Role            { get; private set; }
        public decimal        BaseSalary      { get; private set; }
        public decimal        SalesPercentage { get; private set; }

        private RoleSalaryConfig() { }
        private RoleSalaryConfig(Guid id) : base(id) { }

        public static RoleSalaryConfig Create(Guid tenantId, OperationsRole role, decimal baseSalary, decimal salesPercentage)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La configuración debe pertenecer a un negocio.");
            Validate(baseSalary, salesPercentage);
            return new RoleSalaryConfig { TenantId = tenantId, Role = role, BaseSalary = baseSalary, SalesPercentage = salesPercentage };
        }

        public void Update(decimal baseSalary, decimal salesPercentage)
        {
            Validate(baseSalary, salesPercentage);
            BaseSalary = baseSalary; SalesPercentage = salesPercentage; SetUpdated();
        }

        private static void Validate(decimal baseSalary, decimal salesPercentage)
        {
            if (baseSalary < 0) throw new DomainException("El salario fijo no puede ser negativo.");
            if (salesPercentage < 0 || salesPercentage > 100) throw new DomainException("El % de venta debe estar entre 0 y 100.");
        }
    }
}
