using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>Movimiento de efectivo dentro de una caja abierta (TPV: CashMovement).</summary>
    public class CashMovement : Entity
    {
        public Guid             TenantId    { get; private set; }
        public Guid             RegisterId  { get; private set; }
        public DateTime         Date        { get; private set; }
        public CashMovementType Type        { get; private set; }
        public decimal          Amount      { get; private set; }
        public decimal?         AmountUSD    { get; private set; }
        public Currency         Currency    { get; private set; }
        public string?          Description { get; private set; }
        public Guid?            UserId      { get; private set; }

        private CashMovement() { }
        private CashMovement(Guid id) : base(id) { }

        public static CashMovement Create(Guid tenantId, Guid registerId, CashMovementType type, decimal amount,
            Currency currency, string? description = null, decimal? amountUsd = null, Guid? userId = null)
        {
            if (amount < 0) throw new DomainException("El monto no puede ser negativo.");
            return new CashMovement
            {
                TenantId = tenantId, RegisterId = registerId, Type = type, Amount = amount, Currency = currency,
                AmountUSD = amountUsd, Description = description?.Trim(), UserId = userId, Date = DateTime.UtcNow
            };
        }

        public static CashMovement Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            Guid registerId, DateTime date, CashMovementType type, decimal amount, decimal? amountUsd,
            Currency currency, string? description, Guid? userId)
        {
            var movement = new CashMovement(id)
            {
                TenantId = tenantId, RegisterId = registerId, Type = type, Amount = amount, Currency = currency,
                AmountUSD = amountUsd, Description = description, UserId = userId, Date = date
            };
            movement.CreatedAt = createdAt;
            movement.UpdatedAt = updatedAt;
            return movement;
        }
    }

    /// <summary>
    /// Caja / arqueo (TPV: CashRegister). Se abre con monto inicial y se cierra comparando
    /// el esperado (inicial + ventas efectivo + entradas − salidas − gastos) contra el real contado.
    /// </summary>
    public class CashRegister : Entity, IAggregateRoot
    {
        public Guid               TenantId        { get; private set; }
        public Guid?              WarehouseId     { get; private set; }
        public DateTime           OpenDate        { get; private set; }
        public DateTime?          CloseDate       { get; private set; }
        public decimal            InitialAmount   { get; private set; }
        public decimal?           ExpectedAmount  { get; private set; }
        public decimal?           ActualAmount    { get; private set; }
        public decimal?           Difference      { get; private set; }
        public decimal?           InitialAmountUSD{ get; private set; }
        public decimal?           ExpectedAmountUSD{ get; private set; }
        public decimal?           ActualAmountUSD { get; private set; }
        public decimal?           DifferenceUSD   { get; private set; }
        public CashRegisterStatus Status          { get; private set; } = CashRegisterStatus.Open;
        public Guid               OpenedBy        { get; private set; }
        public Guid?              ClosedBy        { get; private set; }
        public int                SalesCount      { get; private set; }
        public decimal            TotalSales      { get; private set; }
        public decimal            TotalExpenses   { get; private set; }
        public decimal            TotalCashIn     { get; private set; }
        public decimal            TotalCashOut    { get; private set; }

        /// <summary>Si ya se hizo y validó el conteo físico de inventario del almacén durante este turno. Si la caja
        /// tiene almacén asignado, es requisito obligatorio para poder cerrarla.</summary>
        public bool               InventoryCountCompleted { get; private set; }

        private CashRegister() { }
        private CashRegister(Guid id) : base(id) { }

        public static CashRegister Open(Guid tenantId, Guid openedBy, decimal initialAmount,
            Guid? warehouseId = null, decimal? initialAmountUsd = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La caja debe pertenecer a un negocio.");
            if (initialAmount < 0) throw new DomainException("El monto inicial no puede ser negativo.");
            return new CashRegister
            {
                TenantId = tenantId, OpenedBy = openedBy, InitialAmount = initialAmount, InitialAmountUSD = initialAmountUsd,
                WarehouseId = warehouseId, OpenDate = DateTime.UtcNow, Status = CashRegisterStatus.Open
            };
        }

        /// <summary>Acumula totales al registrar una venta/gasto/movimiento sobre esta caja.</summary>
        public void RegisterSale(decimal cashAmount)
        {
            EnsureOpen();
            SalesCount += 1;
            TotalSales += cashAmount;
            SetUpdated();
        }

        public void RegisterCashMovement(CashMovementType type, decimal amount)
        {
            EnsureOpen();
            switch (type)
            {
                case CashMovementType.In:      TotalCashIn  += amount; break;
                case CashMovementType.Out:     TotalCashOut += amount; break;
                case CashMovementType.Expense: TotalExpenses += amount; break;
                case CashMovementType.Refund:  TotalCashOut += amount; break;
            }
            SetUpdated();
        }

        public decimal ComputeExpected() => InitialAmount + TotalSales + TotalCashIn - TotalCashOut - TotalExpenses;

        /// <summary>Se marca automáticamente cuando se cierra un conteo físico del mismo almacén durante el turno.</summary>
        public void MarkInventoryCountCompleted()
        {
            InventoryCountCompleted = true;
            SetUpdated();
        }

        public void Close(Guid closedBy, decimal actualAmount, decimal? actualAmountUsd = null)
        {
            EnsureOpen();
            if (WarehouseId.HasValue && !InventoryCountCompleted)
                throw new DomainException("Debes cerrar y validar (auditar) el conteo físico de inventario del almacén antes de cerrar la caja.");

            var expected   = ComputeExpected();
            var difference = Math.Round(actualAmount - expected, 2);
            // Cuadre estricto: el efectivo contado (CUP) debe coincidir exactamente con el esperado.
            // No se exige lo mismo en USD porque los movimientos de caja no acumulan un total en USD
            // (solo se registran a título informativo), así que "esperado" en USD no es un valor confiable.
            if (difference != 0)
                throw new DomainException(
                    $"El efectivo contado ({actualAmount:0.00} CUP) no coincide con el esperado ({expected:0.00} CUP). " +
                    $"Diferencia: {difference:0.00} CUP. Recuenta o registra un movimiento que explique la diferencia antes de cerrar la caja.");

            ExpectedAmount = expected;
            ActualAmount   = actualAmount;
            Difference     = difference;
            ActualAmountUSD = actualAmountUsd;
            if (InitialAmountUSD.HasValue) { ExpectedAmountUSD = InitialAmountUSD; DifferenceUSD = (actualAmountUsd ?? 0) - (InitialAmountUSD ?? 0); }
            ClosedBy  = closedBy;
            CloseDate = DateTime.UtcNow;
            Status    = CashRegisterStatus.Closed;
            SetUpdated();
        }

        private void EnsureOpen()
        {
            if (Status != CashRegisterStatus.Open) throw new DomainException("La caja no está abierta.");
        }

        public static CashRegister Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            Guid? warehouseId, DateTime openDate, DateTime? closeDate,
            decimal initialAmount, decimal? expectedAmount, decimal? actualAmount, decimal? difference,
            decimal? initialAmountUsd, decimal? expectedAmountUsd, decimal? actualAmountUsd, decimal? differenceUsd,
            CashRegisterStatus status, Guid openedBy, Guid? closedBy, int salesCount, decimal totalSales,
            decimal totalExpenses, decimal totalCashIn, decimal totalCashOut, bool inventoryCountCompleted)
        {
            var register = new CashRegister(id)
            {
                TenantId = tenantId, WarehouseId = warehouseId,
                OpenDate = openDate, CloseDate = closeDate, InitialAmount = initialAmount,
                ExpectedAmount = expectedAmount, ActualAmount = actualAmount, Difference = difference,
                InitialAmountUSD = initialAmountUsd, ExpectedAmountUSD = expectedAmountUsd,
                ActualAmountUSD = actualAmountUsd, DifferenceUSD = differenceUsd, Status = status,
                OpenedBy = openedBy, ClosedBy = closedBy, SalesCount = salesCount, TotalSales = totalSales,
                TotalExpenses = totalExpenses, TotalCashIn = totalCashIn, TotalCashOut = totalCashOut,
                InventoryCountCompleted = inventoryCountCompleted
            };
            register.CreatedAt = createdAt;
            register.UpdatedAt = updatedAt;
            return register;
        }
    }
}
