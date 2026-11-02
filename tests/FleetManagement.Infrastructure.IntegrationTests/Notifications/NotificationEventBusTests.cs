using FleetManagement.Application.Notifications;
using FleetManagement.Web.Notifications;

namespace FleetManagement.Infrastructure.IntegrationTests.Notifications;

public sealed class NotificationEventBusTests
{
    [Fact]
    public void Publish_DataChange_NotifiesAllLocalSubscribers()
    {
        var eventBus = new NotificationEventBus();
        FleetDataChange? received = null;
        eventBus.DataChanged += change => received = change;

        var expected = new FleetDataChange(
            FleetDataChangeKind.Drivers,
            DateTimeOffset.UtcNow);

        eventBus.Publish(expected);

        Assert.Equal(expected, received);
    }
}
