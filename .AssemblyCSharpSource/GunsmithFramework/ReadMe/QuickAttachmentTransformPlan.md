# Quick Attachment Transform Plan

## 背景

GunSmith quick slots currently use Barotrauma's real hidden `ItemContainer` slots as the authoritative storage for attachments. QAT now keeps attachment effect origins on a GunSmith-owned transform derived from the composed weapon sprite state.

This works for visual/effect alignment, but it couples three different concerns:

- Inventory state and QuickMod drag/drop interaction.
- Weapon-world attachment display.
- Effect origins such as DeepLaser rays and `LightComponent` light positions.

The risky path is especially visible in DeepLaser: it calls `weaponItem.SetContainedItemPositions()` before drawing, then reads `laserItem.DrawPosition` as the ray origin. That makes effect alignment depend on mutating the contained item's real physical state, which can interfere with QuickMod slot interaction and was a key coupling point behind the muzzle/quick UI disappearance issue.

The long-term target is a Gunsmith-owned attachment transform layer. Attachment display and effects should read a shared transform without moving the real contained item away from its inventory slot.

## 小计划：低风险迁移

Keep the current `GunsmithQuickSlotLayoutPatch` compatibility behavior while introducing read-only transform queries.

### 实施原则：不要静默兜底

During this migration, do not hide transform/runtime failures behind silent `try/catch` or silent fallback behavior.

- If a `try/catch` is necessary, it must print the concrete error message and relevant context, such as weapon identifier, attachment identifier, quick slot index, and failing API path.
- Prefer letting exceptions surface directly during early implementation when the failure indicates a bad transform, invalid reflection payload, or broken GunSmith integration.
- Fallback behavior is only acceptable for expected compatibility cases, such as non-Gunsmith weapons or attachments with no registered GunSmith transform.
- Compatibility fallback must not mask a malformed GunSmith transform. Invalid or non-finite transform data should produce an explicit error.

1. Add a `GunsmithQuickAttachmentTransformService` or equivalent API that can answer:
   - weapon item
   - contained attachment item
   - quick slot index
   - world/draw position
   - rotation and direction
   - submarine/current hull context
2. Keep `DeepGunsmithRegisterQuickSlotLayout` stable so existing Lua configs do not need a migration.
3. Migrate DeepLaser first:
   - Stop calling `weaponItem.SetContainedItemPositions()` in the laser draw path.
   - Use the Gunsmith transform as the laser start position and direction when available.
   - Fall back to the existing `laserItem.DrawPosition` behavior for non-Gunsmith weapons.
4. Migrate quick-slot `LightComponent` handling:
   - If the light item has a Gunsmith transform, set the light source position and rotation from that transform.
   - Leave native Barotrauma light behavior unchanged when no transform is available.
5. QAT V0.2 decision: do not add a dynamic quick-slot attachment render path yet.
   - HK416 quick-slot attachments are already visible through the GunSmith composed world sprite.
   - A separate transform-driven render path would duplicate those visual layers unless quick-slot visuals are first excluded from composition.
   - Keep dynamic quick-slot rendering deferred until there is a dedicated render metadata design.
6. Remove the GunSmith quick-slot physical body/rect mutation path after laser and light transforms are stable.
   - QAT V0.3 removes the no-op `SuspendLayouts`/`ResumeLayouts` wrappers and GUI call sites.
   - Leave QuickMod inventory slots and drag/drop interaction on the native contained item state.

This path allows each subsystem to be validated independently and keeps the current muzzle fix in place until all consumers have moved off physical contained item placement.

## 大计划：完整架构

Build quick attachments around a single Gunsmith transform model instead of Barotrauma inventory placement.

The final transform service should own:

- Registration of quick slot layout rules from Lua.
- Resolution from quick slot storage item to attachment transform.
- Canvas-to-item-local conversion using the composed weapon canvas.
- Item-local to world/draw-space conversion using the weapon body, root body direction, submarine draw position, and rotation.
- A stable public query API for other plugins/components.

The intended consumers are:

- Quick attachment rendering on the weapon.
- DeepLaser ray origin and direction.
- Flashlight/laser/light source position and rotation.
- Future muzzle flash, particle, sound, or projectile spawn offsets if those effects are moved out of XML-only slot targeting.

