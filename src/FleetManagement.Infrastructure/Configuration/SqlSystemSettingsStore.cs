using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Domain.Configuration;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ذخیره‌ساز تنظیمات مبتنی بر جدول SystemSettings.
/// مقدارها بدون رمزنگاری در SQL ذخیره می‌شوند؛ دسترسی به جدول باید محدود باشد.
/// </summary>
public sealed class SqlSystemSettingsStore(
    IDbContextFactory<FleetDbContext> dbContextFactory) : ISystemSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var settingValue = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (settingValue is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(settingValue, JsonOptions);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var serializedValue = JsonSerializer.Serialize(value, JsonOptions);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var setting = await dbContext.SystemSettings
            .SingleOrDefaultAsync(item => item.Key == key, cancellationToken);

        if (setting is null)
        {
            dbContext.SystemSettings.Add(new SystemSetting(key, serializedValue));
        }
        else
        {
            setting.Update(key, serializedValue);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
