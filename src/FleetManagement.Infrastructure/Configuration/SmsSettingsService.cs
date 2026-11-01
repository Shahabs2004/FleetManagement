using FleetManagement.Domain.Notifications;
using System.Text.Json;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت تنظیمات اتصال و قالب‌های پیامکی در SQL.
/// </summary>
public sealed class SmsSettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
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

    /// <summary>
    /// بررسی وجود تنظیمات بدون خواندن یا deserialize کردن اطلاعات محرمانه اتصال.
    /// </summary>
    public async Task<bool> HasConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await settingsStore.GetAsync<SmsTemplateSettingsSnapshot>(
            SettingKey,
            cancellationToken);
        return snapshot is not null;
    }

    /// <summary>
    /// دریافت قالب‌ها بدون بارگذاری نام کاربری، رمز یا شماره فرستنده سرویس.
    /// </summary>
    public async Task<IReadOnlyList<SmsEventTemplateConfiguration>?> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await settingsStore.GetAsync<SmsTemplateSettingsSnapshot>(
            SettingKey,
            cancellationToken);
        return snapshot?.EventTemplates;
    }

    /// <summary>بررسی عبور مدیر فنی از مرحله پیامک در ویزارد.</summary>
    public async Task<bool> IsSkippedAsync(CancellationToken cancellationToken = default)
    {
        return await settingsStore.GetAsync<bool>(SkippedSettingKey, cancellationToken);
    }

    /// <summary>ثبت عبور از مرحله پیامک بدون فعال‌سازی سرویس.</summary>
    public async Task MarkSkippedAsync(CancellationToken cancellationToken = default)
    {
        await settingsStore.SetAsync(SkippedSettingKey, true, cancellationToken);
        await auditLog.RecordAsync(
            "TechnicalAdministrator",
            "SkipSmsSetup",
            "SmsConfiguration",
            null,
            "Success",
            reason: "مرحله تنظیم پیامک در ویزارد رد شد.",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// به‌روزرسانی قالب‌ها و گیرندگان توسط مدیر سامانه، بدون نمایش یا تغییر اطلاعات اتصال.
    /// </summary>
    public async Task UpdateTemplatesAsync(
        IReadOnlyCollection<SmsEventTemplateConfiguration> templates,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);

        var configuration = await GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("تنظیمات اتصال پیامک هنوز توسط مدیر فنی ثبت نشده است.");

        if (templates.Count != Enum.GetValues<SmsEventType>().Length
            || templates.GroupBy(template => template.EventType).Any(group => group.Count() != 1)
            || templates.Any(template =>
                !Enum.IsDefined(template.EventType)
                || !Enum.IsDefined(template.RecipientRule)
                || template.Template.Length > 2000
                || (template.IsEnabled && string.IsNullOrWhiteSpace(template.Template))))
        {
            throw new ArgumentException("قالب‌ها باید برای هر رویداد دقیقاً یک‌بار و با متن معتبر تعریف شوند.", nameof(templates));
        }

        var before = JsonSerializer.Serialize(configuration.EventTemplates);
        configuration.EventTemplates = templates
            .Select(template => new SmsEventTemplateConfiguration
            {
                EventType = template.EventType,
                IsEnabled = template.IsEnabled,
                RecipientRule = template.RecipientRule,
                Template = template.Template.Trim()
            })
            .ToList();

        await settingsStore.SetAsync(SettingKey, configuration, cancellationToken);
        await auditLog.RecordAsync(
            actorUsername,
            "UpdateSmsTemplates",
            "SmsConfiguration",
            null,
            "Success",
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(configuration.EventTemplates),
            cancellationToken: cancellationToken);
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

        var previous = await GetAsync(cancellationToken);

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);

        await settingsStore.SetAsync(
            SkippedSettingKey,
            false,
            cancellationToken);

        // هیچ رمز، نام کاربری یا محتوای محرمانه در سوابق ممیزی ذخیره نمی‌شود.
        static object? SafeSummary(SmsProviderConfiguration? value)
        {
            return value is null
                ? null
                : new
                {
                    value.Organization,
                    value.SourceNumber,
                    value.MaximumRetryCount,
                    value.RetryIntervalSeconds,
                    value.StatusPollingIntervalSeconds,
                    value.MaximumStatusPollingAttempts,
                    value.ReminderLeadMinutes,
                    EventTemplates = value.EventTemplates.Select(template => new
                    {
                        template.EventType,
                        template.IsEnabled,
                        template.RecipientRule,
                        template.Template
                    }).ToArray(),
                    CredentialsConfigured = !string.IsNullOrWhiteSpace(value.Password)
                };
        }

        await auditLog.RecordAsync(
            "TechnicalAdministrator",
            "UpdateSmsSettings",
            "SmsConfiguration",
            null,
            "Success",
            beforeJson: JsonSerializer.Serialize(SafeSummary(previous)),
            afterJson: JsonSerializer.Serialize(SafeSummary(configuration)),
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// شکل محدودشده برای خواندن بخش غیرمحرمانه تنظیمات پیامک.
    /// سایر فیلدهای JSON، از جمله اعتبارنامه‌ها، deserialize نمی‌شوند.
    /// </summary>
    public sealed class SmsTemplateSettingsSnapshot
    {
        public List<SmsEventTemplateConfiguration>? EventTemplates { get; set; }
    }

}
