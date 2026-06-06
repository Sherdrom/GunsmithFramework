# GunSmith ReadMe

This folder stores development notes for the `GunSmith` prototype under Deep-Diving-Armory.

## Version Notes

- [Current Summary](./Summary.md)
- [V1.0 Content Production Guide](./V1.0_Content_Production_Guide.md)

## Current Direction

The project is currently validating a modular firearm customization workflow for Barotrauma:

- Lua owns configuration and part selection data
- C# owns runtime UI and sprite composition
- weapon visuals are assembled from reusable part layers

## Current Test Scope

The active validation weapons are:

- `deep_m4`
- `deep_hk416`

Current new-system tag:

- `deep_gunsmith`

Current validated slots:

- `receiver`
- HK416 quick-mod ItemContainer bridge for terminal attachment slots

Current nested validation paths include:

- `receiver/barrel`
- `receiver/handguard`
- `receiver/pistol_grip`
- `receiver/stock`
- `receiver/optic_mount`
- `receiver/receiver_top_rail`
- `receiver/receiver_top_rail/rear_optic_mount`
- `receiver/receiver_top_rail/front_optic_mount`
- `receiver/handguard/top_rail`
- `receiver/handguard/bottom_rail`
- `receiver/handguard/left_rail`
- `receiver/handguard/right_rail`
- `receiver/handguard/top_rail/optic_mount`
- `receiver/handguard/Lower_rail_mount`
- `receiver/handguard/Right_rail_mount`
- `receiver/handguard/Left_rail_mount`
- `receiver/barrel/muzzle_mount`

Current quick-mod UI validation paths include:

- HK416 hidden original ItemContainer slots `1-5`
- HK416 quick-mod UI through `Shift+G`
- HK416 `showWhenContained` visible underbarrel slot with managed drag/drop blocking

## Notes

- This is no longer just the original template readme.
- Template setup details should be read from LuaCs / Luatrauma upstream docs if needed.
- Local version notes for this prototype should continue to live in this folder.
