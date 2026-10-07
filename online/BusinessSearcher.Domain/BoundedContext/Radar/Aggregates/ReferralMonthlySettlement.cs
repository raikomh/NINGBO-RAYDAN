using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Registro del cierre de la competencia mensual de referidos: quién ganó el
    /// premio del mes y cuánto se repartió. Uno por (Year, Month) — evita que el
    /// admin premie dos veces el mismo mes.
    /// </summary>
    public class ReferralMonthlySettlement : Entity, IAggregateRoot
    {
        public int      Year                { get; private set; }
        public int      Month               { get; private set; }
        /// <summary>Null si nadie tuvo referidos válidos ese mes (nada que premiar).</summary>
        public Guid?    WinnerClientId      { get; private set; }
        public decimal  PrizeAmount         { get; private set; }
        /// <summary>Referidos válidos del GANADOR ese mes (no el total de la plataforma) — es la base del cálculo del premio.</summary>
        public int      TotalValidReferrals { get; private set; }
        public DateTime SettledAt           { get; private set; }

        private ReferralMonthlySettlement() { }

        public static ReferralMonthlySettlement Create(
            int year, int month, Guid? winnerClientId, decimal prizeAmount, int totalValidReferrals)
        {
            if (month is < 1 or > 12)
                throw new DomainException("Mes inválido.");
            if (prizeAmount < 0)
                throw new DomainException("El premio no puede ser negativo.");
            if (totalValidReferrals < 0)
                throw new DomainException("El total de referidos no puede ser negativo.");

            return new ReferralMonthlySettlement
            {
                Year                = year,
                Month               = month,
                WinnerClientId      = winnerClientId,
                PrizeAmount         = prizeAmount,
                TotalValidReferrals = totalValidReferrals,
                SettledAt           = DateTime.UtcNow
            };
        }
    }
}
