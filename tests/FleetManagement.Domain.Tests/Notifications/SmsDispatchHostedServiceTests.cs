using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Notifications;

public sealed class SmsDispatchHostedServiceTests
{
    [Fact]
    public void وضعیت_تحویل_نمونه_ساختگی_فقط_با_نگاشت_صریح_تأیید_می‌شود()
    {
        var fakeSampleMappings = new Dictionary<string, AdpDeliveryStatusKind>
        {
            ["FAKE-DELIVERED"] = AdpDeliveryStatusKind.Delivered
        };

        Assert.Equal(
            AdpDeliveryStatusKind.Delivered,
            AdpDeliveryStatusMapper.Map("fake-delivered", fakeSampleMappings));
        Assert.Equal(
            AdpDeliveryStatusKind.Unknown,
            AdpDeliveryStatusMapper.Map("FAKE-PENDING", fakeSampleMappings));
        Assert.Equal(
            "نامشخص",
            AdpDeliveryStatusMapper.GetPersianLabel("FAKE-PENDING", fakeSampleMappings));
    }

    [Fact]
    public void نگاشت_پیش‌فرض_تا_دریافت_نمونه_واقعی_خالی_است()
    {
        var configuration = SmsProviderConfiguration.CreateDefault();

        Assert.Empty(configuration.DeliveryStatusMappings);
        Assert.Equal(
            AdpDeliveryStatusKind.Unknown,
            AdpDeliveryStatusMapper.Map("Delivered", configuration.DeliveryStatusMappings));
    }
}
