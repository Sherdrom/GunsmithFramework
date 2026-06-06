using Barotrauma.Items.Components;
using Xunit;

namespace GunsmithSharedTest;

public sealed class GunsmithDataTests
{
    [Fact]
    public void NormalizeSavedState_ReturnsEmptyStringForNull()
    {
        Assert.Equal(string.Empty, GunsmithData.NormalizeSavedState(null!));
    }

    [Fact]
    public void NormalizeSavedState_KeepsShortState()
    {
        const string state = "{\"v\":1,\"parts\":{}}";

        Assert.Equal(state, GunsmithData.NormalizeSavedState(state));
    }

    [Fact]
    public void NormalizeSavedState_TruncatesLongState()
    {
        string state = new('x', 8193);

        string normalized = GunsmithData.NormalizeSavedState(state);

        Assert.Equal(8192, normalized.Length);
        Assert.Equal(new string('x', 8192), normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"v\":1,\"parts\":{}}")]
    [InlineData("{\"v\":1,\"parts\":{\"barrel\":\"short\"}}")]
    public void IsValidSavedState_AcceptsValidState(string state)
    {
        Assert.True(GunsmithData.IsValidSavedState(state));
    }

    [Theory]
    [InlineData("{\"v\":2,\"parts\":{}}")]
    [InlineData("{\"v\":1,\"items\":{}}")]
    [InlineData("{\"v\":1,\"parts\":{}")]
    public void IsValidSavedState_RejectsInvalidShape(string state)
    {
        Assert.False(GunsmithData.IsValidSavedState(state));
    }

    [Fact]
    public void IsValidSavedState_RejectsLongState()
    {
        Assert.False(GunsmithData.IsValidSavedState(new string('x', 8193)));
    }
}
