using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;

namespace BusinessSearcher.Tests.Helpers
{
    public class TenantBuilder
    {
        private string _businessName = "Test Business";
        private string _email = "test@test.com";
        private string _passwordHash = "hashed_password";
        private TenantType _type = TenantType.Retail;

        public TenantBuilder WithBusinessName(string name)
        {
            _businessName = name;
            return this;
        }

        public TenantBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public TenantBuilder WithPasswordHash(string hash)
        {
            _passwordHash = hash;
            return this;
        }

        public TenantBuilder WithType(TenantType type)
        {
            _type = type;
            return this;
        }

        public Tenant Build() => Tenant.Create(_businessName, _email, _passwordHash, _type);
    }

    public class StoreBuilder
    {
        private Guid _tenantId = Guid.NewGuid();
        private string _name = "Test Store";
        private Address _address = new("Test Street", "Test City", "Test State", "Test Country");
        private PhoneNumber _phone = new("+57 300 1234567");
        private string _description = "Test Description";
        private string? _logoUrl = null;

        public StoreBuilder WithTenantId(Guid tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public StoreBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public StoreBuilder WithAddress(Address address)
        {
            _address = address;
            return this;
        }

        public StoreBuilder WithPhone(PhoneNumber phone)
        {
            _phone = phone;
            return this;
        }

        public StoreBuilder WithDescription(string desc)
        {
            _description = desc;
            return this;
        }

        public StoreBuilder WithLogoUrl(string? url)
        {
            _logoUrl = url;
            return this;
        }

        public Store Build() => Store.Create(_tenantId, _name, _address, _phone, _description, _logoUrl);
    }

    public class AddressBuilder
    {
        private string _street = "Test Street";
        private string _city = "Test City";
        private string _state = "Test State";
        private string _country = "Test Country";
        private double? _latitude = null;
        private double? _longitude = null;

        public AddressBuilder WithStreet(string street)
        {
            _street = street;
            return this;
        }

        public AddressBuilder WithCity(string city)
        {
            _city = city;
            return this;
        }

        public AddressBuilder WithState(string state)
        {
            _state = state;
            return this;
        }

        public AddressBuilder WithCountry(string country)
        {
            _country = country;
            return this;
        }

        public AddressBuilder WithCoordinates(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
            return this;
        }

        public Address Build() => new(_street, _city, _state, _country, _latitude, _longitude);
    }

    public class MoneyBuilder
    {
        private decimal _amount = 100m;
        private string _currency = "COP";

        public MoneyBuilder WithAmount(decimal amount)
        {
            _amount = amount;
            return this;
        }

        public MoneyBuilder WithCurrency(string currency)
        {
            _currency = currency;
            return this;
        }

        public Money Build() => new(_amount, _currency);
    }

    public class ScheduleTimeBuilder
    {
        private string _openTime = "08:00";
        private string _closeTime = "18:00";

        public ScheduleTimeBuilder WithOpenTime(string time)
        {
            _openTime = time;
            return this;
        }

        public ScheduleTimeBuilder WithCloseTime(string time)
        {
            _closeTime = time;
            return this;
        }

        public ScheduleTime Build() => ScheduleTime.Create(_openTime, _closeTime);
    }
}
