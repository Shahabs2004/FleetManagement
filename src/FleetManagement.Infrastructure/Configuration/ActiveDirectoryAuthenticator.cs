using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// احراز هویت کاربران و کنترل وضعیت حساب و عضویت در گروه‌های مجاز AD.
/// </summary>
public sealed class ActiveDirectoryAuthenticator(
    ActiveDirectorySettingsService settingsService)
{
    private const string NestedGroupMatchRule = "1.2.840.113556.1.4.1941";
    private const int DisabledAccountFlag = 0x0002;
    private const int LockedAccountFlag = 0x0010;
    private const long NeverExpiresFileTime = 9_223_372_036_854_775_807;

    /// <summary>
    /// ورود جایگزین با نام کاربری و رمز عبور دامنه.
    /// </summary>
    public async Task<DirectoryAuthenticationResult> AuthenticateWithPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var configuration = await settingsService.GetAsync(cancellationToken);

        if (configuration is null || !configuration.IsConnectionVerified)
        {
            return Result(DirectoryAuthenticationStatus.NotConfigured);
        }

        if (!configuration.AllowUsernamePasswordFallback)
        {
            return Result(DirectoryAuthenticationStatus.AuthenticationMethodDisabled);
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return Result(DirectoryAuthenticationStatus.InvalidCredentials);
        }

        return await Task.Run(
            () => AuthenticateCore(
                configuration,
                username,
                password,
                verifyUserPassword: true,
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// اعتبارسنجی حسابی که قبلاً توسط Windows Authentication در IIS احراز شده است.
    /// </summary>
    public async Task<DirectoryAuthenticationResult> AuthenticateWindowsIdentityAsync(
        string windowsIdentityName,
        CancellationToken cancellationToken = default)
    {
        var configuration = await settingsService.GetAsync(cancellationToken);

        if (configuration is null || !configuration.IsConnectionVerified)
        {
            return Result(DirectoryAuthenticationStatus.NotConfigured);
        }

        if (!configuration.UseWindowsIntegratedAuthentication)
        {
            return Result(DirectoryAuthenticationStatus.AuthenticationMethodDisabled);
        }

        if (string.IsNullOrWhiteSpace(windowsIdentityName))
        {
            return Result(DirectoryAuthenticationStatus.InvalidCredentials);
        }

        return await Task.Run(
            () => AuthenticateCore(
                configuration,
                windowsIdentityName,
                suppliedPassword: null,
                verifyUserPassword: false,
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// جست‌وجوی کاربر، بررسی وضعیت حساب، عضویت گروه و در صورت نیاز تأیید رمز.
    /// </summary>
    private static DirectoryAuthenticationResult AuthenticateCore(
        ActiveDirectoryConfiguration configuration,
        string suppliedUsername,
        string? suppliedPassword,
        bool verifyUserPassword,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = NormalizeUsername(suppliedUsername);
            using var serviceConnection = CreateConnection(
                configuration,
                configuration.ServiceAccountUsername,
                configuration.ServiceAccountPassword);

            serviceConnection.Bind();

            var escapedUsername = EscapeFilterValue(username);
            var userFilter =
                "(&(objectCategory=person)(objectClass=user)(|(sAMAccountName="
                + escapedUsername
                + ")(userPrincipalName="
                + escapedUsername
                + ")))";

            var userRequest = new SearchRequest(
                configuration.BaseDistinguishedName,
                userFilter,
                SearchScope.Subtree,
                "sAMAccountName",
                "givenName",
                "sn",
                "displayName",
                "distinguishedName",
                "userAccountControl",
                "msDS-User-Account-Control-Computed",
                "accountExpires",
                configuration.OrganizationalUnitAttribute);

            var userResponse = (SearchResponse)serviceConnection.SendRequest(userRequest);

            if (userResponse.Entries.Count != 1)
            {
                return Result(DirectoryAuthenticationStatus.InvalidCredentials);
            }

            var userEntry = userResponse.Entries[0];
            var accountControl = ReadInteger(userEntry, "userAccountControl");
            var computedControl = ReadInteger(userEntry, "msDS-User-Account-Control-Computed");

            if ((accountControl & DisabledAccountFlag) != 0)
            {
                return Result(DirectoryAuthenticationStatus.AccountDisabled);
            }

            if ((computedControl & LockedAccountFlag) != 0)
            {
                return Result(DirectoryAuthenticationStatus.AccountLocked);
            }

            if (IsAccountExpired(userEntry))
            {
                return Result(DirectoryAuthenticationStatus.AccountExpired);
            }

            var userDn = ReadString(userEntry, "distinguishedName");
            var matchedGroups = FindAllowedGroups(
                serviceConnection,
                configuration.AllowedGroups,
                userDn,
                cancellationToken);

            if (matchedGroups.Count == 0)
            {
                return Result(DirectoryAuthenticationStatus.NotInAllowedGroup);
            }

            if (verifyUserPassword)
            {
                try
                {
                    // استفاده از DN دقیق کاربر مانع ابهام در قالب نام کاربری و دامنه می‌شود.
                    using var userConnection = CreateConnection(
                        configuration,
                        userDn,
                        suppliedPassword ?? string.Empty,
                        useDomain: false);

                    userConnection.Bind();
                }
                catch (LdapException exception) when (exception.ErrorCode == 49)
                {
                    return Result(DirectoryAuthenticationStatus.InvalidCredentials);
                }
            }

            var firstName = ReadString(userEntry, "givenName");
            var lastName = ReadString(userEntry, "sn");
            var displayName = ReadString(userEntry, "displayName");

            var profile = new DirectoryUserProfile
            {
                Username = ReadString(userEntry, "sAMAccountName"),
                FirstName = firstName,
                LastName = lastName,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? $"{firstName} {lastName}".Trim()
                    : displayName,
                OrganizationalUnit = ReadString(
                    userEntry,
                    configuration.OrganizationalUnitAttribute),
                DistinguishedName = userDn,
                AllowedGroups = matchedGroups
            };

            return new DirectoryAuthenticationResult
            {
                Status = DirectoryAuthenticationStatus.Success,
                User = profile
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException)
        {
            return Result(DirectoryAuthenticationStatus.DirectoryUnavailable);
        }
        catch (Exception)
        {
            return Result(DirectoryAuthenticationStatus.DirectoryUnavailable);
        }
    }

    /// <summary>
    /// ایجاد اتصال LDAP با حساب سرویس یا کاربر.
    /// </summary>
    private static LdapConnection CreateConnection(
        ActiveDirectoryConfiguration configuration,
        string username,
        string password,
        bool useDomain = true)
    {
        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(configuration.LdapServer, configuration.Port))
        {
            AuthType = AuthType.Basic,
            Credential = useDomain
                ? new NetworkCredential(username, password, configuration.DomainDnsName)
                : new NetworkCredential(username, password),
            Timeout = TimeSpan.FromSeconds(12)
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = configuration.UseLdaps;

        return connection;
    }

    /// <summary>
    /// یافتن گروه‌هایی که کاربر به‌صورت مستقیم یا تو‌در‌تو عضو آن‌هاست.
    /// </summary>
    private static List<ActiveDirectoryGroup> FindAllowedGroups(
        LdapConnection connection,
        IEnumerable<ActiveDirectoryGroup> allowedGroups,
        string userDistinguishedName,
        CancellationToken cancellationToken)
    {
        var matches = new List<ActiveDirectoryGroup>();
        var escapedUserDn = EscapeFilterValue(userDistinguishedName);

        foreach (var group in allowedGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filter =
                "(&(objectClass=group)(member:"
                + NestedGroupMatchRule
                + ":="
                + escapedUserDn
                + "))";

            var request = new SearchRequest(
                group.DistinguishedName,
                filter,
                SearchScope.Base,
                "distinguishedName");

            var response = (SearchResponse)connection.SendRequest(request);

            if (response.Entries.Count > 0)
            {
                matches.Add(group);
            }
        }

        return matches;
    }

    /// <summary>
    /// بررسی تاریخ انقضای حساب بر اساس ویژگی accountExpires.
    /// </summary>
    private static bool IsAccountExpired(SearchResultEntry entry)
    {
        var rawValue = ReadString(entry, "accountExpires");

        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileTime)
            || fileTime == 0
            || fileTime == NeverExpiresFileTime)
        {
            return false;
        }

        try
        {
            return DateTime.FromFileTimeUtc(fileTime) <= DateTime.UtcNow;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    /// <summary>
    /// حذف پیشوند دامنه از نام کاربری Windows.
    /// </summary>
    private static string NormalizeUsername(string username)
    {
        var trimmed = username.Trim();
        var separatorIndex = trimmed.LastIndexOf('\\');

        if (separatorIndex >= 0)
        {
            return trimmed[(separatorIndex + 1)..];
        }

        // قالب UPN حفظ می‌شود تا جست‌وجو هم با sAMAccountName و هم userPrincipalName انجام شود.
        return trimmed;
    }

    /// <summary>
    /// Escape کردن نویسه‌های ویژه برای جلوگیری از LDAP Filter Injection.
    /// </summary>
    private static string EscapeFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }

    /// <summary>
    /// خواندن مقدار متنی از ویژگی LDAP.
    /// </summary>
    private static string ReadString(SearchResultEntry entry, string attributeName)
    {
        var attribute = entry.Attributes[attributeName];

        return attribute is null || attribute.Count == 0
            ? string.Empty
            : attribute[0]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// خواندن مقدار عددی ویژگی LDAP.
    /// </summary>
    private static int ReadInteger(SearchResultEntry entry, string attributeName)
    {
        return int.TryParse(
            ReadString(entry, attributeName),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }

    private static DirectoryAuthenticationResult Result(
        DirectoryAuthenticationStatus status)
    {
        return new DirectoryAuthenticationResult { Status = status };
    }
}
