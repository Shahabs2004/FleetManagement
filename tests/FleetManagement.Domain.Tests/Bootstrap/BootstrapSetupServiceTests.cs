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
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class InMemoryBootstrapStateStore : IBootstrapStateStore
    {
        private readonly BootstrapState _state = new();

        public Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_state);

        public Task WriteAsync(BootstrapState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
