# NPC Gunsmith Preset

## Summary

NPC Gunsmith presets are authored in Lua and selected directly from the NPC weapon item with a `gunsmithpreset` XML attribute. Ghost NPC XML should keep ammo and normal equipment only; Gunsmith attachments such as sights, grips, and muzzle devices are declared in the Lua profile.

The runtime applies the profile to the weapon's `GunsmithData.SavedState`, then automatically spawns the real quick-slot attachment items into the weapon inventory. This keeps XML compact while preserving systems that depend on real contained items, such as muzzle devices, lights, lasers, and XML `RequiredItems`.

## Profile Format

```lua
Gunsmith.Config.npcPresets.profiles.ghost_hk416_soldier = {
    weapon = "deep_hk416",
    parts = {
        ["receiver"] = "hk416_receiver_std",
        ["receiver/barrel"] = "hk416_barrel_std",
        ["receiver/handguard"] = "hk416_handguard_std",
        ["receiver/optic_mount"] = "deep_2x5x_sight",
        ["receiver/handguard/Lower_rail_mount"] = "deep_vertical_grip",
    }
}
```

Profiles live in `Lua/Scripts/Gunsmith/Config/NpcPresets.lua`. The table key is the preset name used by XML. `weapon` limits the preset to one item identifier, so a wrong attribute on the wrong gun is ignored with a warning. `parts` is the Gunsmith selection map.

## XML Usage

```xml
<Item identifier="deep_hk416" gunsmithpreset="ghost_hk416_soldier">
  <Item identifier="deep_5.56x45_phy" />
</Item>
```

For an NPC carrying multiple Gunsmith weapons, put the preset on each weapon item that needs customization:

```xml
<Item identifier="deep_hk416" gunsmithpreset="ghost_hk416_soldier" />
<Item identifier="deep_FN57" gunsmithpreset="ghost_fn57_soldier" />
```

Do not put quick-slot Gunsmith attachments such as `2.5x_sight` or `vertical_grip` under the weapon. The profile owns them and the runtime creates the hidden contained items.

## Implementation Notes

- Profiles replace the earlier separate `rules` table. Matching is `gunsmithpreset` attribute + `profile.weapon`.
- The C# bridge captures `gunsmithpreset` from the item XML constructor and exposes it to Lua through `DeepGunsmithGetNpcPreset`.
- Existing non-empty `GunsmithData.SavedState` is not overwritten on the server.
- Clients treat non-empty synced `SavedState` as authoritative so quick-slot container contents do not overwrite the server preset during replication.
- Quick-slot parts require `part.item.identifier`; otherwise they can affect visuals/stats but cannot create a real contained item.
- Current first target is `deep_hk416`, because it already has Gunsmith config and `<GunsmithData />`.

## Test Plan

- Validate `NPC/Ghost/Ghost.xml` as XML.
- Spawn a Ghost soldier with `gunsmithpreset="ghost_hk416_soldier"` on the HK416 and confirm the HK416 receives the profile without attachment entries in Ghost.xml.
- Confirm generated quick-slot items occupy the expected hidden slots and do not get cleared by `QuickMod.SyncFromContainer`.
- Confirm saved guns with existing `SavedState` are not overwritten.
- Build Gunsmith client/server projects and run `DeepGunsmithValidate`.
