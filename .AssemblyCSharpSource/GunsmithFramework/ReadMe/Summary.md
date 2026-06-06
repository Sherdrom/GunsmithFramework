# Gunsmith Summary

GunSmith is Deep-Diving-Armory's modular firearm customization system for Barotrauma. The current prototype uses Lua for weapon/platform/part configuration and selection state, while C# owns the runtime UI, composed sprites, item component bridge, and runtime stat application.

## Current State

- Primary validated weapons:
  - `deep_m4`
  - `deep_hk416`
  - AK-platform content is being authored, with `deep_ak74m` config work in progress.
- Current schema model:
  - Weapons bind to a platform.
  - Platforms define root slots, canvas size, path names, required hidden slots, and slot layout semantics.
  - Parts define type, provided compatibility tags, optional item backing, visual source data, stats, and nested mounts.
  - Weapon `roots` provide the default root assembly and root socket alignment.
- Current interaction model:
  - `G` opens the full GunSmith UI for the selected hand weapon.
  - `Shift+G` opens the QuickMod UI for weapons with quick slots.
  - HK416 QuickMod uses the weapon's real hidden Barotrauma `ItemContainer` slots as authoritative physical storage; dynamic quick-slot containers are injected from Lua quick slot bindings.
- Current persistence/network model:
  - `GunsmithData.SavedState` stores the encoded selection JSON.
  - Single-player reads and writes the component field locally.
  - Multiplayer uses Barotrauma native item component events; clients request/submit state, the server owns accepted state and broadcasts it back.

## Major Milestones

- V0-V0.3 established the basic Lua/C# bridge, M4 prototype, composed sprite application, text-first UI, and local `SavedState` persistence.
- V0.4-V0.6 moved from pure selection state toward item-driven customization, added validation, display stats, and a preview-oriented UI path.
- V0.7-V0.9.4 reworked the schema for multiple weapons on shared platforms, receiver-led nested part trees, mount defaults, root sockets, attach points, and stricter config validation.
- V1.0 added the external anchor/content production workflow and Chinese production guide.
- V1.1-V1.2.1 stabilized world sprite transforms, localization, and the first gameplay-facing stat path through ergonomics.
- V1.3-V1.3.3 integrated HK416 with Barotrauma's original `ItemContainer` workflow, then refined hidden quick slots, conditional slot visibility, and drag/drop protection.
- V1.4-V1.4.3 moved attachment stats into GunSmith part data, polished the full and quick UI, stabilized quick drag/drop behavior, and split the C# GUI into focused partial files.
- V1.5 moved saved selection sync onto native item component networking through `GunsmithData`.
- V1.5.1 fixed client-side apply timing for existing weapons and Lua reloads, added container movement apply hooks, removed `pcall` from GunSmith Lua, and corrected exposed userdata/parameter assumptions.
- V1.5.2 实现了动态maxslots用于quickSlots（快速改装），不需要再手动提前定义SubContainer了。
- V1.5.3 reworked quick-slot layout around `roots` and removed legacy `rootParts`/`rootSockets` compatibility reads.
- QAT V0.1 introduced a read-only quick attachment transform service, moved DeepLaser and quick-slot `LightComponent` effects onto that transform when available, and kept non-GunSmith/native fallback behavior stable.
- QAT V0.2 removed GunSmith quick-slot body/rect mutation from layout application while keeping composed world sprite rendering, transform queries, and QuickMod storage compatibility intact.
- QAT V0.3 removed the remaining no-op layout wrappers, the `Item.SetContainedItemPositions` postfix, QuickMod GUI suspend/resume calls, and the obsolete `DeepGunsmithApplyQuickSlotLayouts` Lua hook/calls.
- QAT V0.4 moved HK416 primary muzzle `BarrelPos` onto low-frequency quick attachment transform updates, replaced `muzzleLength` with `quickAttachmentTransform.muzzleOutletOffset`, and patched tagged muzzle particles to use the visible draw transform.
- QAT V0.4.1 connected HK416 lower rail sub-weapons to the keyed barrel path through the VCE selector bridge, so Masterkey / M203 projectiles and tagged flash/spark use the `lower_rail` rule after switching.
- QAT V0.4.2 removed structural barrel `muzzleOutletOffset`; bare muzzle position now comes from the quick-slot anchor, while offsets only belong to installed muzzle / lower-rail parts.
- QAT V0.4.3 made C# world sprite state the authority for canvas-to-item-local conversion, removed manual quick-slot origin calibration, and moved barrel registration to canvas points.
- QAT V0.5 finalized the unified canvas conversion cleanup, removed HK416 `itemPosOrigin`, and fixed quick attachment transform world positions to respect weapon `Scale`.

