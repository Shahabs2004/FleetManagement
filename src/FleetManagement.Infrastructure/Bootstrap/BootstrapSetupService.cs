using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// آماده‌سازی اولیه فایل bootstrap و ایجاد حساب مدیر محلی.
/// </summary>
public sealed class BootstrapSetupService(
    IBootstrapStateStore stateStore,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<BootstrapSetupService> logger)
{
    private static readonly TimeSpan SetupCodeLifetime = TimeSpan.FromHours(24);

    private readonly string _setupCodePath =
        configuration["Bootstrap:SetupCodePath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FleetManagement",
            "setup-code.txt");

    /// <summary>
    /// در اولین اجرا کد راه‌اندازی را تولید می‌کند.
    /// کد خام در فایل موقت با دسترسی محدود ذخیره می‌شود؛ فایل پس از ساخت مدیر حذف می‌شود.
    /// </summary>
    public async Task EnsureSetupCodeAsync(CancellationToken cancellationToken = default)
    {
        var state = await stateStore.ReadAsync(cancellationToken);

        if (state.SetupCompleted)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.SetupCodeHash)
            && state.SetupCodeExpiresAtUtc > DateTimeOffset.UtcNow
            && File.Exists(_setupCodePath))
        {
            return;
        }

        var setupCode = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        state.SetupCodeHash = HashSetupCode(setupCode);
        state.SetupCodeExpiresAtUtc = DateTimeOffset.UtcNow.Add(SetupCodeLifetime);

        await stateStore.WriteAsync(state, cancellationToken);

        var directory = Path.GetDirectoryName(_setupCodePath)
            ?? throw new InvalidOperationException("مسیر فایل کد راه‌اندازی معتبر نیست.");

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(_setupCodePath, setupCode, cancellationToken);

        // فقط مسیر فایل ثبت می‌شود؛ خود کد در گزارش‌های برنامه نوشته نمی‌شود.
        logger.LogWarning(
            "کد اولیه راه‌اندازی ایجاد شد. مدیر فنی باید آن را از فایل محدودشده {SetupCodePath} دریافت کند.",
            _setupCodePath);
    }

    /// <summary>
    /// بررسی کد راه‌اندازی و ساخت حساب مدیر محلی.
    /// </summary>
    public async Task CreateLocalAdministratorAsync(
        string setupCode,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (username.Length is < 3 or > 64)
        {
            throw new ArgumentException("نام کاربری باید بین ۳ تا ۶۴ نویسه باشد.", nameof(username));
        }

        if (password.Length < 14)
        {
            throw new ArgumentException("رمز مدیر محلی باید حداقل ۱۴ نویسه داشته باشد.", nameof(password));
        }

        var state = await stateStore.ReadAsync(cancellationToken);

        if (state.SetupCompleted || state.HasLocalAdministrator)
        {
            throw new InvalidOperationException("حساب مدیر محلی قبلاً ایجاد شده است.");
        }

        if (string.IsNullOrWhiteSpace(state.SetupCodeHash)
            || state.SetupCodeExpiresAtUtc is null
            || state.SetupCodeExpiresAtUtc <= DateTimeOffset.UtcNow
            || !FixedTimeSetupCodeEquals(state.SetupCodeHash, setupCode))
        {
            throw new UnauthorizedAccessException("کد راه‌اندازی نامعتبر یا منقضی شده است.");
        }

        var passwordHash = LocalPasswordHasher.HashPassword(password);

        state.LocalAdministratorUsername = username.Trim();
        state.LocalAdministratorPasswordHash = passwordHash.Hash;
        state.LocalAdministratorPasswordSalt = passwordHash.Salt;
        state.SetupCodeHash = null;
        state.SetupCodeExpiresAtUtc = null;

        await stateStore.WriteAsync(state, cancellationToken);

        // حذف کد خام بلافاصله پس از استفاده موفق.
        if (File.Exists(_setupCodePath))
        {
            File.Delete(_setupCodePath);
        }
    }

    /// <summary>
    /// بررسی رمز حساب مدیر محلی مستقل از SQL و Active Directory.
    /// </summary>
    public async Task<bool> VerifyLocalAdministratorAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var state = await stateStore.ReadAsync(cancellationToken);

        if (!state.HasLocalAdministrator
            || !string.Equals(
                state.LocalAdministratorUsername,
                username,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LocalPasswordHasher.VerifyPassword(
            password,
            state.LocalAdministratorPasswordHash!,
            state.LocalAdministratorPasswordSalt!);
    }

    /// <summary>
    /// تکمیل نهایی راه‌اندازی و قفل کردن مسیر اولیه.
    /// </summary>
    public async Task CompleteSetupAsync(CancellationToken cancellationToken = default)
    {
        var state = await stateStore.ReadAsync(cancellationToken);

        if (!state.HasLocalAdministrator
            || string.IsNullOrWhiteSpace(state.SqlConnectionString))
        {
            throw new InvalidOperationException(
                "برای تکمیل راه‌اندازی، مدیر محلی و اتصال SQL باید آماده باشند.");
        }

        state.SetupCompleted = true;
        state.SetupCodeHash = null;
        state.SetupCodeExpiresAtUtc = null;

        await stateStore.WriteAsync(state, cancellationToken);
    }

    private static string HashSetupCode(string setupCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(setupCode));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeSetupCodeEquals(string expectedHash, string suppliedCode)
    {
        try
        {
            var expected = Convert.FromHexString(expectedHash);
            var actual = Convert.FromHexString(HashSetupCode(suppliedCode));
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
