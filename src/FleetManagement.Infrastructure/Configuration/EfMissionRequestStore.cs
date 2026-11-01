using System.Data;
using System.Globalization;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// پیاده‌سازی ثبت درخواست مأموریت با شماره‌گذاری جلالی و تراکنش Serializable.
/// </summary>
public sealed class EfMissionRequestStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionRequestStore
{
    /// <inheritdoc />
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var result = await operation(new EfUnitOfWork(db), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionRequestUnitOfWork
    {
        public async Task<string> GenerateMissionNumberAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            TimeZoneInfo tehranZone;
            try
            {
                tehranZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tehranZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            }

            var localNow = TimeZoneInfo.ConvertTime(nowUtc, tehranZone);
            var jalaliYear = new PersianCalendar().GetYear(localNow.DateTime);
            var prefix = $"{jalaliYear}-";
            var numbers = await db.Missions
                .Where(mission => mission.MissionNumber.StartsWith(prefix))
                .Select(mission => mission.MissionNumber)
                .ToListAsync(cancellationToken);

            var lastNumber = numbers
                .Select(number => int.TryParse(number.AsSpan(prefix.Length), out var value) ? value : 0)
                .DefaultIfEmpty(0)
                .Max();

            return $"{jalaliYear}-{lastNumber + 1:000}";
        }

        public void Add(Mission mission) => db.Missions.Add(mission);
    }
}
