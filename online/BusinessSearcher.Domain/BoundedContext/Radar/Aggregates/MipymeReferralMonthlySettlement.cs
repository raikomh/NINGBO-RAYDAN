using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Registro del cierre de la competencia mensual de MiPymes referidas: quién ganó el
    /// premio del mes y cuánto se repartió. Uno por (Year, Month) — evita que el admin
    /// premie dos veces el mismo mes. Guard de idempotencia independiente del de la
    /// competencia de clientes (ReferralMonthlySettlement): son dos competencias separadas
    /// aunque el premio caiga en el mismo saldo unificado de CUP del cliente ganador.
    /// </summary>
    public class MipymeReferralMonthlySettlement : Entity, IAggregateRoot
    {
        public int      Year                     { get; private set; }
        public int      Month                    { get; private set; }
        /// <summary>Null si nadie tuvo MiPymes referidas válidas ese mes (nada que premiar).</summary>
        public Guid?    WinnerClientId           { get; private set; }
        public decimal  PrizeAmount              { get; private set; }
        /// <summary>MiPymes válidas referidas por el GANADOR ese mes — base del cálculo del premio.</summary>
        public int      TotalValidMipymeReferrals { get; private set; }
        public DateTime SettledAt                { get; private set; }

        private MipymeReferralMonthlySettlement() { }

        public static MipymeReferralMonthlySettlement Create(
            int year, int month, Guid? winnerClientId, decimal prizeAmount, int totalValidMipymeReferrals)
        {
            if (month is < 1 or > 12)
                throw new DomainException("Mes inválido.");
            if (prizeAmount < 0)
                throw new DomainException("El premio no puede ser negativo.");
            if (totalValidMipymeReferrals < 0)
                throw new DomainException("El total de referidos no puede ser negativo.");

            return new MipymeReferralMonthlySettlement
            {
                Year                      = year,
                Month                     = month,
                WinnerClientId            = winnerClientId,
                PrizeAmount               = prizeAmount,
                TotalValidMipymeReferrals = totalValidMipymeReferrals,
                SettledAt                 = DateTime.UtcNow
            };
        }
    }
}
