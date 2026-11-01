using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// ذخیره و بازیابی چیدمان شخصی داشبورد با ثبت تغییرات در ممیزی.
/// </summary>
public sealed class DashboardLayoutSettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
{
    private static readonly HashSet<string> AllowedCards =
    [
        "pending",
        "confirmed",
        "running",
        "completed",
        "cancelled",
        "today",
        "activeDrivers",
        "readyVehicles"
    ];

    /// <summary>دریافت چیدمان شخصی کاربر یا ترتیب پیش‌فرض.</summary>
    public async Task<DashboardLayoutConfiguration> GetAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var configuration = await settingsStore.GetAsync<DashboardLayoutConfiguration>(
            GetSettingKey(username),
            cancellationToken) ?? new DashboardLayoutConfiguration();

        configuration.CardOrder = configuration.CardOrder
            .Where(AllowedCards.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        configuration.HiddenCards = configuration.HiddenCards
            .Where(AllowedCards.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return configuration;
    }

    /// <summary>ذخیره ترتیب و کارت‌های پنهان‌شده توسط کاربر.</summary>
    public async Task SaveAsync(
        string username,
        DashboardLayoutConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.CardOrder.Any(card => !AllowedCards.Contains(card))
            || configuration.HiddenCards.Any(card => !AllowedCards.Contains(card)))
        {
            throw new ArgumentException("چیدمان شامل شناسه کارت نامعتبر است.", nameof(configuration));
        }

        var before = await GetAsync(username, cancellationToken);
        await settingsStore.SetAsync(GetSettingKey(username), configuration, cancellationToken);

        await auditLog.RecordAsync(
            username,
            "UpdateDashboardLayout",
            "UserDashboard",
            username,
            "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(configuration),
            cancellationToken: cancellationToken);
    }

    /// <summary>ساخت کلید مجزای تنظیمات برای هر حساب.</summary>
    private static string GetSettingKey(string username) =>
        $"Dashboard.Layout.{username.Trim().ToLowerInvariant()}";
}
