using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت تنظیمات اتصال و قالب‌های پیامکی در SQL.
/// </summary>
public sealed class SmsSettingsService(ISystemSettingsStore settingsStore)
{
    private const string SettingKey = "Notifications.Sms.ADP";
    private const string SkippedSettingKey = "Notifications.Sms.SetupSkipped";

    /// <summary>خواندن تنظیمات سرویس پیامک.</summary>
    public Task<SmsProviderConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetAsync<SmsProviderConfiguration>(
            SettingKey,
            cancellationToken);
    }

    /// <summary>بررسی عبور مدیر فنی از مرحله پیامک در ویزارد.</summary>
    public async Task<bool> IsSkippedAsync(CancellationToken cancellationToken = default)
    {
        return await settingsStore.GetAsync<bool>(SkippedSettingKey, cancellationToken);
    }

    /// <summary>ثبت عبور از مرحله پیامک بدون فعال‌سازی سرویس.</summary>
    public Task MarkSkippedAsync(CancellationToken cancellationToken = default)
    {
        return settingsStore.SetAsync(SkippedSettingKey, true, cancellationToken);
    }

    /// <summary>اعتبارسنجی و ذخیره تنظیمات سرویس پیامک.</summary>
    public async Task SaveAsync(
        SmsProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.SourceNumber);

        if (configuration.SourceNumber.Length > 20)
        {
            throw new ArgumentException("شماره فرستنده معتبر نیست.", nameof(configuration));
        }

        if (configuration.EventTemplates.Count == 0)
        {
            configuration.EventTemplates = SmsProviderConfiguration.CreateDefault().EventTemplates;
        }

        if (configuration.EventTemplates.Any(template =>
                !Enum.IsDefined(template.EventType)
                || !Enum.IsDefined(template.RecipientRule)
                || (template.IsEnabled && string.IsNullOrWhiteSpace(template.Template))))
        {
            throw new ArgumentException("تنظیم یکی از قالب‌های پیامکی معتبر نیست.", nameof(configuration));
        }

        if (configuration.MaximumRetryCount is < 0 or > 20
            || configuration.RetryIntervalSeconds is < 5 or > 86400
            || configuration.StatusPollingIntervalSeconds is < 5 or > 86400
            || configuration.MaximumStatusPollingAttempts is < 0 or > 100
            || configuration.ReminderLeadMinutes is < 0 or > 10080)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "یکی از مقادیر زمان‌بندی یا تلاش مجدد خارج از محدوده مجاز است.");
        }

        if (configuration.EventTemplates
            .GroupBy(template => template.EventType)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("برای هر رویداد فقط یک قالب پیامک تعریف شود.");
        }

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);

        await settingsStore.SetAsync(
            SkippedSettingKey,
            false,
            cancellationToken);
    }
}
