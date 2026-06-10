using GunsmithFramework;
using System.Xml.Linq;
using Xunit;

namespace GunsmithSharedTest;

public sealed class GunsmithFabricatorRecipeFilterPatchTests
{
    [Fact]
    public void IsEnabledConfigElement_AcceptsGunsmithFrameworkButtonAttribute()
    {
        XElement element = TestElement(
            "<Item identifier=\"customfabricator\">" +
            "  <Fabricator gunsmithframeworkbutton=\"true\" />" +
            "</Item>");

        Assert.True(GunsmithFabricatorRecipeFilterPatch.IsEnabledElement(element));
    }

    [Theory]
    [InlineData("<Item identifier=\"customfabricator\"><Fabricator /></Item>")]
    [InlineData("<Item identifier=\"customfabricator\"><Fabricator gunsmithframeworkbutton=\"false\" /></Item>")]
    [InlineData("<Item identifier=\"customfabricator\"><ItemContainer gunsmithframeworkbutton=\"true\" /></Item>")]
    public void IsEnabledConfigElement_RejectsMissingOrWrongMarker(string xml)
    {
        Assert.False(GunsmithFabricatorRecipeFilterPatch.IsEnabledElement(TestElement(xml)));
    }

    [Theory]
    [InlineData("fabricator", true)]
    [InlineData("FABRICATOR", true)]
    [InlineData("medicalfabricator", false)]
    [InlineData("deep_general_fabricator", false)]
    public void IsDefaultEnabledPrefabIdentifier_OnlyAcceptsVanillaFabricator(string identifier, bool expected)
    {
        Assert.Equal(expected, GunsmithFabricatorRecipeFilterPatch.IsDefaultEnabledPrefabIdentifier(identifier));
    }

    [Fact]
    public void ParsePartItemIdentifierResult_SplitsTrimsAndDeduplicatesIdentifiers()
    {
        object[] hookResult =
        {
            "scope, barrel ;grip",
            new[] { "stock|BARREL", "laser\nlight" }
        };

        HashSet<string> identifiers = GunsmithFabricatorRecipeFilterPatch.ParsePartItemIdentifierResult(hookResult);

        Assert.Equal(6, identifiers.Count);
        Assert.Contains("scope", identifiers);
        Assert.Contains("barrel", identifiers);
        Assert.Contains("grip", identifiers);
        Assert.Contains("stock", identifiers);
        Assert.Contains("laser", identifiers);
        Assert.Contains("light", identifiers);
        Assert.Contains("BARREL", identifiers);
    }

    private static XElement TestElement(string xml)
        => XElement.Parse(xml);
}
