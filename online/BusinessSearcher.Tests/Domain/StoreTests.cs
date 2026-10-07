using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using Xunit;

namespace BusinessSearcher.Tests.Domain
{
    public class StoreTests
    {
        private static readonly Guid   TestTenantId = Guid.NewGuid();
        private static readonly Address TestAddress  = new("Calle 10 #5-20", "Bogotá", "Cundinamarca", "Colombia");
        private static readonly PhoneNumber TestPhone = new("+57 300 1234567");

        private static Store CreateValidStore() =>
            Store.Create(TestTenantId, "Empanadas Doña Rosa", TestAddress, TestPhone, "Las mejores empanadas");

        [Fact]
        public void Create_WithValidData_ShouldCreateStore()
        {
            var store = CreateValidStore();

            Assert.Equal("Empanadas Doña Rosa", store.Name);
            Assert.Equal(TestTenantId, store.TenantId);
            Assert.True(store.IsActive);
            Assert.Single(store.DomainEvents); // StoreCreatedEvent
        }

        [Fact]
        public void Create_WithoutTenantId_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() =>
                Store.Create(Guid.Empty, "Tienda", TestAddress, TestPhone));
        }

        [Fact]
        public void AddOrUpdateSchedule_ShouldAddSchedule()
        {
            var store    = CreateValidStore();
            var time     = ScheduleTime.Create("08:00", "18:00");
            var schedule = store.AddOrUpdateSchedule(DayOfWeek.Monday, time);

            Assert.Single(store.Schedules);
            Assert.Equal(DayOfWeek.Monday, schedule.DayOfWeek);
        }

        [Fact]
        public void AddOrUpdateSchedule_SameDay_ShouldUpdateExistingSchedule()
        {
            var store = CreateValidStore();
            var time1 = ScheduleTime.Create("08:00", "14:00");
            var time2 = ScheduleTime.Create("10:00", "22:00");

            store.AddOrUpdateSchedule(DayOfWeek.Monday, time1);
            store.AddOrUpdateSchedule(DayOfWeek.Monday, time2);

            Assert.Single(store.Schedules);
            Assert.Equal(time2.OpenTime, store.Schedules.First().Time.OpenTime);
        }

        [Fact]
        public void Deactivate_WhenActive_ShouldSetIsActiveFalse()
        {
            var store = CreateValidStore();
            store.Deactivate();

            Assert.False(store.IsActive);
        }
    }
}
