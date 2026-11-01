using FleetManagement.Domain.Notifications;

namespace FleetManagement.Domain.Tests.Notifications;

/// <summary>
/// آزمون قواعد صف پیامک، تلاش مجدد دستی و لغو یادآوری.
/// </summary>
public sealed class SmsQueueItemTests
{
    [Fact]
    public void تلاش_مجدد_دستی_به_علت_اصلاح_خطا_نیاز_دارد()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(SmsEventType.MissionApproved, maximumRetryCount: 0);
        item.MarkSending(now);
        item.MarkTransientFailure("خطای ارتباط", now);

        // Act
        item.RetryManually(now.AddMinutes(1), "تنظیم اتصال اصلاح شد.");

        // Assert
        Assert.Equal(SmsQueueStatus.Queued, item.Status);
        Assert.Equal(now.AddMinutes(1), item.NextAttemptAtUtc);
        Assert.Null(item.ErrorReason);
    }

    [Fact]
    public void تلاش_مجدد_بدون_علت_اصلاح_پذیرفته_نمی_شود()
    {
        var item = CreateItem(SmsEventType.MissionApproved, maximumRetryCount: 0);
        var now = DateTimeOffset.UtcNow;
        item.MarkSending(now);
        item.MarkTransientFailure("خطای دائمی", now);

        Assert.Throws<ArgumentException>(() => item.RetryManually(now, " "));
        Assert.Equal(SmsQueueStatus.Failed, item.Status);
    }

    [Fact]
    public void پیام_تحویل_شده_نمی_تواند_به_خطای_دائمی_تبدیل_شود()
    {
        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(SmsEventType.MissionApproved);
        item.MarkSending(now);
        item.MarkAccepted("12345", now, 30);
        item.RecordDeliveryStatus("Delivered", true, now.AddSeconds(30), 30);

        Assert.Throws<InvalidOperationException>(
            () => item.MarkPermanentFailure("خطای دیرهنگام", now.AddMinutes(1)));
        Assert.Equal(SmsQueueStatus.Delivered, item.Status);
    }

    [Fact]
    public void یادآوری_ارسال_نشده_قابل_لغو_است()
    {
        var item = CreateItem(SmsEventType.MissionStartReminder);

        item.CancelScheduledReminder("زمان مأموریت تغییر کرد.");

        Assert.Equal(SmsQueueStatus.Cancelled, item.Status);
        Assert.Equal("زمان مأموریت تغییر کرد.", item.ErrorReason);
        Assert.Null(item.NextAttemptAtUtc);
    }

    [Fact]
    public void پیام_غیر_یادآوری_قابل_لغو_از_این_مسیر_نیست()
    {
        var item = CreateItem(SmsEventType.MissionApproved);

        Assert.Throws<InvalidOperationException>(
            () => item.CancelScheduledReminder("لغو"));
    }

    private static SmsQueueItem CreateItem(
        SmsEventType eventType,
        int maximumRetryCount = 3)
    {
        return new SmsQueueItem(
            eventType,
            "کاربر آزمون",
            "989121234567",
            "متن پیام آزمون",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(1),
            maximumRetryCount,
            30,
            3);
    }
}
