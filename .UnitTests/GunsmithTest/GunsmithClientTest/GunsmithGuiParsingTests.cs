using Barotrauma;
using GunsmithFramework;
using Microsoft.Xna.Framework;
using System.Collections;
using System.Reflection;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithGuiParsingTests
{
    [Fact]
    public void QuickSpecAllowsEmptyPartListAndInstalledOnlyPartList()
    {
        const string spec = "|ui.quick_root|::::::" +
            "barrel|path.barrel||0||slot=0,anchorValid=1,items=barrel_item;" +
            "scope|path.scope|installed|0|installed:part.installed:installed:Ergonomics=1:scope_item::0%2C0%2C0%2C0|slot=1,anchorValid=1,items=scope_item";
        var slots = GunsmithGui.ParseSpec(spec).Slots;
        Assert.Equal(2, slots.Count);
        Assert.Empty(slots[0].Parts);
        Assert.Equal("", slots[0].CurrentPartId);
        Assert.Contains("barrel_item", slots[0].QuickMeta.AllowedItemIdentifiers);
        Assert.Equal("installed", Assert.Single(slots[1].Parts).Id);
        Assert.Equal("installed", slots[1].CurrentPartId);
        Assert.Contains("scope_item", slots[1].QuickMeta.AllowedItemIdentifiers);
    }

    [Fact]
    public void ParseSpec_ParsesContextPreviewStatsSlotsAndQuickMeta()
    {
        const string spec =
            "root.child|deep.gunsmith.ui.custom|root" +
            "::padding=8,scale=1.5,offsetX=3,offsetY=-2" +
            "::Ergonomics=4.5,RangedSpreadReduction=0.25,RangedAttackSpeed=-0.1" +
            "::barrel|deep.part.barrel|short|1|" +
            "short:deep.part.short:available:Ergonomics=1~RangedAttackMultiplier=0.2:item%7Csmg:texture%3Apath:1%2C2%2C3%2C4," +
            "long:deep.part.long:missing," +
            "exclusive:deep.part.exclusive:exclusive," +
            "incompatible:deep.part.incompatible:incompatible" +
            "|slot=2,anchorX=0.25,anchorY=0.75,anchorValid=1,items=smg~rifle";

        GunsmithGui.GunsmithGuiSpec parsed = GunsmithGui.ParseSpec(spec);

        Assert.Equal("root.child", parsed.Context.CurrentPath);
        Assert.Equal("deep.gunsmith.ui.custom", parsed.Context.PathLabel);
        Assert.Equal("root", parsed.Context.ParentPath);
        Assert.Equal(8.0f, parsed.PreviewSettings.Padding);
        Assert.Equal(1.5f, parsed.PreviewSettings.Scale);
        Assert.Equal(new Vector2(3.0f, -2.0f), parsed.PreviewSettings.Offset);
        Assert.Equal(4.5f, parsed.WeaponStats.Ergonomics);
        Assert.Equal(0.25f, parsed.WeaponStats.Get(StatTypes.RangedSpreadReduction));
        Assert.Equal(-0.1f, parsed.WeaponStats.Get(StatTypes.RangedAttackSpeed));

        GunsmithGui.GunsmithGuiSlot slot = Assert.Single(parsed.Slots);
        Assert.Equal("barrel", slot.Path);
        Assert.Equal("deep.part.barrel", slot.NameKey);
        Assert.Equal("short", slot.CurrentPartId);
        Assert.True(slot.CanEnter);
        Assert.Equal(2, slot.QuickMeta.SlotIndex);
        Assert.Equal(new Vector2(0.25f, 0.75f), slot.QuickMeta.Anchor);
        Assert.True(slot.QuickMeta.AnchorValid);
        Assert.Contains("smg", slot.QuickMeta.AllowedItemIdentifiers);
        Assert.Contains("rifle", slot.QuickMeta.AllowedItemIdentifiers);

        Assert.Equal(3, slot.Parts.Count);
        GunsmithGui.GunsmithGuiPart shortPart = slot.Parts[0];
        Assert.Equal("short", shortPart.Id);
        Assert.Equal("deep.part.short", shortPart.NameKey);
        Assert.Equal("available", shortPart.Status);
        Assert.Equal(1.0f, shortPart.Stats.Ergonomics);
        Assert.Equal(0.2f, shortPart.Stats.Get(StatTypes.RangedAttackMultiplier));
        Assert.Equal("item|smg", shortPart.ItemIdentifier);
        Assert.Equal("texture:path", shortPart.VisualTexturePath);
        Assert.Equal(new Rectangle(1, 2, 3, 4), shortPart.VisualSourceRect);

        GunsmithGui.GunsmithGuiPart longPart = slot.Parts[1];
        Assert.Equal("long", longPart.Id);
        Assert.False(longPart.IsActionable);

        GunsmithGui.GunsmithGuiPart exclusivePart = slot.Parts[2];
        Assert.Equal("exclusive", exclusivePart.Status);
        Assert.False(exclusivePart.IsActionable);
        Assert.DoesNotContain(slot.Parts, part => part.Id == "incompatible");
    }

    [Fact]
    public void ParseSpec_UsesDefaultsAndSkipsInvalidEntries()
    {
        GunsmithGui.GunsmithGuiSpec parsed = GunsmithGui.ParseSpec("current::::invalid|entry;scope|name|part|0|part:name");

        Assert.Equal("current", parsed.Context.CurrentPath);
        Assert.Equal("gunsmith.framework.ui.weapon_root", parsed.Context.PathLabel);
        GunsmithGui.GunsmithGuiSlot slot = Assert.Single(parsed.Slots);
        Assert.Equal("scope", slot.Path);
        Assert.False(slot.CanEnter);
        Assert.Equal(-1, slot.QuickMeta.SlotIndex);
        Assert.False(slot.QuickMeta.AnchorValid);
        Assert.Equal(Vector2.Zero, slot.QuickMeta.Anchor);
        GunsmithGui.GunsmithGuiPart part = Assert.Single(slot.Parts);
        Assert.Equal("part", part.Id);
        Assert.Equal("name", part.NameKey);
        Assert.Equal("available", part.Status);
        Assert.Equal(Rectangle.Empty, part.VisualSourceRect);
    }

    [Fact]
    public void ParseSpec_PreservesPartsAfterEmptyEncodedFields()
    {
        const string spec =
            "root::padding=12,scale=1::Ergonomics=0::rear_sight|rear_sight|installed|0|" +
            "__empty:empty:disabled," +
            "virtual:part.virtual:available::::0%2C0%2C0%2C0," +
            "installed:part.installed:installed:Ergonomics=1:item:texture:1%2C2%2C3%2C4";

        GunsmithGui.GunsmithGuiSlot slot = Assert.Single(GunsmithGui.ParseSpec(spec).Slots);

        Assert.Equal(3, slot.Parts.Count);
        Assert.Equal("virtual", slot.Parts[1].Id);
        Assert.Equal("installed", slot.Parts[2].Id);
    }

    [Theory]
    [InlineData("rear_sight", "rear_sight", "__empty", "__empty")]
    [InlineData("barrel", "rear_sight", "__empty", null)]
    public void PartSelectionAfterSlotChange_OnlyPreservesSelectionForSameSlot(
        string previousSlot,
        string nextSlot,
        string partId,
        string? expected)
    {
        Assert.Equal(expected, GunsmithGui.PartSelectionAfterSlotChange(previousSlot, nextSlot, partId));
    }

    [Fact]
    public void InsertTooltipStats_PutsStatsBeforeModAndSkillLines()
    {
        const string modLine = "\n‖color:purple‖Deep-Diving-Armory‖color:end‖";
        const string tooltip = "description" + modLine + "\nrequired skill";

        Assert.Equal(
            "description\nbonus" + modLine + "\nrequired skill",
            GunsmithGui.InsertTooltipStats(tooltip, "bonus", modLine));
    }

    [Fact]
    public void FormatTooltipStat_MatchesVanillaValueFirstStyle()
    {
        Assert.Equal(
            $"  ‖color:{XMLExtensions.ToStringHex(GUIStyle.Green)}‖+30%‖color:end‖ 火力",
            GunsmithGui.FormatTooltipStat("+30%", "火力", 0.3f));
        Assert.Equal(
            $"  ‖color:{XMLExtensions.ToStringHex(GUIStyle.Red)}‖-10%‖color:end‖ 射速",
            GunsmithGui.FormatTooltipStat("-10%", "射速", -0.1f));
        Assert.Equal(
            $"‖color:{XMLExtensions.ToStringHex(GUIStyle.Green)}‖+30%‖color:end‖ 火力",
            GunsmithGui.FormatTooltipStat("+30%", "火力", 0.3f, indent: string.Empty));
    }

    [Theory]
    [InlineData("Ergonomics=-25", -25.0f, "-25")]
    [InlineData("Ergonomics=25", 25.0f, "+25")]
    [InlineData("Ergonomics=250", 200.0f, "+200")]
    [InlineData("TeamAttackMultiplier=-1.5", -1.0f, "-100%")]
    [InlineData("TeamAttackMultiplier=-1", -1.0f, "-100%")]
    [InlineData("RangedAttackMultiplier=-0.15", -0.15f, "-15%")]
    [InlineData("RangedAttackMultiplier=0.2", 0.2f, "+20%")]
    [InlineData("WeaponsSkillBonus=-15", -15.0f, "-15")]
    [InlineData("ExtraMissionCount=-3", -3.0f, "-3")]
    public void StatFormattingShowsSignedErgonomicsAndClampsOnlyPercentages(
        string spec, float expectedValue, string expectedText)
    {
        GunsmithGui.GunsmithStats stats = GunsmithGui.ParseSpec("root::" + spec + "::").WeaponStats;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        IEnumerable entries = (IEnumerable)typeof(GunsmithGui).GetMethod("FormatStats", flags)!.Invoke(null, [stats])!;
        object entry = Assert.Single(entries.Cast<object>());
        Assert.Equal(expectedValue, entry.GetType().GetProperty("Value")!.GetValue(entry));
        Assert.Contains(expectedText, (string)entry.GetType().GetProperty("Text")!.GetValue(entry)!);
        Assert.Equal(
            expectedValue < 0.0f ? Color.IndianRed : Color.LightGreen,
            typeof(GunsmithGui).GetMethod("StatDisplayColor", flags)!.Invoke(null, [entry]));

        string tooltip = (string)typeof(GunsmithGui).GetMethod("FormatTooltipStats", flags)!.Invoke(
            null, [stats, "gunsmith.framework", "  "])!;
        string color = XMLExtensions.ToStringHex(expectedValue < 0.0f ? GUIStyle.Red : GUIStyle.Green);
        Assert.Contains($"‖color:{color}‖{expectedText}‖color:end‖", tooltip);
    }

    [Fact]
    public void StatFormattingOmitsZeroErgonomics()
    {
        GunsmithGui.GunsmithStats stats = GunsmithGui.ParseSpec("root::Ergonomics=0::").WeaponStats;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        string tooltip = (string)typeof(GunsmithGui).GetMethod("FormatTooltipStats", flags)!.Invoke(
            null, [stats, "gunsmith.framework", "  "])!;

        Assert.DoesNotContain("‖color:", tooltip);
    }

    [Theory]
    [InlineData("1,2,3,4", true, 1, 2, 3, 4)]
    [InlineData(" 5, 6, 7, 8 ", true, 5, 6, 7, 8)]
    [InlineData("1,2,3", false, 0, 0, 0, 0)]
    [InlineData("1,2,three,4", false, 0, 0, 0, 0)]
    public void TryParseRectangle_ParsesExpectedInputs(string value, bool expected, int x, int y, int width, int height)
    {
        bool result = GunsmithApi.TryParseRectangle(value, out Rectangle rectangle);

        Assert.Equal(expected, result);
        if (expected)
        {
            Assert.Equal(new Rectangle(x, y, width, height), rectangle);
        }
    }
}
