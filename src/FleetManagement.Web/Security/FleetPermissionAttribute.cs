using Microsoft.AspNetCore.Authorization;

namespace FleetManagement.Web.Security;

/// <summary>
/// محدودسازی یک صفحه یا مسیر به مجوز مشخص در ماتریس دسترسی.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FleetPermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// ساخت ویژگی با کلید مجوز موردنیاز.
    /// </summary>
    public FleetPermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Policy = $"Permission:{permission}";
    }
}
