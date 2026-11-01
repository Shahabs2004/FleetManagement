using FleetManagement.Domain.Auditing;

namespace FleetManagement.Domain.Tests.Auditing;

/// <summary>
/// آزمون قواعد اعتبارسنجی و ثبت تغییرناپذیر رویداد ممیزی.
/// </summary>
public sealed class AuditEntryTests
{
    [Fact]
    public void رویداد_ممیزی_اطلاعات_عامل_و_قبل_و_بعد_را_ثبت_می_کند()
    {
        // Arrange
        const string before = """{"status":"Pending"}""";
        const string after = """{"status":"Approved"}""";

        // Act
        var entry = new AuditEntry(
            "operator.one",
            "Approve",
            "Mission",
            Guid.NewGuid().ToString(),
            "Success",
            "بررسی مدارک انجام شد",
            before,
            after,
            "10.0.0.20");

        // Assert
        Assert.Equal("operator.one", entry.ActorUsername);
        Assert.Equal("Approve", entry.Action);
        Assert.Equal("Mission", entry.EntityType);
        Assert.Equal("Success", entry.Outcome);
        Assert.Equal(before, entry.BeforeJson);
        Assert.Equal(after, entry.AfterJson);
        Assert.Equal("10.0.0.20", entry.ClientIpAddress);
        Assert.NotEqual(default, entry.OccurredAtUtc);
    }

    [Fact]
    public void نام_کاربری_خالی_برای_ممیزی_پذیرفته_نمی_شود()
    {
        Assert.Throws<ArgumentException>(() => new AuditEntry(
            " ",
            "Create",
            "Driver",
            null,
            "Success",
            null,
            null,
            null,
            null));
    }

    [Fact]
    public void تصویر_بزرگتر_از_محدوده_مجاز_پذیرفته_نمی_شود()
    {
        var oversizedSnapshot = new string('x', 12001);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditEntry(
            "admin",
            "Update",
            "Vehicle",
            "vehicle-1",
            "Success",
            null,
            oversizedSnapshot,
            null,
            null));
    }

    [Fact]
    public void فیلدهای_اصلی_رویداد_فقط_خواندنی_هستند()
    {
        var property = typeof(AuditEntry).GetProperty(nameof(AuditEntry.Action));

        Assert.NotNull(property);
        Assert.False(property!.SetMethod?.IsPublic ?? false);
    }
}
