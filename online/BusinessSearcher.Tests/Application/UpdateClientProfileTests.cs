using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Radar.Commands.ClientAuth;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using Moq;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    /// <summary>
    /// PATCH /api/v1/client/auth/profile: el cliente edita su propio nombre, teléfono
    /// y dirección desde la app. La regla central es que null significa "no tocar",
    /// para que la pantalla de dirección y la de datos personales no se pisen entre sí.
    /// </summary>
    public class UpdateClientProfileCommandHandlerTests
    {
        private static (UpdateClientProfileCommandHandler Handler, Client Client) Build()
        {
            var client = Client.Create("Nombre Viejo", "user@test.com", "hash");

            var clients = new Mock<IClientRepository>();
            clients.Setup(c => c.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(client);

            var uow = new Mock<IRadarUnitOfWork>();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(u => u.AccountId).Returns(client.Id);

            return (new UpdateClientProfileCommandHandler(clients.Object, uow.Object, currentUser.Object), client);
        }

        [Fact]
        public async Task Handle_WithAddressNameAndPhone_ShouldPersistAllOfThem()
        {
            var (handler, client) = Build();

            var dto = await handler.Handle(new UpdateClientProfileCommand(
                Street: "Calle 23 #456",
                City: "La Habana",
                State: "La Habana",
                Country: "Cuba",
                FullName: "Nombre Nuevo",
                PhoneNumber: "+5358465182"), CancellationToken.None);

            Assert.Equal("Calle 23 #456", client.Street);
            Assert.Equal("La Habana", client.City);
            Assert.Equal("Cuba", client.Country);
            Assert.Equal("Nombre Nuevo", client.FullName);
            Assert.Equal("+5358465182", client.PhoneNumber);

            // La respuesta que consume la app debe traer ya los valores nuevos.
            Assert.Equal("Nombre Nuevo", dto.FullName);
            Assert.Equal("+5358465182", dto.PhoneNumber);
            Assert.Equal("Calle 23 #456", dto.Street);
        }

        [Fact]
        public async Task Handle_WithOnlyAddress_ShouldNotWipeNameOrPhone()
        {
            var (handler, client) = Build();
            client.SetPhoneNumber("+5350000000");

            await handler.Handle(new UpdateClientProfileCommand(
                Street: "Calle 1", City: "Matanzas"), CancellationToken.None);

            Assert.Equal("Calle 1", client.Street);
            Assert.Equal("Nombre Viejo", client.FullName);
            Assert.Equal("+5350000000", client.PhoneNumber);
        }

        [Fact]
        public async Task Handle_WithOnlyNameAndPhone_ShouldNotWipeAddress()
        {
            var (handler, client) = Build();
            client.SetAddress("Calle 1", "Matanzas", "Matanzas", "Cuba");

            await handler.Handle(new UpdateClientProfileCommand(
                FullName: "Otro Nombre", PhoneNumber: "+5351111111"), CancellationToken.None);

            Assert.Equal("Otro Nombre", client.FullName);
            Assert.Equal("+5351111111", client.PhoneNumber);
            Assert.Equal("Calle 1", client.Street);
            Assert.Equal("Matanzas", client.City);
            Assert.Equal("Cuba", client.Country);
        }

        [Fact]
        public async Task Handle_WithBlankNameOrPhone_ShouldIgnoreThemInsteadOfThrowing()
        {
            var (handler, client) = Build();
            client.SetPhoneNumber("+5350000000");

            // UpdateFullName y SetPhoneNumber lanzan con cadena vacía. El handler los
            // filtra para que un formulario con esos campos en blanco no reviente.
            await handler.Handle(new UpdateClientProfileCommand(
                FullName: "   ", PhoneNumber: ""), CancellationToken.None);

            Assert.Equal("Nombre Viejo", client.FullName);
            Assert.Equal("+5350000000", client.PhoneNumber);
        }

        [Fact]
        public async Task Handle_WithCoordinates_ShouldSetLocation()
        {
            var (handler, client) = Build();

            await handler.Handle(new UpdateClientProfileCommand(
                City: "Santa Clara", Latitude: 22.4069, Longitude: -79.9648), CancellationToken.None);

            Assert.Equal(22.4069, client.Latitude);
            Assert.Equal(-79.9648, client.Longitude);
            Assert.Equal("Santa Clara", client.City);
        }
    }
}
