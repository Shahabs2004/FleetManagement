using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره الگوی چاپ دستور مأموریت در تنظیمات SQL.
/// </summary>
public sealed class MissionOrderTemplateService(ISystemSettingsStore settingsStore)
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
        Validate(template);

        await settingsStore.SetAsync(SettingKey, template, cancellationToken);
    }

    private static void Validate(MissionOrderTemplateConfiguration template)
    {
        ValidateText(template.HeaderTitle, 100, nameof(template.HeaderTitle));
        ValidateText(template.OrganizationName, 200, nameof(template.OrganizationName), allowEmpty: true);
        ValidateText(template.IntroText, 1000, nameof(template.IntroText), allowEmpty: true);
        ValidateText(template.FooterText, 1000, nameof(template.FooterText), allowEmpty: true);
        ValidateText(template.RequesterSignatureLabel, 100, nameof(template.RequesterSignatureLabel));
        ValidateText(template.OperatorSignatureLabel, 100, nameof(template.OperatorSignatureLabel));
        ValidateText(template.ApproverSignatureLabel, 100, nameof(template.ApproverSignatureLabel));
        ValidateText(template.DriverSignatureLabel, 100, nameof(template.DriverSignatureLabel));
    }

    private static void ValidateText(string? value, int maxLength, string parameterName, bool allowEmpty = false)
    {
        if (!allowEmpty) ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value?.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
    }
}