The final invariant is simple: if the player sees an attachment at a point on the weapon, every visual effect from that attachment must originate from the same point.

## Barotrauma 源码参考点

Relevant source paths in `D:\Projects\Barotrauma\Barotrauma\Barotrauma`:

- `BarotraumaShared/SharedSource/Items/Components/ItemContainer.cs`
  - `SetContainedItemPositions()` mutates contained item body, rect, submarine, hull, nested contained positions, and light transforms.
  - This is the behavior GunSmith should stop depending on for attachment display/effects.
- `BarotraumaClient/ClientSource/Items/Components/ItemContainer.cs`
  - `DrawContainedItems()` calculates draw positions for contained items separately from physical positions.
  - This is the closest native model for a display-only attachment path.
- `BarotraumaClient/ClientSource/Items/Components/LightComponent.cs`
  - `SetLightSourceTransformProjSpecific()` chooses light parent body, position, rotation, and sprite flip.
  - Gunsmith quick-slot light handling should override this only when a Gunsmith transform exists.
- `BarotraumaClient/ClientSource/Items/Item.cs`
  - `Item.Draw()` uses either `DrawPosition`/`RotationRad` or `body.DrawPosition`/`body.DrawRotation`.
  - A transform-based quick attachment draw path should avoid relying on the attachment body's mutated position.

Current mod reference points:

- `GunsmithQuickSlotLayoutPatch.cs`
  - Current compatibility layer that registers quick slot layouts and applies physical contained item placement.
- `DeepLaser/ClientProject/ClientSource/Plugin.cs`
  - Current laser draw path that reads `laserItem.DrawPosition`.
- `Lua/Scripts/Gunsmith/Runtime.lua`
  - Registers quick slot layout data through `DeepGunsmithRegisterQuickSlotLayout`.

## 分阶段实施顺序

1. **Transform service v1**
   - Keep existing hooks.
   - Add read-only transform lookup.
   - Do not remove the current physical layout patch yet.

2. **DeepLaser migration**
   - Replace the laser origin/direction with transform service output when available.
   - Remove the explicit `SetContainedItemPositions()` call from the laser draw path.
   - Preserve fallback behavior for non-Gunsmith weapons.

3. **LightComponent migration**
   - Patch only Gunsmith quick-slot attachments.
   - Transform-driven lights should match attachment display position and rotation.
   - Native lights should remain untouched.

4. **Physical layout removal**
   - Keep composed world sprite rendering as the visible quick-slot attachment source.
   - Stop moving quick-slot contained item bodies/rects from the layout patch.
   - Keep inventory/QuickMod interaction based on native contained item storage.

5. **Optional rendering migration**
   - Only add transform-driven quick-slot item rendering if quick-slot visual layers are removed from composed sprites first.
   - Define render metadata separately before implementing this path.

6. **Compatibility cleanup**
   - Remove `SuspendLayouts` and `ResumeLayouts`.
   - Remove the obsolete `DeepGunsmithApplyQuickSlotLayouts` hook and Lua-side calls.
   - Keep Lua-facing hook names stable unless there is a deliberate config migration.

## 测试矩阵

- HK416 muzzle single-click in QuickMod: attachment must not disappear.
- HK416 muzzle drag/drop: drag out, drag back, incompatible slot failure, and restore should keep current behavior.
- Slot 1-5 regression: lower rail, right rail, left rail, optic, and muzzle all remain draggable.
- DeepLaser: laser ray origin overlaps the visible laser device in both left/right facing directions.
- LightComponent: flashlight or laser light originates from the visible attachment, not the hidden inventory slot.
- Non-Gunsmith weapons: DeepLaser and lights keep native behavior.
- Multiplayer: server should not perform client-only transform layout work or receive new high-frequency sync.

## 保留假设

- QuickMod interaction is already acceptable and should not be redesigned as part of this migration.
- Lua quick slot config shape should remain compatible during the migration.
- GunSmith transform calculation is client-side presentation/effect logic.
- XML `targetslot` based muzzle behavior can remain as-is until laser, light, and attachment rendering have been migrated.
