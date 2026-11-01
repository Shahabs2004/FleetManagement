using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Notifications;

public sealed class SmsDispatchHostedServiceTests
{
    [Theory]
    [InlineData("Delivered", true)]
    [InlineData("delivrd", true)]
    [InlineData("Pending", false)]
    [InlineData("1", false)]
    public void Delivery_status_is_only_confirmed_for_known_success_values(string status, bool expected)
    {
        Assert.Equal(expected, SmsDispatchHostedService.IsDeliveredStatus(status));
    }
}
