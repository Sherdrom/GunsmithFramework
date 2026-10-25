using GunsmithFramework;
using Xunit;

namespace GunsmithSharedTest;

public sealed class GunsmithErgonomicsAimPatchTests
{
    [Fact]
    public void UiPreservesNegativePenaltiesAndCapsPositiveErgonomicsAtAimLimit()
    {
        Assert.Equal(0.0f, GunsmithErgonomicsAimPatch.MinimumErgonomics);
        Assert.Equal(200.0f, GunsmithErgonomicsAimPatch.MaximumErgonomics);
        Assert.Equal(-250.0f, GunsmithErgonomicsAimPatch.ClampErgonomics(-250.0f));
        Assert.Equal(-1.0f, GunsmithErgonomicsAimPatch.ClampErgonomics(-1.0f));
        Assert.Equal(0.0f, GunsmithErgonomicsAimPatch.ClampErgonomics(0.0f));
        Assert.Equal(100.0f, GunsmithErgonomicsAimPatch.ClampErgonomics(100.0f));
        Assert.Equal(200.0f, GunsmithErgonomicsAimPatch.ClampErgonomics(201.0f));
    }
}
