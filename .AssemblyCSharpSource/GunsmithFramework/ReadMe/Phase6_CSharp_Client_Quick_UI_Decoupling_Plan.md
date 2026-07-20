# Phase 6: C# Client Quick UI Decoupling Plan

## Goal

Reduce coupling in the C# client Quick UI without changing gameplay behavior:

- move Quick-slot drag state, inventory mutation, rollback, and Lua synchronization out of the overlay renderer;
- move Quick-overlay input Harmony bridges out of the hidden-slot layout owner;
- leave rendering, managed-slot policy, and drag transactions with one clear owner each;
- delete forwarding and state that become unnecessary during the move.

Preserve current GUI appearance, slot rules, item placement order, rollback behavior, sounds, hook names, multiplayer behavior, reload cleanup, and public Lua/C# entry points.

This phase is driven by responsibility and dependency direction, not file length. The standard Gunsmith GUI is already split into focused partial files; it must not be split again merely to make files shorter.

## Preconditions And Baseline

- Phase 4 lifecycle cleanup is complete and its reload tests remain green.
- Phase 5 is either complete or explicitly deferred before implementation starts.
- At plan-writing time, `ReadMe/` contains Phase 1 through Phase 4 but no Phase 5 plan. Do not silently combine presumed Phase 5 work with this phase.
- The worktree contains no unrelated source edits.
- Record the current client QuickMod behavior matrix before moving code.

At plan-writing time, the relevant source snapshot is:

| File | Lines | Relevant responsibilities |
| --- | ---: | --- |
| `ClientProject/ClientSource/GunsmithGui/GunsmithGuiQuickOverlay.cs` | 1043 | drawing, hit testing, drag state, inventory mutation, rollback, Lua sync |
| `ClientProject/ClientSource/GunsmithGui/GunsmithGui.cs` | 754 | window state plus four Quick-drag fields and forwarding methods |
| `ClientProject/ClientSource/GunsmithQuick/GunsmithHiddenQuickSlotsPatch.cs` | 714 | registration, visibility, layout packing, inventory guards, GUI/input bridges |

`GunsmithHiddenQuickSlotsPatch` currently contains 11 direct `GunsmithGui` references. `PendingQuickDrag.OriginalPartId` is assigned but never read. Existing client tests cover parsing and reload cleanup, but do not directly protect Quick drag/drop decisions or rollback state.

Run and record the baseline before editing:

```powershell
dotnet restore .UnitTests\GunsmithTest\GunsmithTest.sln
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
Get-ChildItem .UnitTests -File -Filter *.lua | Sort-Object Name | ForEach-Object { lua $_.FullName }
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

If restore is blocked by an external NuGet or TLS failure, record that separately. A cached `--no-build --no-restore` run is useful for diagnosis but is not the final phase gate.

## Architectural Invariants

Preserve these rules throughout the phase:

- `QuickOverlayFrame` owns drawing, geometry, hit testing, hover/failure feedback, and its mouse-release latch only.
- One `GunsmithQuickDrag` owner holds all pending-drag and native-drop reconciliation state.
- Inventory mutation and its rollback remain in the same owner; do not split forward and rollback paths between classes.
- Every weapon-inventory mutation remains inside the existing `BeginQuickSlotMutation` / `EndQuickSlotMutation` `try/finally` guard.
- A failed operation must leave every involved item in a valid inventory or still being dragged; it must not lose or duplicate an item.
- A completed or cancelled transaction invokes `GunsmithFrameworkSyncQuickContainer` under the same conditions and no more often than the current implementation.
- Native inventory combine behavior remains native; the Quick transaction intercept must not consume a combinable native drop.
- The hidden-slot owner continues to own registration data, conditional visibility, layout caches, and managed-slot enforcement.
- Harmony endpoint classes contain only patch adaptation and delegation, not a second copy of policy or transaction logic.
- Reload and normal window close both restore or clear pending drag state through the drag owner.
- No interface hierarchy, dependency-injection container, event bus, generic inventory service, or new dependency is introduced.

## Confirmed Coupling To Remove

| Current owner | Mixed concern | Consequence | Phase 6 action |
| --- | --- | --- | --- |
| `QuickOverlayFrame` | drawing plus `TryPutItem`, `RemoveItem`, swap, rollback, and Lua sync | renderer changes can affect inventory correctness | extract the transaction boundary |
| outer `GunsmithGui` | `pendingQuickDrag`, native-drop guard state, delayed clear state | drag lifecycle is owned outside the code that implements it | move state to the transaction owner |
| outer `GunsmithGui` | two pass-through native-drag methods | callers depend on GUI only to reach unrelated inventory logic | delete pass-through methods |
| `GunsmithHiddenQuickSlotsPatch` | world-drop, cursor, character input, and mouse input patches | layout/policy owner depends broadly on GUI | move GUI/input endpoints to one bridge class |
| `PendingQuickDrag` | `OriginalPartId` is stored but never read | dead state obscures the real transaction contract | delete the field and constructor argument |
| client tests | no Quick drag state-transition coverage | extraction can silently change interception or cleanup | add focused tests before or with the move |

File size alone is not a defect. Keep cohesive layout and managed-slot enforcement code together even if `GunsmithHiddenQuickSlotsPatch` remains substantial after the input bridge is moved.

## Target Dependency Direction

```text
GunsmithGui / QuickOverlayFrame
    -> GunsmithQuickDrag
        -> GunsmithHiddenQuickSlotsPatch mutation guard
        -> GunsmithApi Lua-hook call

