using System.Text.Json;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره الگوی چاپ دستور مأموریت در تنظیمات SQL.
/// </summary>
public sealed class MissionOrderTemplateService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
{
    private const string SettingKey = "Missions.OrderTemplate";

    /// <summary>دریافت الگوی جاری چاپ.</summary>
    public async Task<MissionOrderTemplateConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        return await settingsStore.GetAsync<MissionOrderTemplateConfiguration>(SettingKey, cancellationToken)
            ?? MissionOrderTemplateConfiguration.CreateDefault();
    }

    /// <summary>اعتبارسنجی و ذخیره الگوی چاپ توسط مدیر سامانه.</summary>
    public async Task SaveAsync(
        MissionOrderTemplateConfiguration template,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        template.Validate();

        var before = await GetAsync(cancellationToken);
        await settingsStore.SetAsync(SettingKey, template, cancellationToken);
        await auditLog.RecordAsync(
            actor,
            "UpdateTemplate",
            "MissionOrderTemplate",
            null,
            "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(template),
            cancellationToken: cancellationToken);
    }


}
