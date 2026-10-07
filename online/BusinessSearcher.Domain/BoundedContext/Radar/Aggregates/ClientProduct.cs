using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Producto puesto en venta por un cliente particular (no un negocio/Tenant).
    /// Aparece en el buscador junto a los productos de tiendas (con etiqueta de origen)
    /// y en el mapa como pin azul, ubicado en la posición GPS del cliente-vendedor.
    /// </summary>
    public class ClientProduct : Entity, IAggregateRoot
    {
        public Guid    ClientId              { get; private set; }
        public string  Name                  { get; private set; } = default!;
        public string  NormalizedName        { get; private set; } = default!;
        public string? Description           { get; private set; }
        public decimal Price                 { get; private set; }
        public string  Currency              { get; private set; } = "CUP";
        public bool    IsAvailable           { get; private set; } = true;
        public string? ImageUrl              { get; private set; }

        private ClientProduct() { }

        public static ClientProduct Create(
            Guid clientId, string name, decimal price,
            string currency = "CUP", string? description = null, string? imageUrl = null)
        {
            if (clientId == Guid.Empty)
                throw new DomainException("El producto debe pertenecer a un cliente.");
            ValidateName(name);
            if (price < 0)
                throw new DomainException("El precio no puede ser negativo.");

            return new ClientProduct
            {
                ClientId       = clientId,
                Name           = name.Trim(),
                NormalizedName = AvailabilityReport.Normalize(name),
                Description    = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Price          = price,
                Currency       = string.IsNullOrWhiteSpace(currency) ? "CUP" : currency.Trim().ToUpperInvariant(),
                ImageUrl       = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim()
            };
        }

        public void Update(string name, decimal price, string currency, string? description, string? imageUrl)
        {
            ValidateName(name);
            if (price < 0)
                throw new DomainException("El precio no puede ser negativo.");

            Name           = name.Trim();
            NormalizedName = AvailabilityReport.Normalize(name);
            Price          = price;
            Currency       = string.IsNullOrWhiteSpace(currency) ? Currency : currency.Trim().ToUpperInvariant();
            Description    = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            ImageUrl       = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
            SetUpdated();
        }

        public void SetAvailability(bool isAvailable)
        {
            IsAvailable = isAvailable;
            SetUpdated();
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("El nombre del producto es requerido.");
            if (name.Trim().Length < 2)
                throw new DomainException("El nombre del producto debe tener al menos 2 caracteres.");
            if (name.Length > 200)
                throw new DomainException("El nombre del producto no puede exceder 200 caracteres.");
        }
    }
}
