using System.Reflection;
using System.Text.Json;
using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Notifications;

public sealed class SmsSettingsSnapshotTests
{
    [Fact]
    public void TemplateSnapshot_DeserializesTemplatesWithoutCredentialProperties()
    {
        const string json = """
            {
              "Organization": "private-organization",
              "Username": "private-user",
              "Password": "private-password",
              "SourceNumber": "1000",
              "EventTemplates": []
            }
            """;

        var snapshot = JsonSerializer.Deserialize<SmsSettingsService.SmsTemplateSettingsSnapshot>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.EventTemplates);
        Assert.DoesNotContain(
            typeof(SmsSettingsService.SmsTemplateSettingsSnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name is "Organization" or "Username" or "Password" or "SourceNumber");
    }
}