## Public Interfaces

- Lua hooks kept stable:
  - `DeepGunsmithApply`
  - `DeepGunsmithOpen`
  - `DeepGunsmithOpenQuick`
  - `DeepGunsmithRefreshParts`
  - `DeepGunsmithRefreshQuick`
  - `DeepGunsmithRequestState`
  - `DeepGunsmithSaveState`
  - `DeepGunsmithReceiveState`
  - `DeepGunsmithRegisterHiddenQuickSlots`
  - `DeepGunsmithRegisterQuickSlotVisibility`
  - `DeepGunsmithBeginQuickSlotMutation`
  - `DeepGunsmithEndQuickSlotMutation`
  - `DeepGunsmithIsQuickSlotMutation`
  - `DeepGunsmithRegisterQuickSlotCapacity`
  - `DeepGunsmithClearQuickSlotLayouts`
  - `DeepGunsmithRegisterQuickSlotLayout`
  - `DeepGunsmithClearQuickAttachmentBarrelTransforms`
  - `DeepGunsmithRegisterQuickAttachmentBarrelCanvasPoint`
- Persisted state remains:
  - `{"v":1,"parts":{...}}`
- Main console commands:
  - `DeepGunsmithValidate`
  - `DeepGunsmithValidationSelfTest`

## Current Validation Notes

- M4/HK416 full UI and QuickMod flows have been locally validated through repeated in-game passes.
- HK416 QAT V0.1 validation confirms quick-slot flashlight light output is restored, DeepLaser uses the visible attachment transform, and QuickMod drag/drop behavior remains stable.
- QAT V0.2 keeps quick-slot visible attachments in the composed world sprite and removes GunSmith physical layout writes from quick-slot contained items.
- QAT V0.3 removes the obsolete `DeepGunsmithApplyQuickSlotLayouts` hook and Lua-side calls; HK416 QAT regression confirms QuickMod, visible attachments, DeepLaser, and flashlight behavior remain normal.
- QAT V0.4 moves HK416 main muzzle `BarrelPos` and tagged flash/spark particles onto QAT barrel transforms; in-game validation confirms the muzzle flash alignment, QuickMod, DeepLaser, and flashlight behavior remain normal.
- QAT V0.4.1 validation confirms the VCE selector bridge is active and HK416 Masterkey / M203 lower rail barrel positions no longer fall back to the primary muzzle.
- QAT V0.4.3 removes manual quick-slot origin calibration; HK416 needs in-game validation for muzzle, DeepLaser, flashlight, and lower rail alignment after the unified conversion.
- QAT V0.5 validation confirms the barrel particle path aligns after unified conversion; DeepLaser / flashlight offset caused by missing item scale has been fixed and needs in-game revalidation.
- Existing weapons in cabinets, player inventories, and hands are now reapplied after map load and `cl_reloadlua`.
- GunSmith Lua scripts intentionally avoid `pcall` so invalid hook parameters and userdata assumptions surface immediately during testing.
- GunSmith solution build is expected to pass with:
  - `dotnet build .AssemblyCSharpSource/GunSmith/GunSmith.sln -m:1 /p:UseSharedCompilation=false /p:DebugType=None /p:DebugSymbols=false /p:ModDeployDir=.buildcheck\`

## Deferred Work

- Quick attachment transform architecture is in progress; QAT V0.1 is summarized in [V0.1_QAT_Summary.md](V0.1_QAT_Summary.md), QAT V0.2 is summarized in [V0.2_QAT_Summary.md](V0.2_QAT_Summary.md), QAT V0.3 is summarized in [V0.3_QAT_Summary.md](V0.3_QAT_Summary.md), QAT V0.4 is summarized in [V0.4_QAT_Summary.md](V0.4_QAT_Summary.md), and QAT V0.5 is summarized in [V0.5_QAT_Summary.md](V0.5_QAT_Summary.md).
- Dynamic transform-driven quick-slot attachment rendering is deferred until quick-slot visual layers can be separated from composed world sprites.
- Multiplayer validation still needs real multi-client testing.
- Server-side anti-cheat validation is intentionally minimal; full Lua compatibility rule reconstruction on the server is deferred.
- Server-authoritative runtime stat application beyond saved selection sync is deferred.
- Workbench/access gating, wider weapon content, and final art polish remain outside the current sync/apply stabilization pass.
