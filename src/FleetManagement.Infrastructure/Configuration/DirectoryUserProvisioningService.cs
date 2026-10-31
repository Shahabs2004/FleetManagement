using FleetManagement.Domain.Users;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ایجاد پروفایل در اولین ورود و همگام‌سازی اطلاعات آن در ورودهای بعدی.
/// </summary>
public sealed class DirectoryUserProvisioningService(
    IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// پروفایل را ایجاد یا همگام می‌کند؛ حساب غیرفعال داخلی اجازه ورود نمی‌گیرد.
    /// </summary>
    public async Task<DirectoryUserProvisioningResult> SynchronizeAsync(
        DirectoryUserProfile directoryUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directoryUser);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                item => item.Username == directoryUser.Username,
                cancellationToken);

        if (user is null)
        {
            user = new ApplicationUser(
                directoryUser.Username,
                directoryUser.FirstName,
                directoryUser.LastName,
                directoryUser.OrganizationalUnit,
                directoryUser.DistinguishedName);

            dbContext.ApplicationUsers.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new DirectoryUserProvisioningResult(user.Id, true, false);
        }

        if (user.IsDisabled)
        {
            return new DirectoryUserProvisioningResult(user.Id, false, true);
        }

        user.SynchronizeFromDirectory(
            directoryUser.FirstName,
            directoryUser.LastName,
            directoryUser.OrganizationalUnit,
            directoryUser.DistinguishedName);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DirectoryUserProvisioningResult(user.Id, true, false);
    }
}

/// <summary>
/// نتیجه ایجاد یا همگام‌سازی پروفایل داخلی کاربر.
/// </summary>
public sealed record DirectoryUserProvisioningResult(
    Guid UserId,
    bool CanSignIn,
    bool IsDisabledByApplication);
