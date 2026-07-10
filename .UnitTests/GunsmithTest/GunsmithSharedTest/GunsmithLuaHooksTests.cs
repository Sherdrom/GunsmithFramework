using Xunit;

namespace GunsmithFramework.Tests;

public sealed class GunsmithLuaHooksTests
{
    [Fact]
    public void SwitchingOwnerAndClearingUnregistersTrackedHooks()
    {
        List<string> removed = [];
        object firstOwner = new();
        object secondOwner = new();

        GunsmithLuaHooks.Track(firstOwner, "First", removed.Add);
        GunsmithLuaHooks.Track(secondOwner, "Second", removed.Add);
        GunsmithLuaHooks.Clear();

        Assert.Equal(["First", "Second"], removed);
    }
}
