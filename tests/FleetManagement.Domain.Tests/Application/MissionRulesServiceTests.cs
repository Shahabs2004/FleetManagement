using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>
/// آزمون سناریوهای کاربردی تنظیم قواعد برنامه‌ریزی مأموریت.
/// </summary>
public sealed class MissionRulesServiceTests
{
    [Fact]
    public async Task GetRulesAsync_WhenSettingDoesNotExist_ReturnsDefaultRules()
    {
        var settings = new InMemorySystemSettingsStore();
        var audit = new RecordingAuditLogWriter();
        var service = new MissionRulesService(settings, audit);

        var rules = await service.GetRulesAsync();

        Assert.Equal(0, rules.MinimumGapHours);
        Assert.Equal(90, rules.BookingHorizonDays);
    }

    [Fact]
    public async Task GetRulesAsync_WhenStoredRulesAreInvalid_ThrowsInvalidOperationException()
    {
        var settings = new InMemorySystemSettingsStore();
        await settings.SetAsync("Missions.Rules", new MissionRulesConfiguration
        {
            MinimumGapHours = 25,
            BookingHorizonDays = 90
        });
        var service = new MissionRulesService(settings, new RecordingAuditLogWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRulesAsync());
    }

    [Theory]
    [InlineData(-1, 90)]
    [InlineData(25, 90)]
    [InlineData(0, 0)]
    [InlineData(0, 731)]
    public async Task SaveRulesAsync_WhenValuesAreOutsideAllowedRange_ThrowsArgumentOutOfRangeException(
        int minimumGapHours,
        int bookingHorizonDays)
    {
        var service = new MissionRulesService(
            new InMemorySystemSettingsStore(),
            new RecordingAuditLogWriter());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveRulesAsync(
            new MissionRulesConfiguration
            {
                MinimumGapHours = minimumGapHours,
                BookingHorizonDays = bookingHorizonDays
            },
            "operator"));
    }

    [Fact]
    public async Task SaveRulesAsync_WhenRulesAreValid_PersistsAndAuditsTheChange()
    {
        var settings = new InMemorySystemSettingsStore();
        var audit = new RecordingAuditLogWriter();
        var service = new MissionRulesService(settings, audit);
        var rules = new MissionRulesConfiguration
        {
            MinimumGapHours = 2,
            BookingHorizonDays = 120
        };

        await service.SaveRulesAsync(rules, "operator");

        var saved = await settings.GetAsync<MissionRulesConfiguration>("Missions.Rules");
        Assert.NotNull(saved);
        Assert.Equal(2, saved.MinimumGapHours);
        Assert.Equal(120, saved.BookingHorizonDays);
        Assert.Equal("operator", audit.ActorUsername);
        Assert.Equal("UpdateRules", audit.Action);
        Assert.Equal("MissionRules", audit.EntityType);
        Assert.Equal("Success", audit.Outcome);
        Assert.Equal(rules, JsonSerializer.Deserialize<MissionRulesConfiguration>(audit.AfterJson!));
    }

    private sealed class InMemorySystemSettingsStore : ISystemSettingsStore
    {
        private readonly Dictionary<string, object> _values = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[key] = value!;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
    {
        public string? ActorUsername { get; private set; }
        public string? Action { get; private set; }
        public string? EntityType { get; private set; }
        public string? Outcome { get; private set; }
        public string? AfterJson { get; private set; }

        public Task<Guid> RecordAsync(
            string actorUsername,
            string action,
            string entityType,
            string? entityId,
            string outcome,
            string? reason = null,
            string? beforeJson = null,
            string? afterJson = null,
            string? clientIpAddress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActorUsername = actorUsername;
            Action = action;
            EntityType = entityType;
            Outcome = outcome;
            AfterJson = afterJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
