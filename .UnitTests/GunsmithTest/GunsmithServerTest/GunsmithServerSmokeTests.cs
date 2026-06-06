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
}