GunsmithQuickOverlayInputPatch
    -> GunsmithGui for window/input queries
    -> GunsmithQuickDrag for native-drag reconciliation

GunsmithHiddenQuickSlotsPatch
    -> managed-slot registration, visibility, layout, and inventory enforcement
    -> at most the existing read-only IsOpenForItem visibility query
```

Forbidden reverse dependencies:

- `GunsmithQuickDrag` must not depend on `QuickOverlayFrame` or any drawing type.
- `GunsmithHiddenQuickSlotsPatch` must not call Quick-overlay frame methods or own character/cursor/world-drop behavior.
- `QuickOverlayFrame` must not call `Inventory.TryPutItem`, `Inventory.RemoveItem`, `GunsmithApi.CallLuaHook`, or mutation-guard methods directly after extraction.

The two read-only `GunsmithGui.IsOpenForItem` checks used by conditional slot visibility may remain. Replacing them with an interface, callback registry, or duplicated UI state would add more machinery than it removes.

## Scope

### 1. Lock The Transaction Contract With Focused Tests

Add one client test file:

```text
.UnitTests/GunsmithTest/GunsmithClientTest/GunsmithQuickDragTests.cs
```

Use the existing xUnit project and the existing reflection/uninitialized-object style only where Barotrauma constructors cannot run in the test host. Do not add a mocking framework or production diagnostics solely for tests.

Cover the smallest executable behavior set:

- reset with no pending drag is safe;
- reset can be called twice;
- a pending drag matches only its original weapon, item, slot path, and slot index;
- an unrelated dragged item is not intercepted;
- a drop into the source weapon inventory is left to the existing path;
- an invalid target slot is not intercepted;
- an allowed native combine is left to native inventory behavior;
- removed weapon/item state is cancelled without retaining pending state;
- successful reconciliation clears pending and delayed native-drag state;
- reload cleanup clears all transaction state through `GunsmithQuickDrag.Reset`.

Where the test host cannot construct a real working `Inventory`, test the pure branch decision or state transition and cover the actual multi-inventory mutation/rollback with the in-game matrix. Do not create a fake inventory abstraction just to force an integration scenario into xUnit.

Extend `GunsmithReloadCleanupTests.cs` only for the ownership change; do not duplicate the same reset assertions in both files.

### 2. Extract One Quick Drag Transaction Owner

Add:

```text
ClientProject/ClientSource/GunsmithQuick/GunsmithQuickDrag.cs
```

Use one `internal static` class. It owns:

- the pending-drag record;
- the current pending drag;
- the reentrancy flag for native slot drops;
- the item scheduled for removal from `Inventory.DraggingItems` after a native drop;
- begin, place, swap, replace, native-drop interception, reconciliation, restore, and reset operations;
- the private helpers that put/remove/return items and invoke Quick-container synchronization.

Move the existing logic with minimal signature changes. The overlay should pass primitive transaction inputs such as weapon, slot path, slot index, allowed identifiers, and dragged item. `GunsmithQuickDrag` must not accept a `QuickOverlayFrame` or rendering/layout object.

Delete `PendingQuickDrag.OriginalPartId` and its constructor argument because the value has no reader. Keep `SlotPath`: it distinguishes restoring to the originating displayed slot.

Keep the current rollback order exactly:

1. move the displaced item only after all preconditions pass;
2. attempt the requested placement;
3. if placement fails, remove any partially moved item;
4. restore the displaced item to its exact source slot when possible;
5. otherwise use the existing controlled-character inventory fallback;
6. clear pending state only when the transaction is complete or irrecoverably invalid;
7. synchronize Lua only under the existing success/cancellation conditions.

Do not replace the explicit `try/finally` mutation guards with a generic transaction framework. Three short guarded operations are easier to audit than a reusable abstraction with hidden inventory effects.

### 3. Reduce `QuickOverlayFrame` To UI Work

Keep in `GunsmithGuiQuickOverlay.cs`:

- `QuickOverlayFrame` construction and update;
- slot layout, anchor calculation, connector drawing, icons, hover states, and failed-drop timers;
- buffer-inventory hit testing;
- allowed-item display feedback;
- sounds and the short mouse-release suppression latch.

Change interaction methods to delegate transaction work to `GunsmithQuickDrag`:

- starting a drag records state through the drag owner;
- dropping on a Quick slot asks the drag owner to place or swap;
- closing/restoring asks the drag owner to restore the source item;
- visual success/failure feedback remains in the overlay.

Move `suppressQuickUninstallRelease` into `QuickOverlayFrame` if baseline verification confirms the frame lifetime is exactly the open Quick window lifetime. If that assumption is false, keep it as the single GUI-owned UI latch; do not put this visual/input debounce flag into the transaction owner.

After extraction, the overlay must contain no inventory mutation, rollback, Quick-slot mutation-guard, or Lua synchronization implementation.

### 4. Remove Drag State And Forwarders From `GunsmithGui`

Remove from the outer partial class:

- `pendingQuickDrag`;
- `handlingNativeQuickDragDrop`;
- `pendingNativeQuickDragDropClearItem`;
- `TryHandlePendingQuickDragNativeSlotDrop` forwarding;
- `ReconcilePendingQuickDragAfterNativeDragging` forwarding.

Update `Reset` and `CloseWindow` to call the owner-local restore/reset operations in the correct order. Preserve Phase 4 guarantees:

- cleanup works with no active window;
- stale dragging-list entries are removed;
- reset is idempotent;
- line texture disposal and other GUI cache cleanup remain unchanged.

Keep the one `TryHandleQuickOverlayDragging` GUI entry point because it legitimately delegates to the live frame instance owned by `GunsmithGui`.

### 5. Separate The Quick Overlay Input Harmony Bridge

Add:

```text
ClientProject/ClientSource/GunsmithQuick/GunsmithQuickOverlayInputPatch.cs
```

Move only the Harmony endpoints concerned with GUI/input adaptation:

- handling Quick-overlay dragging before a world drop;
- reconciling the pending drag after native dragging;
- including the Quick buffer in `Inventory.IsMouseOnInventory`;
- blocking controlled-character input while the Gunsmith window is active;
- keeping the cursor local to the window;
- suppressing background interactions behind the window.

These methods should delegate to `GunsmithGui` or `GunsmithQuickDrag` and contain no slot-registration, layout-packing, or inventory-rollback policy.

Keep the following in `GunsmithHiddenQuickSlotsPatch` because they enforce the managed-slot policy:

- hidden/conditional slot registration;
- mutation guards;
- original-layout capture and packed-layout caching;
- managed-slot visibility decisions;
- auto-put, swap, and direct-put inventory interception;
- managed/injected slot lookup helpers.

Update the native occupied-slot interception to call `GunsmithQuickDrag` directly instead of forwarding through `GunsmithGui`.

Harmony `PatchAll` already discovers attributed classes. Do not add a patch registry or manual initialization path.

### 6. Review The Result, Then Stop

After the move:

- search for every old drag-state field and confirm it has one owner;
- confirm `OriginalPartId` is gone;
- confirm the hidden-slot owner has no world-drop, character-input, cursor, or background-interaction patch methods;
- confirm its remaining GUI dependency is limited to the two existing read-only visibility checks;
- remove obsolete `using` directives and pass-through methods;
- update `ReadMe/Summary.md` only if its architecture/file-ownership description is now incorrect.

Do not continue into `GunsmithFabricatorClientPatch`, `GunsmithQuickAttachmentTransformService`, `GunsmithApi`, or unrelated large files because the Quick UI extraction went well. Those are separate risk domains and need their own evidence and phase.

## Explicit Non-Goals

- Do not implement or infer missing Phase 5 work.
- Do not split every large C# file or partial class.
- Do not rewrite the standard Gunsmith window, GUI parsing, formatting, or visuals.
- Do not redesign Quick-slot configuration, hidden-slot rules, anchors, or conditional visibility.
- Do not change item identifiers, allowed-item rules, slot indices, or inventory fallback order.
- Do not rename or change `GunsmithFrameworkSyncQuickContainer`.
- Do not change native networking events, custom message IDs, payloads, or server authority.
- Do not change persistence, Lua module structure, public hooks, or configuration schema.
- Do not add an inventory interface, controller hierarchy, command pattern, state machine framework, event bus, or dependency injection.
- Do not add a mocking package or any production dependency.
- Do not update deployed `bin` DLLs until the complete phase gate passes.

## Verification

### Automated

Run focused client tests while moving each boundary, then the complete suite:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithClientTest\GunsmithClientTest.csproj -c Release /p:UseSharedCompilation=false
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
Get-ChildItem .UnitTests -File -Filter *.lua | Sort-Object Name | ForEach-Object { lua $_.FullName }
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
git diff --check
```

