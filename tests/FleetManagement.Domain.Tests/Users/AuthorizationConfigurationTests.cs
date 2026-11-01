using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Users;

public sealed class AuthorizationConfigurationTests
{
    [Fact]
    public void ViewerRole_HasReadOnlyFleetPermissions()
    {
        var role = AuthorizationConfiguration.CreateDefault().Roles
            .Single(item => item.Key == "Viewer");
        var permissions = role.Permissions.Select(item => item.Permission).ToArray();

        Assert.Contains("drivers.view", permissions);
        Assert.Contains("vehicles.view", permissions);
        Assert.DoesNotContain("drivers.manage", permissions);
        Assert.DoesNotContain("vehicles.manage", permissions);
        Assert.DoesNotContain("drivers.*", permissions);
        Assert.DoesNotContain("vehicles.*", permissions);
    }

    [Fact]
    public void FleetManagerRole_HasFleetManagementPermissions()
    {
        var role = AuthorizationConfiguration.CreateDefault().Roles
            .Single(item => item.Key == "FleetManager");
        var permissions = role.Permissions.Select(item => item.Permission).ToArray();

        Assert.Contains("drivers.*", permissions);
        Assert.Contains("vehicles.*", permissions);
        Assert.Contains("drivers.default-vehicle.manage", permissions);
    }

    [Fact]
    public void OperatorRole_HasExplicitMissionActionsWithoutFleetManagement()
    {
        var role = AuthorizationConfiguration.CreateDefault().Roles
            .Single(item => item.Key == "Operator");
        var permissions = role.Permissions.Select(item => item.Permission).ToArray();

        Assert.Contains("missions.review", permissions);
        Assert.Contains("missions.approve", permissions);
        Assert.Contains("missions.reject", permissions);
        Assert.DoesNotContain("drivers.*", permissions);
        Assert.DoesNotContain("vehicles.*", permissions);
    }
}
