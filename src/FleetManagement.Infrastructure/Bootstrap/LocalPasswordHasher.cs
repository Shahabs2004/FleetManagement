using System.Security.Cryptography;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// هش‌کردن رمز عبور با PBKDF2 و نمک تصادفی مستقل.
/// </summary>
public static class LocalPasswordHasher
{
    // تعداد تکرارها قابل افزایش در نسخه‌های آینده است؛ نسخه فعلی از SHA-256 استفاده می‌کند.
    private const int Iterations = 600_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;

    /// <summary>تولید نمک و هش رمز عبور.</summary>
    public static (string Hash, string Salt) HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return (
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt));
    }

    /// <summary>مقایسه امن رمز واردشده با هش ذخیره‌شده.</summary>
    public static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password)
            || string.IsNullOrEmpty(hash)
            || string.IsNullOrEmpty(salt))
        {
            return false;
        }

        try
        {
            var expectedHash = Convert.FromBase64String(hash);
            var saltBytes = Convert.FromBase64String(salt);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
