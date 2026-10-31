using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// آماده‌سازی اولیه فایل bootstrap و ایجاد حساب مدیر محلی.
/// </summary>
public sealed class BootstrapSetupService(
    IBootstrapStateStore stateStore,
    ILogger<BootstrapSetupService> logger)
{
    private static readonly TimeSpan SetupCodeLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// در اولین اجرا کد راه‌اندازی را تولید می‌کند.
    /// کد خام فقط یک بار در گزارش محلی سرور نوشته می‌شود؛
    /// مقدار ذخیره‌شده در فایل bootstrap صرفاً هش کد است.
    /// </summary>
    public async Task EnsureSetupCodeAsync(CancellationToken cancellationToken = default)
    {
        var state = await stateStore.ReadAsync(cancellationToken);

        if (state.SetupCompleted
            || !string.IsNullOrWhiteSpace(state.SetupCodeHash))
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

        // این پیام فقط در گزارش محلی سرور ثبت می‌شود؛ هرگز به پاسخ HTTP یا UI ارسال نمی‌شود.
        logger.LogWarning(
            "کد یک‌بارمصرف راه‌اندازی Fleet Management ایجاد شد. آن را فقط از گزارش محلی محافظت‌شده سرور دریافت کنید: {SetupCode}",
            setupCode);
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
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(state.SetupCodeHash),
                Convert.FromHexString(HashSetupCode(setupCode))))
        {
            throw new UnauthorizedAccessException("کد راه‌اندازی نامعتبر یا منقضی شده است.");
        }

        var passwordHash = LocalPasswordHasher.HashPassword(password);

        state.LocalAdministratorUsername = username.Trim();
        state.LocalAdministratorPasswordHash = passwordHash.Hash;
        state.LocalAdministratorPasswordSalt = passwordHash.Salt;

        // کد راه‌اندازی پس از ایجاد مدیر محلی غیرقابل استفاده می‌شود.
        state.SetupCodeHash = null;
        state.SetupCodeExpiresAtUtc = null;

        await stateStore.WriteAsync(state, cancellationToken);
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
}