Run structural checks:

```powershell
rg -n "pendingQuickDrag|handlingNativeQuickDragDrop|pendingNativeQuickDragDropClearItem" .AssemblyCSharpSource\GunsmithFramework\ClientProject\ClientSource
rg -n "OriginalPartId" .AssemblyCSharpSource\GunsmithFramework\ClientProject\ClientSource
rg -n "TryPutItem|RemoveItem|BeginQuickSlotMutation|EndQuickSlotMutation|GunsmithFrameworkSyncQuickContainer" .AssemblyCSharpSource\GunsmithFramework\ClientProject\ClientSource\GunsmithGui\GunsmithGuiQuickOverlay.cs
rg -n "HandleQuickOverlayDraggingBeforeWorldDrop|RestoreQuickOverlayDragAfterNativeDragging|BlockGunsmithWindow|KeepGunsmithWindowCursorLocal|SkipGunsmithWindowBackgroundInteractions" .AssemblyCSharpSource\GunsmithFramework\ClientProject\ClientSource\GunsmithQuick\GunsmithHiddenQuickSlotsPatch.cs
```

Expected structural results:

- the three transaction-state names resolve only to `GunsmithQuickDrag` and focused tests;
- `OriginalPartId` has no result;
- the overlay has no direct mutation, rollback, or sync call;
- the hidden-slot owner has none of the moved GUI/input endpoints.

