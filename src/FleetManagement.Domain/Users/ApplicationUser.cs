using System.Text.Json;
using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Users;

/// <summary>
/// پروفایل داخلی کاربر سازمانی که هویت اصلی آن از Active Directory دریافت می‌شود.
/// </summary>
public sealed class ApplicationUser : Entity
{
    private ApplicationUser()
    {
        // سازنده خصوصی برای EF Core
    }

    /// <summary>نام کاربری یکتای دامنه.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>نام کاربر که از AD همگام می‌شود.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>نام خانوادگی کاربر که از AD همگام می‌شود.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>واحد سازمانی جاری کاربر که ممکن است به‌صورت دستی اصلاح شده باشد.</summary>
    public string OrganizationalUnit { get; private set; } = string.Empty;

    /// <summary>آخرین واحد سازمانی دریافت‌شده از AD برای بازگشت از override دستی.</summary>
    public string DirectoryOrganizationalUnit { get; private set; } = string.Empty;

    /// <summary>آیا واحد سازمانی توسط مدیر سامانه به‌صورت دستی تعیین شده است؟</summary>
    public bool IsOrganizationalUnitManuallyOverridden { get; private set; }

    /// <summary>فهرست DN گروه‌های مجاز کاربر در AD به‌صورت JSON.</summary>
    public string DirectoryGroupsJson { get; private set; } = "[]";

    /// <summary>شناسه Distinguished Name در Active Directory.</summary>
    public string DirectoryDistinguishedName { get; private set; } = string.Empty;

    /// <summary>غیرفعال‌سازی داخلی سامانه؛ بر وضعیت AD اولویت دارد.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>زمان ایجاد پروفایل در سامانه.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>آخرین زمان همگام‌سازی اطلاعات دایرکتوری.</summary>
    public DateTimeOffset LastDirectorySyncAtUtc { get; private set; }

    /// <summary>
    /// ساخت پروفایل اولیه پس از اولین ورود موفق از Active Directory.
    /// </summary>
    public ApplicationUser(
        string username,
        string firstName,
        string lastName,
        string organizationalUnit,
        string directoryDistinguishedName,
        IEnumerable<string>? directoryGroups = null)
    {
        Username = RequiredText(username, nameof(username), 64);
        FirstName = OptionalText(firstName, 100);
        LastName = OptionalText(lastName, 100);
        OrganizationalUnit = OptionalText(organizationalUnit, 200);
        DirectoryOrganizationalUnit = OptionalText(organizationalUnit, 200);
        DirectoryGroupsJson = SerializeGroups(directoryGroups);
        DirectoryDistinguishedName = RequiredText(
            directoryDistinguishedName,
            nameof(directoryDistinguishedName),
            2048);

        CreatedAtUtc = DateTimeOffset.UtcNow;
        LastDirectorySyncAtUtc = CreatedAtUtc;
    }

    /// <summary>
    /// همگام‌سازی اطلاعات پایه از AD بدون تغییر وضعیت غیرفعال داخلی یا واحد دستی.
    /// </summary>
    public void SynchronizeFromDirectory(
        string firstName,
        string lastName,
        string organizationalUnit,
        string directoryDistinguishedName,
        IEnumerable<string>? directoryGroups = null)
    {
        FirstName = OptionalText(firstName, 100);
        LastName = OptionalText(lastName, 100);
        DirectoryDistinguishedName = RequiredText(
            directoryDistinguishedName,
            nameof(directoryDistinguishedName),
            2048);
        DirectoryGroupsJson = SerializeGroups(directoryGroups);

        DirectoryOrganizationalUnit = OptionalText(organizationalUnit, 200);
        if (!IsOrganizationalUnitManuallyOverridden)
        {
            OrganizationalUnit = DirectoryOrganizationalUnit;
        }

        LastDirectorySyncAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>خواندن DN گروه‌های مجاز کاربر از تصویر ذخیره‌شده.</summary>
    public IReadOnlyList<string> GetDirectoryGroupDistinguishedNames()
        => JsonSerializer.Deserialize<List<string>>(DirectoryGroupsJson) ?? [];

    private static string SerializeGroups(IEnumerable<string>? groups)
    {
        var normalized = (groups ?? [])
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalized);
    }

    /// <summary>
    /// غیرفعال‌سازی داخلی حساب توسط مدیر سامانه.
    /// </summary>
    public void Disable()
    {
        IsDisabled = true;
    }

    /// <summary>
    /// فعال‌سازی مجدد فقط با اقدام صریح مدیر سامانه.
    /// </summary>
    public void Enable()
    {
        IsDisabled = false;
    }

    /// <summary>
    /// تعیین واحد سازمانی دستی با حفظ آن در برابر همگام‌سازی بعدی AD.
    /// </summary>
    public void OverrideOrganizationalUnit(string organizationalUnit)
    {
        OrganizationalUnit = RequiredText(
            organizationalUnit,
            nameof(organizationalUnit),
            200);

        IsOrganizationalUnitManuallyOverridden = true;
    }

    /// <summary>
    /// بازگشت واحد سازمانی به همگام‌سازی خودکار از AD.
    /// </summary>
    public void ClearOrganizationalUnitOverride(string directoryOrganizationalUnit)
    {
        DirectoryOrganizationalUnit = OptionalText(directoryOrganizationalUnit, 200);
        OrganizationalUnit = DirectoryOrganizationalUnit;
        IsOrganizationalUnitManuallyOverridden = false;
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }

    private static string OptionalText(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
