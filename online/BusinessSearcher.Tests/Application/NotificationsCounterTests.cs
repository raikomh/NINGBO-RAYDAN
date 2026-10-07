using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Operations.Products;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using Moq;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    // Cubre el bug reportado: el contador de notificaciones no vistas no aumentaba
    // con notificaciones nuevas ni bajaba a cero al marcarlas como vistas.
    public class NotificationsCounterTests
    {
        private static readonly Guid TenantId  = Guid.NewGuid();
        private static readonly Guid AccountId = Guid.NewGuid();

        private static Mock<ICurrentUserService> CurrentUserMock()
        {
            var u = new Mock<ICurrentUserService>();
            u.SetupGet(x => x.TenantId).Returns(TenantId);
            u.SetupGet(x => x.AccountId).Returns(AccountId);
            return u;
        }

        [Fact]
        public async Task UnreadCount_WithNoSeenMarker_ShouldCountAllNotifications()
        {
            var product = Product.Create(TenantId, "Arroz", 10, 20, minStock: 5);

            var products = new Mock<IProductRepository>();
            products.Setup(p => p.GetByTenantAsync(TenantId, null, null, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Product> { product });

            var priceHistory = new Mock<IProductPriceHistoryRepository>();
            priceHistory.Setup(p => p.GetRecentAsync(TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProductPriceHistory>());

            var settings = new Mock<ISettingRepository>();
            settings.Setup(s => s.GetByKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Setting?)null);

            var handler = new GetNotificationsUnreadCountHandler(
                products.Object, priceHistory.Object, settings.Object, CurrentUserMock().Object);

            var count = await handler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);

            // Antes del fix nunca había un endpoint/handler que calculara esto contra la
            // marca "seenAt": sin marca previa, todas las notificaciones existentes cuentan.
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task MarkSeen_ThenUnreadCount_ShouldDropToZero()
        {
            var product = Product.Create(TenantId, "Arroz", 10, 20, minStock: 5);

            var products = new Mock<IProductRepository>();
            products.Setup(p => p.GetByTenantAsync(TenantId, null, null, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Product> { product });

            var priceHistory = new Mock<IProductPriceHistoryRepository>();
            priceHistory.Setup(p => p.GetRecentAsync(TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProductPriceHistory>());

            Setting? stored = null;
            var settings = new Mock<ISettingRepository>();
            settings.Setup(s => s.GetByKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(stored));
            settings.Setup(s => s.AddAsync(It.IsAny<Setting>(), It.IsAny<CancellationToken>()))
                .Callback<Setting, CancellationToken>((setting, _) => stored = setting)
                .Returns(Task.CompletedTask);

            var uow = new Mock<IOperationsUnitOfWork>();

            // El producto ya existía antes de marcar como vistas las notificaciones.
            await Task.Delay(5);

            var markHandler = new MarkNotificationsSeenHandler(settings.Object, uow.Object, CurrentUserMock().Object);
            await markHandler.Handle(new MarkNotificationsSeenCommand(), CancellationToken.None);

            settings.Verify(s => s.AddAsync(It.IsAny<Setting>(), It.IsAny<CancellationToken>()), Times.Once);
            uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(stored);

            var countHandler = new GetNotificationsUnreadCountHandler(
                products.Object, priceHistory.Object, settings.Object, CurrentUserMock().Object);
            var count = await countHandler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task UnreadCount_ShouldIncreaseWhenNewNotificationArrivesAfterMarkedSeen()
        {
            var product = Product.Create(TenantId, "Arroz", 10, 20, minStock: 5);

            var products = new Mock<IProductRepository>();
            products.Setup(p => p.GetByTenantAsync(TenantId, null, null, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Product> { product });

            var seenAt = DateTime.UtcNow;
            var seenMarker = Setting.Create(TenantId, "ops.notifications.seenAt.x", seenAt.ToString("O"));

            var settings = new Mock<ISettingRepository>();
            settings.Setup(s => s.GetByKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(seenMarker);

            var priceHistory = new Mock<IProductPriceHistoryRepository>();

            // Sin cambios de precio nuevos: el único hecho "nuevo" sería el producto,
            // pero ya fue creado antes de seenAt, así que no debe contar.
            priceHistory.Setup(p => p.GetRecentAsync(TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProductPriceHistory>());

            var handler = new GetNotificationsUnreadCountHandler(
                products.Object, priceHistory.Object, settings.Object, CurrentUserMock().Object);

            var countBefore = await handler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);
            Assert.Equal(0, countBefore);

            // Llega un cambio de precio nuevo, posterior a la última vez que se marcaron como vistas.
            var newPriceChange = ProductPriceHistory.Create(
                TenantId, product.Id, oldCost: 10, newCost: 12, oldSell: 20, newSell: 24);

            priceHistory.Setup(p => p.GetRecentAsync(TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProductPriceHistory> { newPriceChange });

            var countAfter = await handler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);

            Assert.Equal(1, countAfter);
        }

        [Fact]
        public async Task UnreadCount_EditingUnrelatedProductField_ShouldNotReviveLowStockAlert()
        {
            // Bug encontrado al probar en vivo: editar cualquier campo del producto (ej. precio)
            // tocaba Product.UpdatedAt, y la notificación de stock bajo usaba esa misma fecha,
            // así que el aviso ya visto "revivía" como no visto sin que el stock cambiara.
            var product = Product.Create(TenantId, "Arroz", 10, 20, minStock: 5);

            var products = new Mock<IProductRepository>();
            products.Setup(p => p.GetByTenantAsync(TenantId, null, null, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Product> { product });

            var priceHistory = new Mock<IProductPriceHistoryRepository>();
            priceHistory.Setup(p => p.GetRecentAsync(TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProductPriceHistory>());

            await Task.Delay(5);
            var seenMarker = Setting.Create(TenantId, "ops.notifications.seenAt.x", DateTime.UtcNow.ToString("O"));

            var settings = new Mock<ISettingRepository>();
            settings.Setup(s => s.GetByKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(seenMarker);

            var handler = new GetNotificationsUnreadCountHandler(
                products.Object, priceHistory.Object, settings.Object, CurrentUserMock().Object);

            var countBeforeEdit = await handler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);
            Assert.Equal(0, countBeforeEdit);

            await Task.Delay(5);
            // Edición que no toca el stock: solo el precio de venta.
            product.Update(product.Name, product.CostPrice, sellPrice: 25, product.Barcode, product.Description,
                product.Unit, product.CategoryId, product.CostPriceUSD, product.SellPriceUSD, product.MinStock,
                product.TaxRate, product.BatchNumber, product.ExpirationDate, product.ForSale);

            var countAfterEdit = await handler.Handle(new GetNotificationsUnreadCountQuery(), CancellationToken.None);

            Assert.Equal(0, countAfterEdit);
        }
    }
}