Because production changes are client-only, build the three shipped clients:

```powershell
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\WindowsClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\LinuxClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\OSXClient.csproj -c Release /p:UseSharedCompilation=false
```

If implementation changes a shared project or shared project file, expand the gate to all three server targets as well. Existing documented compatibility warnings may remain; no new warning is allowed.

### Client In Game

Test each path with item identities/counts recorded before and after the action:

- drag a Quick item out and return it to its source slot;
- drag a Quick item to another empty Quick slot;
- swap two occupied Quick slots;
- drag an external allowed item into an empty Quick slot;
- replace an occupied Quick slot and return the displaced item to the exact source slot;
- replace an occupied Quick slot when exact-source restoration is unavailable and verify the controlled-inventory fallback;
- drop a Quick item onto an empty native inventory slot;
- drop it onto an occupied native slot and verify the displaced item enters the original Quick slot;
- drop it onto a combinable native stack and verify native combine remains in control;
- attempt an incompatible item, a full-inventory replacement, and a failed second insertion;
- release over the Gunsmith window, player inventory, another inventory, and the world;
- close the window, remove the weapon, and perform plugin cleanup while a drag is pending;
- verify failed operations retain or restore every item and play the current failure feedback;
- verify successful/cancelled operations produce the current Quick-container state once;
- verify conditional hidden-slot visibility, auto-put exclusion, layout packing, and Quick-slot lights are unchanged;
- verify cursor confinement and character/background input blocking are unchanged;
- verify ordinary non-Gunsmith inventories and the standard Gunsmith window remain unaffected.

