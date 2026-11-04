using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Core.Tests;

public sealed class ContainerAccessRuleParserTests
{
    [Fact]
    public void ParsesAgentAndGroupRulesWithCombinedRights()
    {
        using var agent = JsonDocument.Parse(
            """{"kind":"agent","id":7,"rights":["take","put"]}""");
        using var group = JsonDocument.Parse(
            """{"kind":"group","id":9,"rights":["use","build"]}""");

        Assert.True(ContainerAccessRuleParser.TryParse(agent.RootElement, out var agentRule));
        Assert.Equal(AccessPrincipalKind.Agent, agentRule.Principal.Kind);
        Assert.Equal(7UL, agentRule.Principal.Id);
        Assert.Equal(
            ContainerAccessRights.Take | ContainerAccessRights.Put,
            agentRule.Rights);

        Assert.True(ContainerAccessRuleParser.TryParse(group.RootElement, out var groupRule));
        Assert.Equal(AccessPrincipalKind.Group, groupRule.Principal.Kind);
        Assert.Equal(9UL, groupRule.Principal.Id);
        Assert.Equal(
            ContainerAccessRights.Use | ContainerAccessRights.Build,
            groupRule.Rights);
    }

    [Theory]
    [InlineData("""{"kind":"agent","id":7,"rights":[]}""")]
    [InlineData("""{"kind":"agent","id":7,"rights":["fly"]}""")]
    [InlineData("""{"kind":"robot","id":7,"rights":["take"]}""")]
    [InlineData("""{"kind":"agent","id":"7","rights":["take"]}""")]
    [InlineData("""{"kind":"agent","id":7}""")]
    [InlineData("""[]""")]
    public void RejectsInvalidRules(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.False(ContainerAccessRuleParser.TryParse(document.RootElement, out _));
    }
}
