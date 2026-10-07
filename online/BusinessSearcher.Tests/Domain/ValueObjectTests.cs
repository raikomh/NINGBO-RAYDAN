using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using Xunit;

namespace BusinessSearcher.Tests.Domain
{
    public class MoneyTests
    {
        [Fact]
        public void Create_WithValidAmount_ShouldCreateMoney()
        {
            var money = new Money(25000m, "COP");
            Assert.Equal(25000m, money.Amount);
            Assert.Equal("COP", money.Currency);
        }

        [Fact]
        public void Create_WithNegativeAmount_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() => new Money(-100m, "COP"));
        }

        [Fact]
        public void Add_WithSameCurrency_ShouldReturnSum()
        {
            var m1  = new Money(100m, "COP");
            var m2  = new Money(50m, "COP");
            var sum = m1.Add(m2);

            Assert.Equal(150m, sum.Amount);
            Assert.Equal("COP", sum.Currency);
        }

        [Fact]
        public void Add_WithDifferentCurrency_ShouldThrowDomainException()
        {
            var m1 = new Money(100m, "COP");
            var m2 = new Money(50m, "USD");

            Assert.Throws<DomainException>(() => m1.Add(m2));
        }

        [Fact]
        public void Subtract_WithSameCurrency_ShouldReturnDifference()
        {
            var m1 = new Money(100m, "COP");
            var m2 = new Money(30m, "COP");
            var diff = m1.Subtract(m2);

            Assert.Equal(70m, diff.Amount);
        }

        [Fact]
        public void Subtract_ResultingNegative_ShouldThrowDomainException()
        {
            var m1 = new Money(30m, "COP");
            var m2 = new Money(100m, "COP");

            Assert.Throws<DomainException>(() => m1.Subtract(m2));
        }

        [Fact]
        public void Multiply_WithValidFactor_ShouldReturnProduct()
        {
            var m = new Money(100m, "COP");
            var result = m.Multiply(2.5m);

            Assert.Equal(250m, result.Amount);
            Assert.Equal("COP", result.Currency);
        }

        [Fact]
        public void Equality_WithSameValues_ShouldBeEqual()
        {
            var m1 = new Money(100m, "COP");
            var m2 = new Money(100m, "COP");

            Assert.Equal(m1, m2);
        }

        [Fact]
        public void Equality_WithDifferentValues_ShouldNotBeEqual()
        {
            var m1 = new Money(100m, "COP");
            var m2 = new Money(50m, "COP");

            Assert.NotEqual(m1, m2);
        }

        [Fact]
        public void Zero_ShouldCreateMoneyWithAmountZero()
        {
            var zero = Money.Zero("USD");
            Assert.Equal(0m, zero.Amount);
            Assert.True(zero.IsZero());
        }
    }

    public class AddressTests
    {
        [Fact]
        public void Create_WithValidData_ShouldCreateAddress()
        {
            var addr = new Address("Calle 10 #5-20", "Bogotá", "Cundinamarca", "Colombia", 4.7110, -74.0721);

            Assert.Equal("Calle 10 #5-20", addr.Street);
            Assert.Equal("Bogotá", addr.City);
            Assert.True(addr.HasCoordinates);
        }

        [Theory]
        [InlineData("", "Bogotá", "Cundinamarca", "Colombia")]
        [InlineData("Calle 10", "", "Cundinamarca", "Colombia")]
        [InlineData("Calle 10", "Bogotá", "", "Colombia")]
        [InlineData("Calle 10", "Bogotá", "Cundinamarca", "")]
        public void Create_WithMissingField_ShouldThrowDomainException(
            string street, string city, string state, string country)
        {
            Assert.Throws<DomainException>(() => new Address(street, city, state, country));
        }

        [Fact]
        public void Create_WithInvalidLatitude_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() =>
                new Address("Calle 10", "Bogotá", "Cundinamarca", "Colombia", 95, -74));
        }

        [Fact]
        public void Create_WithInvalidLongitude_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() =>
                new Address("Calle 10", "Bogotá", "Cundinamarca", "Colombia", 4.7, -185));
        }

        [Fact]
        public void Equality_WithSameData_ShouldBeEqual()
        {
            var a1 = new Address("Calle 10", "Bogotá", "Cundinamarca", "Colombia");
            var a2 = new Address("Calle 10", "Bogotá", "Cundinamarca", "Colombia");

            Assert.Equal(a1, a2);
        }
    }

    public class PhoneNumberTests
    {
        [Theory]
        [InlineData("+57 300 1234567")]
        [InlineData("+57(300)1234567")]
        [InlineData("300-1234567")]
        public void Create_WithValidFormat_ShouldCreatePhoneNumber(string phone)
        {
            var phoneNum = new PhoneNumber(phone);
            Assert.Equal(phone, phoneNum.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("abcdef")]
        public void Create_WithInvalidFormat_ShouldThrowDomainException(string phone)
        {
            Assert.Throws<DomainException>(() => new PhoneNumber(phone));
        }

        [Fact]
        public void Equality_WithSameValue_ShouldBeEqual()
        {
            var p1 = new PhoneNumber("+57 300 1234567");
            var p2 = new PhoneNumber("+57 300 1234567");

            Assert.Equal(p1, p2);
        }
    }

    public class ScheduleTimeTests
    {
        [Fact]
        public void Create_WithValidTimes_ShouldCreateSchedule()
        {
            var schedule = ScheduleTime.Create("08:00", "18:00");

            Assert.Equal(10 * 60, schedule.DurationInMinutes); // 10 horas = 600 minutos
        }

        [Fact]
        public void Create_WithCloseTimeBeforeOpenTime_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() => ScheduleTime.Create("18:00", "08:00"));
        }

        [Fact]
        public void Create_WithInvalidTimeFormat_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() => ScheduleTime.Create("8:00", "18:00"));
        }

        [Fact]
        public void IsOpenAt_WithTimeInRange_ShouldReturnTrue()
        {
            var schedule = ScheduleTime.Create("08:00", "18:00");
            var time = new TimeOnly(12, 0); // 12:00 PM

            Assert.True(schedule.IsOpenAt(time));
        }

        [Fact]
        public void IsOpenAt_WithTimeOutOfRange_ShouldReturnFalse()
        {
            var schedule = ScheduleTime.Create("08:00", "18:00");
            var time = new TimeOnly(22, 0); // 10:00 PM

            Assert.False(schedule.IsOpenAt(time));
        }
    }
}
