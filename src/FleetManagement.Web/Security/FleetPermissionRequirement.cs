using Microsoft.AspNetCore.Authorization;

namespace FleetManagement.Web.Security;

/// <summary>
/// شرط لازم برای دسترسی به یک عملیات مشخص سامانه.
/// </summary>
public sealed record FleetPermissionRequirement(string Permission) : IAuthorizationRequirement;
