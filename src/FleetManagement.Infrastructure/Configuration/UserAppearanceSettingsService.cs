using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ذخیره حالت رنگی شخصی هر کاربر.
/// </summary>
public sealed class UserAppearanceSettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
{
    /// <summary>دریافت حالت رنگی کاربر؛ روشن حالت پیش‌فرض است.</summary>
    public async Task<UserAppearanceConfiguration> GetAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var value = await settingsStore.GetAsync<UserAppearanceConfiguration>(
            GetKey(username),
            cancellationToken) ?? new UserAppearanceConfiguration();

        if (value.Theme is not ("Light" or "Dark"))
        {
            value.Theme = "Light";
        }

        return value;
    }

    /// <summary>ذخیره حالت رنگی و ثبت تغییر در ممیزی.</summary>
    public async Task SaveAsync(
        string username,
        string theme,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (theme is not ("Light" or "Dark"))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        var previous = await GetAsync(username, cancellationToken);
        var updated = new UserAppearanceConfiguration { Theme = theme };

        await settingsStore.SetAsync(GetKey(username), updated, cancellationToken);
        await auditLog.RecordAsync(
            username,
            "UpdateAppearance",
            "UserPreference",
            username,
            "Success",
            beforeJson: System.Text.Json.JsonSerializer.Serialize(previous),
            afterJson: System.Text.Json.JsonSerializer.Serialize(updated),
            cancellationToken: cancellationToken);
    }

    /// <summary>ساخت کلید تنظیم شخصی.</summary>
    private static string GetKey(string username) =>
        $"Appearance.Theme.{username.Trim().ToLowerInvariant()}";
}
