namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// نتیجه احراز هویت دایرکتوری به همراه پروفایل کاربر در صورت موفقیت.
/// </summary>
public sealed class DirectoryAuthenticationResult
{
    /// <summary>وضعیت نهایی احراز هویت.</summary>
    public DirectoryAuthenticationStatus Status { get; init; }

    /// <summary>پروفایل کاربر در صورت موفقیت.</summary>
    public DirectoryUserProfile? User { get; init; }

    /// <summary>آیا کاربر اجازه ورود دارد؟</summary>
    public bool Succeeded => Status == DirectoryAuthenticationStatus.Success;
}
