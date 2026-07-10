# Phase 2: Duplicate Logic Consolidation Plan

## Goal

Replace confirmed duplicate Lua logic with the smallest existing-module helpers while preserving all gameplay, validation, UI protocol, persistence, networking, and public configuration behavior.

This phase consolidates only logic that is currently identical. Similar code with different rules stays separate.

## Preconditions

- Phase 1 is complete or its remaining experiments are explicitly deferred.
- The Phase 1 baseline still passes.
- The worktree contains no unrelated source edits.

Run before editing:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
lua .UnitTests\QuickModClearSlotTest.lua
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

## Confirmed Duplicate Areas

### Part `provides` / Mount `accepts` Matching

The same nested-loop rule currently exists in:

- `Lua/Scripts/Gunsmith/Core.lua` as `partProvidesAccepted`
- `Lua/Scripts/Gunsmith/Validation.lua` as `partProvidesAccepted`
- `Lua/Scripts/Gunsmith/Runtime.lua` as `fabricatorPartProvidesAccepted`

All three implementations return `true` when any value in `part.provides` occurs in `accepts`, and return `false` for missing/non-table inputs or no match.

### UI Part Entry Encoding

`UiSpec.lua` and `QuickUiSpec.lua` independently encode the same seven fields:

1. part id
2. localization key
3. status
4. encoded stats
5. item identifier
6. visual texture path
7. visual source rectangle

The two modules must keep their different status calculations. Only the final field encoding is shared.

## Scope

### 1. Add One Core Matching Helper

Add one function to `Core.lua` after the existing local `intersects` helper:

```lua
function Core.PartProvidesAccepted(part, accepts)
    return intersects(Core.PartProvides(part), accepts)
end
```

This reuses the existing `Core.PartProvides` and `intersects` behavior instead of keeping a fourth implementation.

Replace the existing duplicate calls in:

- `Core.ApplyMountDefaultsForPath`
- validation default-part checks
- validation provided/accepted diagnostics
- `Runtime.FabricatorPartItemIds`
- `Core.IsPartCompatible`

Then delete all three local matching functions.

Before editing and after editing, inspect every caller:

```powershell
rg -n "partProvidesAccepted|fabricatorPartProvidesAccepted|PartProvidesAccepted" Lua\Scripts\Gunsmith
```

After the edit, only `Core.PartProvidesAccepted` and its callers should remain.

### 2. Lock The Matching Contract With One Small Lua Check

Add a small reusable test file:

```text
.UnitTests/SharedLuaHelpersTest.lua
```

Cover only the behavior that would break all three former callers:

- nil part returns false
- missing `provides` returns false
- non-table `accepts` returns false
- one matching value returns true
- no matching value returns false
- multiple values still match correctly

Do not add a test framework or fixtures. Use plain `assert` and print one success line.

Run:

```powershell
lua .UnitTests\SharedLuaHelpersTest.lua
```

### 3. Add One UI Part Entry Encoder

Add a pure encoding function to `UiSpec.lua`:

```lua
function UiSpec.EncodePartEntry(partId, part, status)
    local visual = part.visual or {}
    local source = visual.source or {}
    return table.concat({
        partId,
        part.nameKey,
        status,
        Stats.Encode(Stats.PartStats(part), "~"),
        Core.EncodeText(part.item and part.item.identifier or ""),
        Core.EncodeText(visual.texture or ""),
        Core.EncodeText(string.format("%d,%d,%d,%d", source.x or 0, source.y or 0, source.w or 0, source.h or 0))
    }, ":")
end
```

Keep it in `UiSpec.lua`; `Main.lua` already loads `UiSpec.lua` before `QuickUiSpec.lua`. Do not add a new encoding module for one helper.

Update both modules:

- `UiSpec.appendPartEntry` continues to calculate inventory-aware status, then inserts `UiSpec.EncodePartEntry(...)`.
- `QuickUiSpec.appendPartEntry` continues to calculate quick-slot/container-aware status, then inserts `Gunsmith.UiSpec.EncodePartEntry(...)`.

Delete only the duplicated visual/stat/string encoding blocks.

### 4. Extend The Lua Check With The Encoding Contract

Extend `.UnitTests/SharedLuaHelpersTest.lua` to assert one exact encoded part entry containing:

- a non-zero stat
- an item identifier
- a texture path
- a source rectangle
- a supplied status

The expected string must match the existing C# delimiter contract. This test protects the Lua producer while the existing `GunsmithGuiParsingTests` protect the C# consumer.

Do not test the standard and Quick status rules through this helper; those rules intentionally remain in their owning modules.

## Behavior That Must Stay Separate

Do not merge these areas:

- Standard UI missing-item checks use `Inventory.HasPartItem`.
- Quick UI checks current slot contents, compatible identifiers, container acceptance, and collected available ids.
- `Validation.hasAnyProvidedPart` scans all registered parts and is not the same operation as testing one part.
- Empty-part entries contain fewer fields and remain local to each UI builder.
- Final slot/spec assembly remains in `UiSpec.Build` and `QuickUiSpec.Build`.
- `Core.PartProvides`, `contains`, and `intersects` remain small focused helpers.

## Explicit Non-Goals

- Do not create `Utils.lua`, `Common.lua`, or a generic serializer module.
- Do not change delimiter characters or field order.
- Do not migrate UI specs to JSON.
- Do not merge `UiSpec.Build` and `QuickUiSpec.Build`.
- Do not merge standard and Quick status rules.
- Do not split `Core.lua`, `Runtime.lua`, or `Validation.lua` in this phase.
- Do not change public package/configuration APIs.
- Do not change C# production code.
- Do not change persistence or plugin lifecycle behavior.

## Verification

### Automated

Run after each consolidation step:

```powershell
lua .UnitTests\SharedLuaHelpersTest.lua
lua .UnitTests\QuickModClearSlotTest.lua
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
```

The C# tests are required because Lua UI strings are consumed by `GunsmithGuiParsing`.

### In Game

Verify:

- startup validation reports the same errors and warnings as before
- default mounted parts still resolve
- incompatible parts remain unavailable
- fabricator part recipes remain unchanged
- standard UI shows the same installed/available/missing/incompatible states
- Quick UI shows the same states and allowed item behavior
- visual icons, textures, source rectangles, and stats still parse correctly

No full six-platform C# build is required for this Lua-only phase unless another pending change touches C# or project files.

## Commit Sequence

Keep two behavior-preserving rollback boundaries:

1. `refactor(lua): centralize part provides matching`
2. `refactor(lua): reuse UI part entry encoding`

Add the relevant assertions in the same commit as each new helper so no commit leaves non-trivial logic without a runnable check.

## Phase Gate

The phase is complete when:

- the three matching implementations are replaced by `Core.PartProvidesAccepted`
- standard and Quick UI use one part-entry encoder
- all intentional status-rule differences remain local and unchanged
- the new Lua helper check passes
- the existing Lua self-check passes
- all first-party Lua files pass syntax checking
- all C# tests pass
- in-game validation, fabricator, standard UI, and Quick UI behavior match the baseline
- no new module or dependency is introduced
- `git diff --check` passes

## Expected Result

- Production reduction: approximately 20 to 30 lines.
- Duplicate matching logic: three implementations reduced to one.
- Duplicate part-entry encoding: two implementations reduced to one.
- New dependencies: none.
- New production modules: none.
- Gameplay, configuration, wire format, and public APIs: unchanged.
