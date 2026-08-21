using GunsmithFramework;
using Xunit;

namespace GunsmithServerTest;

public sealed class GunsmithServerSmokeTests
{
    [Fact]
    public void ServerAssemblyReference_ExposesPluginType()
    {
        Assert.Equal("GunsmithFramework", typeof(GunsmithFramework.GunsmithFramework).Namespace);
    }

    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void WorkbenchRequestRequiresOwnedWeaponAndAccessibleEnabledWorkbench(
        bool weaponOwnedByCharacter,
        bool workbenchResolved,
        bool workbenchEnabled,
        bool canClientAccessWorkbench,
        bool expected)
    {
        Assert.Equal(
            expected,
            GunsmithPartChangeServer.CanClientSubmitPartChangeAtWorkbench(
                weaponOwnedByCharacter,
                workbenchResolved,
                workbenchEnabled,
                canClientAccessWorkbench));
    }
}
