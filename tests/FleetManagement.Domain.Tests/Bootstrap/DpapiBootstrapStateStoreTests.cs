using System.Text;
using FleetManagement.Infrastructure.Bootstrap;
using Microsoft.Extensions.Configuration;

namespace FleetManagement.Domain.Tests.Bootstrap;

public sealed class DpapiBootstrapStateStoreTests
{
    [Fact]
    public async Task ProtectedState_RoundTripsWithoutPlaintextOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dat");
        const string secret = "technical-bootstrap-secret";

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Bootstrap:FilePath"] = path
                })
                .Build();
            var store = new DpapiBootstrapStateStore(configuration);

            await store.WriteAsync(new BootstrapState { SetupCodeHash = secret });

            var fileText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));
            Assert.DoesNotContain(secret, fileText);
            Assert.Equal(secret, (await store.ReadAsync()).SetupCodeHash);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
