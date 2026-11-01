using FleetManagement.Infrastructure.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetManagement.Domain.Tests.Bootstrap;

public sealed class BootstrapSetupServiceTests
{
    [Fact]
    public async Task TechnicalAdministratorCode_IsAcceptedAndLockedAfterFiveFailedAttempts()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var stateStore = new InMemoryBootstrapStateStore();
            var service = new BootstrapSetupService(
                stateStore,
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Bootstrap:TechnicalAdministratorCodePath"] = Path.Combine(directory, "technical.txt"),
                        ["Bootstrap:SetupCodePath"] = Path.Combine(directory, "setup.txt")
                    })
                    .Build(),
                NullLogger<BootstrapSetupService>.Instance);

            await service.EnsureSetupCodeAsync();
            var code = await File.ReadAllTextAsync(Path.Combine(directory, "technical.txt"));

            Assert.True(await service.VerifyTechnicalAdministratorAccessAsync(code, "valid-code-test"));

            for (var attempt = 0; attempt < 5; attempt++)
            {
                Assert.False(await service.VerifyTechnicalAdministratorAccessAsync("invalid", "lockout-test"));
            }

            Assert.False(await service.VerifyTechnicalAdministratorAccessAsync(code, "lockout-test"));

            stateStore.State.LocalAdministratorUsername = "administrator";
            stateStore.State.LocalAdministratorPasswordHash = "hash";
            stateStore.State.LocalAdministratorPasswordSalt = "salt";
            stateStore.State.SqlConnectionString = "Server=localhost;Database=Fleet;";
            await service.CompleteSetupAsync();

            Assert.False(File.Exists(Path.Combine(directory, "technical.txt")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SetupCode_CannotCreateTwoAdministratorsConcurrently()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var stateStore = new SnapshottingBootstrapStateStore();
            var service = new BootstrapSetupService(
                stateStore,
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Bootstrap:TechnicalAdministratorCodePath"] = Path.Combine(directory, "technical.txt"),
                        ["Bootstrap:SetupCodePath"] = Path.Combine(directory, "setup.txt")
                    })
                    .Build(),
                NullLogger<BootstrapSetupService>.Instance);

            await service.EnsureSetupCodeAsync();
            var code = await File.ReadAllTextAsync(Path.Combine(directory, "setup.txt"));

            var results = await Task.WhenAll(
                TryCreateAdministratorAsync(service, code, "administrator-one"),
                TryCreateAdministratorAsync(service, code, "administrator-two"));

            Assert.Single(results, created => created);
            Assert.True(stateStore.State.HasLocalAdministrator);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<bool> TryCreateAdministratorAsync(
        BootstrapSetupService service,
        string code,
        string username)
    {
        try
        {
            await service.CreateLocalAdministratorAsync(
                code,
                username,
                "Strong-Local-Admin-Password-2026");
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class SnapshottingBootstrapStateStore : IBootstrapStateStore
    {
        private readonly object _sync = new();
        private BootstrapState _state = new();

        public BootstrapState State
        {
            get
            {
                lock (_sync)
                {
                    return Clone(_state);
                }
            }
        }

        public async Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default)
        {
            // دو درخواست را در وضعیت رقابتی قرار می‌دهد؛ هر خواندن snapshot مستقلی می‌گیرد.
            await Task.Delay(25, cancellationToken);
            lock (_sync)
            {
                return Clone(_state);
            }
        }

        public Task WriteAsync(BootstrapState state, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _state = Clone(state);
            }

            return Task.CompletedTask;
        }

        private static BootstrapState Clone(BootstrapState state) => new()
        {
            SchemaVersion = state.SchemaVersion,
            SetupCompleted = state.SetupCompleted,
            SetupCodeHash = state.SetupCodeHash,
            SetupCodeExpiresAtUtc = state.SetupCodeExpiresAtUtc,
            TechnicalAdministratorCodeHash = state.TechnicalAdministratorCodeHash,
            TechnicalAdministratorCodeExpiresAtUtc = state.TechnicalAdministratorCodeExpiresAtUtc,
            LocalAdministratorUsername = state.LocalAdministratorUsername,
            LocalAdministratorPasswordHash = state.LocalAdministratorPasswordHash,
            LocalAdministratorPasswordSalt = state.LocalAdministratorPasswordSalt,
            SqlConnectionString = state.SqlConnectionString
        };
    }

    private sealed class InMemoryBootstrapStateStore : IBootstrapStateStore
    {
        public BootstrapState State { get; } = new();

        public Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task WriteAsync(BootstrapState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
