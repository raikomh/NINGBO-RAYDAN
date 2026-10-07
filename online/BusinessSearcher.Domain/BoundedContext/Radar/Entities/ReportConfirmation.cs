using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.Common;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Entities
{
    /// <summary>
    /// Confirmación comunitaria (feature 2) de un reporte de disponibilidad.
    /// Un cliente confirma (Agrees=true) o desmiente (Agrees=false) la información,
    /// y opcionalmente indica el estado real que observó.
    /// </summary>
    public class ReportConfirmation : Entity
    {
        public Guid ReportId { get; private set; }
        public Guid ClientId { get; private set; }

        /// <summary>true = confirma el reporte, false = lo desmiente.</summary>
        public bool Agrees { get; private set; }

        /// <summary>Estado observado por quien confirma (opcional). Permite la señal "no vayas".</summary>
        public AvailabilityStatus? ReportedStatus { get; private set; }

        private ReportConfirmation() { }

        internal ReportConfirmation(Guid reportId, Guid clientId, bool agrees, AvailabilityStatus? reportedStatus)
        {
            ReportId       = reportId;
            ClientId       = clientId;
            Agrees         = agrees;
            ReportedStatus = reportedStatus;
        }

        internal void Update(bool agrees, AvailabilityStatus? reportedStatus)
        {
            Agrees         = agrees;
            ReportedStatus = reportedStatus;
            SetUpdated();
        }
    }
}
