using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetManagement.Web.Notifications;

/// <summary>
/// کانال لحظه‌ای اعلان‌های خصوصی هر کاربر.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    /// <summary>عضویت نشست در گروه خصوصی کاربر احراز‌شده.</summary>
    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.FindFirst("fleet:username")?.Value
            ?? Context.User?.Identity?.Name;

        if (!string.IsNullOrWhiteSpace(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(username));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>ساخت نام گروه خصوصی با یکسان‌سازی نام کاربری.</summary>
    public static string GetUserGroup(string username)
        => $"user:{username.Trim().ToUpperInvariant()}";
}
