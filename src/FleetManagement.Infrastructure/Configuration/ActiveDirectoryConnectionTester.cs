using System.DirectoryServices.Protocols;
using System.Net;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// آزمون اتصال و اعتبار حساب سرویس Active Directory.
/// </summary>
public sealed class ActiveDirectoryConnectionTester
{
    /// <summary>
    /// اتصال به AD با تنظیمات ارائه‌شده و بررسی دسترسی حساب سرویس.
    /// </summary>
    public Task TestAsync(
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(configuration.LdapServer)
                || string.IsNullOrWhiteSpace(configuration.ServiceAccountUsername)
                || string.IsNullOrWhiteSpace(configuration.ServiceAccountPassword)
                || string.IsNullOrWhiteSpace(configuration.BaseDistinguishedName))
            {
                throw new ArgumentException("اطلاعات اتصال Active Directory کامل نیست.");
            }

            var identifier = new LdapDirectoryIdentifier(
                configuration.LdapServer,
                configuration.Port);

            using var connection = new LdapConnection(identifier)
            {
                AuthType = AuthType.Basic,
                Credential = new NetworkCredential(
                    configuration.ServiceAccountUsername,
                    configuration.ServiceAccountPassword,
                    configuration.DomainDnsName),
                Timeout = TimeSpan.FromSeconds(12)
            };

            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = configuration.UseLdaps;
            connection.Bind();

            cancellationToken.ThrowIfCancellationRequested();

            // جست‌وجوی پایه فقط برای اطمینان از دسترسی حساب سرویس به Base DN انجام می‌شود.
            var request = new SearchRequest(
                configuration.BaseDistinguishedName,
                "(objectClass=*)",
                SearchScope.Base,
                "distinguishedName");

            var response = (SearchResponse)connection.SendRequest(request);

            if (response.Entries.Count == 0)
            {
                throw new InvalidOperationException(
                    "اتصال برقرار شد، اما Base DN در Active Directory پیدا نشد.");
            }
        }, cancellationToken);
    }
}
