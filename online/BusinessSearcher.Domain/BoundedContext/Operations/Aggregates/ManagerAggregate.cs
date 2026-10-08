using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>
    /// Gestor de ventas de un negocio (TPV). No inicia sesión: es un dato de referencia cuyo
    /// <see cref="Code"/> se asocia a cada venta para saber qué gestor la realizó.
    /// </summary>
    public class Manager : Entity, IAggregateRoot
    {
        public Guid    TenantId     { get; private set; }
        public string  Code         { get; private set; } = default!;
        public string  Name         { get; private set; } = default!;
        /// <summary>Carné de identidad.</summary>
        public string  IdNumber     { get; private set; } = default!;
        public string  Municipality { get; private set; } = default!;
        public string  Province     { get; private set; } = default!;
        public string  Phone        { get; private set; } = default!;
        public bool    IsActive     { get; private set; } = true;

        private Manager() { }
        private Manager(Guid id) : base(id) { }

        public static Manager Create(Guid tenantId, string code, string name, string idNumber,
            string municipality, string province, string phone)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El gestor debe pertenecer a un negocio.");
            var m = new Manager { TenantId = tenantId };
            m.Apply(code, name, idNumber, municipality, province, phone);
            return m;
        }

        public void Update(string code, string name, string idNumber, string municipality, string province,
            string phone, bool isActive)
        {
            Apply(code, name, idNumber, municipality, province, phone);
            IsActive = isActive; SetUpdated();
        }

        private void Apply(string code, string name, string idNumber, string municipality, string province, string phone)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new DomainException("El código del gestor es requerido.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del gestor es requerido.");
            if (string.IsNullOrWhiteSpace(idNumber)) throw new DomainException("El CI del gestor es requerido.");
            if (string.IsNullOrWhiteSpace(municipality)) throw new DomainException("El municipio es requerido.");
            if (string.IsNullOrWhiteSpace(province)) throw new DomainException("La provincia es requerida.");
            if (string.IsNullOrWhiteSpace(phone)) throw new DomainException("El número de teléfono es requerido.");
            Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); IdNumber = idNumber.Trim();
            Municipality = municipality.Trim(); Province = province.Trim(); Phone = phone.Trim();
        }
    }
}
