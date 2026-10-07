namespace BusinessSearcher.Domain.BoundedContext.Radar.Enums
{
    /// <summary>Plan del cliente consumidor. Premium habilita favoritos y alertas de radar.</summary>
    public enum ClientPlan
    {
        Normal  = 1,
        Premium = 2
    }

    /// <summary>Estado de disponibilidad reportado para un producto en un lugar.</summary>
    public enum AvailabilityStatus
    {
        /// <summary>🟢 Hay producto.</summary>
        Available  = 1,
        /// <summary>🟡 Poco stock.</summary>
        LowStock   = 2,
        /// <summary>🔴 Agotado.</summary>
        OutOfStock = 3
    }

    /// <summary>Estado de la cola reportado en el lugar (feature 8).</summary>
    public enum QueueStatus
    {
        Unknown = 0,
        /// <summary>🟢 Sin cola.</summary>
        NoQueue = 1,
        /// <summary>🟡 Espera corta (~15 min).</summary>
        Short   = 2,
        /// <summary>🔴 Espera larga (más de 1 h).</summary>
        Long    = 3
    }

    /// <summary>Origen del reporte (feature 17: comercios verificados).</summary>
    public enum ReportSource
    {
        /// <summary>Reportado por un usuario de la comunidad.</summary>
        Community = 0,
        /// <summary>Publicado directamente por el propio comercio (verificado).</summary>
        Store     = 1
    }

    /// <summary>Estado de un referido en el programa de invitaciones.</summary>
    public enum ReferralStatus
    {
        /// <summary>Registrado pero aún no validado (no cuenta para recompensa).</summary>
        Pending  = 0,
        /// <summary>El invitado fue aprobado por un admin (o pasó a premium): referido válido.</summary>
        Valid    = 1,
        /// <summary>Descartado (fraude, cuenta suspendida, etc.).</summary>
        Rejected = 2
    }

    /// <summary>Tipo de movimiento en el saldo de CUP por referidos de un cliente.</summary>
    public enum ReferralCupTransactionType
    {
        /// <summary>10 CUP fijos por cada referido validado.</summary>
        PerReferralBonus = 1,
        /// <summary>Premio del top 1 de la competencia mensual.</summary>
        MonthlyPrize     = 2,
        /// <summary>Liquidación: el admin marca el saldo como pagado (monto negativo, vuelve a 0).</summary>
        PayoutSettlement = 3,
        /// <summary>7000 CUP fijos por cada MiPyme (Tenant) referida y validada.</summary>
        MipymeReferralBonus = 4,
        /// <summary>Premio del top 1 de la competencia mensual de MiPymes referidas.</summary>
        MipymeMonthlyPrize  = 5
    }

    public enum PremiumClaimStatus
    {
        Pending  = 1,
        Approved = 2,
        Rejected = 3
    }
}
