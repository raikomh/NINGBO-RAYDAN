namespace BusinessSearcher.Domain.BoundedContext.Operations.Enums
{
    public enum ExpenseType { Rent = 1, Salary = 2, Utilities = 3, Marketing = 4, Other = 5, CashOut = 6 }

    /// <summary>Ajuste: usado por la conversión/desglose de inventario (mezclar N productos origen en un producto destino con otra unidad).</summary>
    public enum InventoryMovementType { Entrada = 1, Salida = 2, Traslado = 3, Merma = 4, Ajuste = 5 }

    public enum PurchaseRequestStatus { Pending = 1, Approved = 2, Rejected = 3, Completed = 4 }

    public enum CashMovementType { In = 1, Out = 2, Expense = 3, Refund = 4 }

    public enum CashRegisterStatus { Open = 1, Closed = 2 }

    public enum SaleStatus { Completed = 1, Refunded = 2 }

    public enum PaymentMethod { Cash = 1, Card = 2, Transfer = 3, Mixed = 4 }

    public enum Currency { CUP = 1, USD = 2 }

    public enum DiscountType { Amount = 1, Percentage = 2 }

    public enum PurchaseStatus { Pending = 1, Completed = 2, Cancelled = 3 }

    public enum InventoryCountStatus { Open = 1, Closed = 2 }

    /// <summary>Rol operativo del usuario dentro del negocio (TPV/ERP).</summary>
    public enum OperationsRole
    {
        Administrador = 1,
        Cajero        = 2,
        JefeDeTurno   = 3,
        Almacenero    = 4,
        Comercial     = 5,
        Auditor       = 6
    }
}