Run the core matrix once in single-player/host mode and once from a remote multiplayer client. For multiplayer paths, confirm one logical action produces one server-authoritative result and no duplicate item or duplicate sync.

### Reload Regression

Perform two consecutive unload/reload cycles, including one with the Quick window open and a drag pending.

Verify:

- pending drag and delayed native-clear state do not survive unload;
- the item is restored or safely retained according to the existing cleanup contract;
- input patches fire once after reload;
- hidden-slot layouts and GUI resources still restore as required by Phase 4;
- a second cleanup call is harmless.

## Commit Sequence

Keep two reviewable rollback boundaries. Include tests with the behavior they protect.

1. `refactor(client): isolate Quick drag transactions`
   - add `GunsmithQuickDrag` and focused tests;
   - move drag state, mutations, rollback, reconciliation, and sync;
   - reduce the overlay to UI delegation;
   - remove GUI forwarders and dead `OriginalPartId` state;
   - update reload cleanup ownership.
2. `refactor(client): separate Quick overlay input patches`
   - add `GunsmithQuickOverlayInputPatch`;
   - move only GUI/input Harmony endpoints;
   - call the drag owner directly from native occupied-slot interception;
   - remove obsolete imports/delegation and update architecture documentation if needed.

Do not combine Fabricator, transform-service, networking, persistence, or Lua module work with either commit.

## Phase Gate

The phase is complete when:

- all pending Quick-drag transaction state has one owner in `GunsmithQuickDrag`;
- `QuickOverlayFrame` contains rendering/input feedback but no inventory mutation, rollback, mutation guard, or Lua sync implementation;
- `GunsmithGui` no longer stores transaction state or forwards native inventory transactions;
- `GunsmithHiddenQuickSlotsPatch` no longer owns world-drop, character-input, cursor, or background-interaction bridges;
- its only direct GUI dependency is the justified read-only Quick-window visibility query;
- `PendingQuickDrag.OriginalPartId` and all obsolete forwarders/usings are removed;
- success, failure, cancellation, native combine, native swap, close, removal, and reload behaviors match the baseline;
- no tested path loses or duplicates an item;
- focused and complete C# tests pass from a fresh build;
- all standalone Lua checks and Lua syntax checks pass;
- all three shipped client targets build in Release;
- single-player, remote-client, and two-cycle reload matrices pass;
- no new dependency, public API, hook name, wire-format change, or compiler warning is introduced;
- `git diff --check` passes.

## Expected Result

- Quick drag transaction ownership: one focused static class.
- Quick overlay: rendering and interaction feedback only.
- Hidden-slot owner: slot policy, visibility, layout, and enforcement rather than general GUI input handling.
- Direct hidden-slot-to-GUI references: reduced from 11 to the two justified visibility queries.
- Dead pending-drag state and GUI pass-through methods: removed.
- Total production code: approximately unchanged; the improvement is dependency direction and testability, not artificial line-count reduction.
- New abstractions and dependencies: none beyond the two concrete responsibility owners required by the existing behavior.
- Gameplay, saves, networking contracts, reload behavior, and public APIs: unchanged.
