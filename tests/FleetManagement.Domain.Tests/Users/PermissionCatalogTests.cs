using FleetManagement.Application.Authorization;
using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Users;

/// <summary>
/// آزمون‌های کاتالوگ مرکزی و نگاشت مجوزهای پایه.
/// </summary>
public sealed class PermissionCatalogTests
{
    [Fact]
    public void Catalog_ContainsEveryPermissionUsedByDefaultRoles()
    {
        var configuration = AuthorizationConfiguration.CreateDefault();

        var permissions = configuration.Roles
            .SelectMany(role => role.Permissions)
            .Select(permission => permission.Permission)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.All(permissions, permission => Assert.True(
            PermissionCatalog.Contains(permission),
            $"مجوز {permission} در کاتالوگ ثبت نشده است."));
    }

    [Fact]
    public void Catalog_ContainsIndependentPermissionsFromSpecification()
    {
        var independentPermissions = new[]
        {
            PermissionCatalog.Security.AuditView,
            PermissionCatalog.Notifications.SmsView,
            PermissionCatalog.Security.SessionsView,
            PermissionCatalog.Missions.Extend,
            PermissionCatalog.Missions.Split,
            PermissionCatalog.Missions.OdometerManage,
            PermissionCatalog.Missions.SplitScheduleEdit,
            PermissionCatalog.Administration.UsersManage,
            PermissionCatalog.Administration.SystemManage,
            PermissionCatalog.Administration.LocationsManage,
            PermissionCatalog.Administration.MissionTemplateManage,
            PermissionCatalog.Administration.MissionSplitReasonsManage,
            PermissionCatalog.Missions.WithdrawalReview,
            PermissionCatalog.Missions.ChangeReview
        };

        Assert.All(independentPermissions, permission =>
            Assert.True(PermissionCatalog.Contains(permission), permission));
    }

    [Fact]
    public void OperatorRole_HasOnlyExplicitlyAssignedIndependentMissionPermissions()
    {
        var role = AuthorizationConfiguration.CreateDefault().Roles
            .Single(item => item.Key == "Operator");
        var permissions = role.Permissions.Select(item => item.Permission).ToArray();

        Assert.Contains(PermissionCatalog.Missions.OdometerManage, permissions);
        Assert.DoesNotContain(PermissionCatalog.Missions.WithdrawalReview, permissions);
        Assert.Contains(PermissionCatalog.Missions.ChangeReview, permissions);
        Assert.DoesNotContain(PermissionCatalog.Missions.SplitScheduleEdit, permissions);
        Assert.DoesNotContain(PermissionCatalog.Missions.All, permissions);
    }

    [Fact]
    public void FleetManagerRole_CanEditSplitTimeButCannotManageSystemTemplates()
    {
        var role = AuthorizationConfiguration.CreateDefault().Roles
            .Single(item => item.Key == "FleetManager");
        var permissions = role.Permissions.Select(item => item.Permission).ToArray();

        Assert.Contains(PermissionCatalog.Missions.SplitScheduleEdit, permissions);
        Assert.DoesNotContain(PermissionCatalog.Administration.MissionTemplateManage, permissions);
        Assert.DoesNotContain(PermissionCatalog.Administration.MissionSplitReasonsManage, permissions);
    }

    [Fact]
    public void Catalog_RejectsUnknownAndEmptyPermissionKeys()
    {
        Assert.False(PermissionCatalog.Contains("missions.unknown-operation"));
        Assert.False(PermissionCatalog.Contains(string.Empty));
        Assert.False(PermissionCatalog.Contains(" "));
    }

    [Fact]
    public void Catalog_Definitions_HaveUniqueCodes()
    {
        var duplicateCodes = PermissionCatalog.Definitions
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicateCodes);
    }
}
