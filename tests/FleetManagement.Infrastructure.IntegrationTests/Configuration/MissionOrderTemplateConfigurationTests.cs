using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Infrastructure.IntegrationTests.Configuration;

public sealed class MissionOrderTemplateConfigurationTests
{
    [Fact]
    public void CreateDefault_UsesTwoSignatureColumns()
    {
        var template = MissionOrderTemplateConfiguration.CreateDefault();

        Assert.Equal(2, template.SignatureColumns);
        template.Validate();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_AcceptsSupportedSignatureLayouts(int columns)
    {
        var template = MissionOrderTemplateConfiguration.CreateDefault();
        template.SignatureColumns = columns;

        template.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Validate_RejectsUnsupportedSignatureLayouts(int columns)
    {
        var template = MissionOrderTemplateConfiguration.CreateDefault();
        template.SignatureColumns = columns;

        Assert.Throws<ArgumentOutOfRangeException>(() => template.Validate());
    }
}
