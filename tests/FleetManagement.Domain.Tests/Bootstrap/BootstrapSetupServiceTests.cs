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

    private sealed class InMemoryBootstrapStateStore : IBootstrapStateStore
    {
        public BootstrapState State { get; } = new();

        public Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task WriteAsync(BootstrapState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
