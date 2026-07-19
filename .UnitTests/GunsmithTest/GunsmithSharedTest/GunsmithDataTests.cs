using Barotrauma.Items.Components;
using GunsmithFramework;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
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
    [InlineData(""" { "parts" : { "receiver/barrel" : "short" }, "v" : 1 } """)]
    [InlineData("""{"v":1,"parts":{"receiver/\"\\\n\t你好":"part-\"\\\n\t世界"}}""")]
    public void IsValidSavedState_AcceptsValidState(string state)
    {
        Assert.True(GunsmithData.IsValidSavedState(state));
    }

    [Theory]
    [InlineData("{\"v\":1,\"parts\":{not-json}}")]
    [InlineData("{\"v\":2,\"parts\":{}}")]
    [InlineData("{\"v\":\"1\",\"parts\":{}}")]
    [InlineData("{\"parts\":{}}")]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"v\":1,\"v\":1,\"parts\":{}}")]
    [InlineData("{\"v\":1,\"parts\":{},\"parts\":{}}")]
    [InlineData("{\"v\":1,\"parts\":{},\"extra\":true}")]
    [InlineData("{\"v\":1,\"parts\":[]}")]
    [InlineData("{\"v\":1,\"parts\":null}")]
    [InlineData("{\"v\":1,\"parts\":\"x\"}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":1}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":true}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":{}}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":[]}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":null}}")]
    [InlineData("{\"v\":1,\"parts\":{\"\":\"part\"}}")]
    [InlineData("{\"v\":1,\"parts\":{\" \":\"part\"}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":\"\"}}")]
    [InlineData("{\"v\":1,\"parts\":{\"receiver\":\" \"}}")]
    [InlineData("{\"v\":1,\"parts\":{},}")]
    [InlineData("{\"v\":1/* comment */,\"parts\":{}}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void IsValidSavedState_RejectsInvalidState(string state)
    {
        Assert.False(GunsmithData.IsValidSavedState(state));
    }

    [Fact]
    public void IsValidSavedState_RejectsLongState()
    {
        Assert.False(GunsmithData.IsValidSavedState(new string('x', 8193)));
    }

    [Theory]
    [InlineData("receiver/handguard", "hk416_handguard", true)]
    [InlineData("", "hk416_handguard", false)]
    [InlineData("receiver/handguard", "", false)]
    [InlineData(" ", "hk416_handguard", false)]
    [InlineData("receiver/handguard", " ", false)]
    public void IsValidPartChange_RequiresSlotAndPart(string slotPath, string partId, bool expected)
    {
        Assert.Equal(expected, GunsmithData.IsValidPartChange(slotPath, partId));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void CanClientSubmitPartChange_AllowsOwnedOrAccessibleItem(
        bool isOwnedByCharacter,
        bool canClientAccess,
        bool expected)
    {
        Assert.Equal(expected, GunsmithData.CanClientSubmitPartChange(isOwnedByCharacter, canClientAccess));
    }

    [Fact]
    public void DataAccess_ReadsAndWritesComponentFromOlderAssemblyGeneration()
    {
        AssemblyLoadContext oldGeneration = new("GunsmithDataTests.OldGeneration", isCollectible: true);
        try
        {
            Assembly oldAssembly = oldGeneration.LoadFromAssemblyPath(typeof(GunsmithData).Assembly.Location);
            Type oldDataType = oldAssembly.GetType(typeof(GunsmithData).FullName!, throwOnError: true)!;
            ItemComponent oldData = (ItemComponent)RuntimeHelpers.GetUninitializedObject(oldDataType);
            oldDataType.GetProperty(nameof(GunsmithData.SavedState))!.SetValue(oldData, "old-state");

            Assert.Same(oldData, GunsmithDataAccess.Find(new[] { oldData }));
            Assert.Equal("old-state", GunsmithDataAccess.GetSavedState(oldData));
            Assert.True(GunsmithDataAccess.SetSavedState(oldData, "new-state"));
            Assert.Equal("new-state", oldDataType.GetProperty(nameof(GunsmithData.SavedState))!.GetValue(oldData));
        }
        finally
        {
            oldGeneration.Unload();
        }
    }
}
