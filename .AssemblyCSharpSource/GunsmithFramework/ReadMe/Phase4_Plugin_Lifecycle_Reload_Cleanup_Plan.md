# Phase 4: Plugin Lifecycle And Reload Cleanup Plan

## Goal

Make the C# plugin lifecycle repeatable and ownership-complete:

```text
Initialize -> OnLoadCompleted -> Dispose -> Initialize -> OnLoadCompleted
```

After `Dispose`, no plugin-owned callback, Harmony bridge, runtime registry, GUI mutation, or GPU resource from the disposed generation should remain active. A second `Dispose` must also be safe.

Preserve gameplay, save data, multiplayer authority, public Lua APIs, current hook names, wire formats, and configuration behavior.

This phase completes lifecycle cleanup only. It does not split the large Lua or C# modules.

## Preconditions And Baseline

- Phase 3 and the follow-up multiplayer-authority fixes are complete.
- The worktree contains no unrelated source edits.
- Test dependencies can be restored before implementation begins; a cached `--no-build --no-restore` result is not sufficient as the final gate.
- Perform the supported client and dedicated-server reload procedures once before editing and record any existing warnings or duplicate callbacks.

At plan-writing time:

- all six standalone Lua checks pass;
- cached Release test assemblies report 60 passing tests: 52 shared, 7 client, and 1 server;
- a fresh restore is locally blocked by NuGet `NU1301` TLS credential errors, so implementation must obtain a fresh successful restore/test result before completion.

Run before editing:

```powershell
dotnet restore .UnitTests\GunsmithTest\GunsmithTest.sln
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
Get-ChildItem .UnitTests -File -Filter *.lua | Sort-Object Name | ForEach-Object { lua $_.FullName }
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

## Lifecycle Invariants

Preserve these rules throughout the phase:

- The class that owns mutable static state also owns its `Clear`, `Reset`, or `Dispose` operation.
- Shared state is cleared once from the shared plugin lifecycle, not once in shared code and again in client code.
- External callbacks are detached before their target state is cleared.
- Harmony patches are removed before client-side visual and GUI state is restored.
- Client cleanup restores vanilla-visible state before disposing generated textures.
- Cleanup methods are idempotent and accept already-empty state.
- Registration tables are repopulated by the normal supported load path; reload must not depend on stale values from the previous generation.
- Stable networking message IDs continue to resolve to exactly one current callback after reload.
- No generic lifecycle interface, reset registry, service container, or cleanup framework is introduced.

## Confirmed Lifecycle Gaps

| Owner | Mutable state | Current cleanup | Confirmed gap |
| --- | --- | --- | --- |
| `GunsmithFramework` | `Package`, `harmonyInstance` | Harmony is unpatched and nulled | `Package` is not reset |
| `GunsmithRuntimeStates` | runtime states and effect cache | `Clear` exists | Client `GunsmithApi.Dispose` clears it a second time |
| `GunsmithQuickSlotCapacityPatch` | three registration/injected-slot dictionaries | none | Removed or changed registrations can survive same-generation reinitialization |
| `GunsmithQuickAttachmentBarrelTransforms` | five per-item rule/cache dictionaries | per-item `ClearTransforms` only | Server state and items without client sprite state have no global cleanup |
| `GunsmithErgonomicsAimPatch` | per-character aim runtime dictionary | opportunistic per-item removal only | Entries can survive until the declaring generation is collected |
| `GunsmithNpcPresetPatch` | pending preset list and preset weak table | none | Interrupted construction and same-generation reinitialization retain state; `ReportReady` is a no-op |
| `GunsmithQuickPartItemSpawner` | pending-spawn keys and client mutation delegates | callback-local removal only | Dispose cannot invalidate an in-flight spawn or detach the client delegates |
| `GunsmithQuickAttachmentBarrelSelectorPatch` | Harmony instance and reflected optional-VCE members | none | After `UnpatchSelf`, a second initialization can incorrectly treat the setter as still patched |
| `GunsmithGui` | window fields, icon-source cache, quick-overlay line texture | `CloseWindow` clears only an active window | The icon cache and separately allocated GPU texture are not released on plugin dispose |
| `GunsmithFabricatorClientPatch` | weak client-state table and directly replaced GUI handlers/layout | none | Unpatching Harmony does not restore delegates and GUI changes already attached to a live fabricator |

The following cleanup already exists and should be preserved rather than rewritten:

- `GunsmithLuaHooks.Clear`
- sprite restoration and generated sprite texture disposal in `GunsmithApi.Dispose`
- `GunsmithQuickSlotLightPatch.ClearAllState`
- `GunsmithHiddenQuickSlotsPatch.Reset`
- `GunsmithQuickSlotLayoutPatch.ClearAllLayouts`
- `GunsmithQuickAttachmentTransformService.ClearAllState`
- `GunsmithRuntimeStates.Clear`, including the runtime-effects cache

## Scope

### 1. Lock The Reload Contract With Focused Tests

Add one shared lifecycle test file:

```text
.UnitTests/GunsmithTest/GunsmithSharedTest/GunsmithLifecycleCleanupTests.cs
```

Use the existing xUnit projects and the existing reflection style from `GunsmithReloadCleanupTests`. Do not add production diagnostic properties solely for tests.

Cover the smallest behavior set that protects the lifecycle root cause:

- seed each shared mutable registry, run its owner-local reset, and verify it is empty;
- call every new reset twice and verify the second call is harmless;
- verify quick-part-spawner mutation delegates are null after reset;
- verify optional selector bridge fields are reset so the same setter can be patched by a later Harmony instance;
- seed `GunsmithFramework.Package`, call plugin `Dispose`, and verify it is null;
- verify shared plugin `Dispose` can be called twice without leaving runtime state.

Extend the existing client file:

```text
.UnitTests/GunsmithTest/GunsmithClientTest/GunsmithReloadCleanupTests.cs
```

Add focused assertions for:

- client cleanup remaining safe when no Gunsmith window is open;
- GUI caches being empty after reload cleanup;
- fabricator category handlers and layout values being restored;
- current hidden-slot layout restoration continuing to pass;
- calling client cleanup twice.

GPU disposal and actual Harmony patch counts still require in-game verification; do not build a fake graphics or Harmony framework for these cases.

### 2. Give Each Shared State Owner One Idempotent Reset

Add only owner-local operations:

- `GunsmithQuickSlotCapacityPatch.Reset`
  - clear `MaxQuickSlotByItemIdentifier`;
  - clear `QuickSlotTagsByItemIdentifier`;
  - clear `InjectedSlotsByItemIdentifier`.
- `GunsmithQuickAttachmentBarrelTransforms.ClearAllTransforms`
  - clear all five per-item dictionaries;
  - keep existing `ClearTransforms(Item)` for normal item removal.
- `GunsmithErgonomicsAimPatch.Reset`
  - clear the per-character runtime dictionary.
- `GunsmithNpcPresetPatch.Reset`
  - clear `PendingItemPresets` under its existing lock;
  - clear `Presets` under its existing lock.
- `GunsmithQuickPartItemSpawner.Reset`
  - invalidate pending work;
  - clear pending keys;
  - set `BeginQuickSlotMutation` and `EndQuickSlotMutation` to `null`.
- `GunsmithQuickAttachmentBarrelSelectorPatch.Reset`
  - under `PatchLock`, null the Harmony instance, optional component type, getter, and patched setter.

For in-flight quick-part spawns, use one integer generation token if the native spawn queue has no cancellation API. Capture the current token when queuing; after reset, an old callback must remove its spawned item and return without changing an inventory. Do not add a cancellation service or task abstraction.

Delete the empty `GunsmithNpcPresetPatch.ReportReady` method and its call after confirming it still has no behavior or initialization requirement.

Do not clear immutable constants or reflection caches that refer only to stable Barotrauma core types. The optional-VCE bridge is different because it stores a Harmony instance and its own patch status.

### 3. Make Shared `Dispose` The Single Cleanup Coordinator

Keep coordination in the existing shared `Plugin.cs`; do not create another lifecycle class.

Use this order:

1. remove registered Lua callbacks through `GunsmithLuaHooks.Clear`;
2. unpatch the current Harmony instance;
3. run client/server `DisposePlatform` cleanup;
4. reset the optional selector bridge and all shared mutable-state owners;
5. clear `GunsmithRuntimeStates` once;
6. set `Package` and `harmonyInstance` to `null`.

All owner reset methods must be idempotent so `Dispose` does not need state flags.

Remove `GunsmithRuntimeStates.Clear` from `GunsmithApi.Dispose`; the shared plugin lifecycle remains its only plugin-wide caller. Keep per-item `GunsmithRuntimeStates.Remove` calls used during ordinary item cleanup.

Do not introduce exception aggregation or a disposable collection. If an external teardown call is known to throw during testing, protect only that call so later state cleanup still runs and log the failure through the existing logger path.

### 4. Finish Client GUI And Resource Cleanup

Add one reload-only reset entry point to `GunsmithGui` while keeping `CloseWindow` for normal UI behavior.

The reset must:

- restore pending Quick-overlay buffers before detaching the window;
- clear window references even if `activeWindow` is already null;
- clear `partRows`, `warnedQuickAnchorPaths`, and `partIconSourceCache`;
- reset the active context, selection, preview, stats, Quick-mode, and drag state;
- dispose and null `QuickOverlayFrame.lineTexture` when it exists.

Call this reset from `GunsmithApi.Dispose` before disposing the shared sprite textures and `SpriteBatch`.

When restoring sprite state during dispose:

- skip vanilla-sprite mutation for an already removed item;
- always remove and dispose its generated state;
- allow one invalid item to be cleaned without preventing cleanup of the remaining entries.

Do not move GUI state into a new service or split the partial GUI class in this phase.

### 5. Restore Direct Fabricator UI Mutations

Harmony unpatching only stops future patch callbacks. It does not undo the category-button delegates and GUI tree changes already applied by `GunsmithFabricatorClientPatch`.

Extend the existing `ClientState` with only the original values that the patch overwrites:

- original category-button `OnClicked` handlers and sizes;
- original input-holder relative size;
- original weapon-container inventory/UI flags and `RectTransform` reference.

Add `GunsmithFabricatorClientPatch.Reset` that iterates the live weak-table entries and:

1. restores category-button handlers and sizes when those controls still exist;
2. detaches the Gunsmith category button and weapon-area nodes;
3. restores the input-holder size;
4. restores the weapon-container flags and inventory transform;
5. recalculates the parent layout;
6. clears the weak table.

Call it from client dispose before texture/resource disposal.

Do not recreate the complete vanilla Fabricator GUI, copy vanilla `CreateGUI`, or add a generic UI mutation tracker. Restore only values this patch changes.

### 6. Verify Re-registration Without Adding A New Protocol

After cleanup, execute the actual supported reload path and confirm the normal load sequence repopulates:

- Lua hook callbacks;
- weapon tags;
- hidden/conditional Quick-slot registrations;
- Quick-slot capacity registrations;
- optional selector patch state;
- client and server part-change networking receivers.

The native networking API exposes registration by message ID but no removal operation in the current references. Keep the existing stable IDs and verify that re-registering them replaces the old callback by observing one server mutation and one client state response after multiple reloads.

Do not invent an unregister call or add a networking wrapper. If the supported runtime demonstrably retains multiple receivers for one ID, stop this phase at that finding and handle the networking lifecycle as a separate correctness change with LuaCs source/API evidence.

Do not add a new C#-reload Lua event unless the real supported reload path fails to replay the existing registration functions. If replay is missing, add one narrowly named existing-module hook that calls the current registration functions; do not duplicate their logic.

## Explicit Non-Goals

- Do not split `Runtime.lua`, `Core.lua`, `Validation.lua`, `GunsmithGui`, `GunsmithFabricatorClientPatch`, or the anchor editor.
- Do not migrate deprecated `ILuaCsHook.Add` usage in this phase.
- Do not merge the component-event and custom-message multiplayer paths.
- Do not change networking message IDs, payload order, validation, or server authority.
- Do not change persistence JSON, save version, or the 8192-character limit.
- Do not change public Lua package/configuration APIs.
- Do not change Quick-slot, NPC preset, fabricator filtering, ergonomics, sprite, or attachment-transform gameplay rules.
- Do not add a dependency, lifecycle interface, reset registry, service locator, or dependency-injection layer.
- Do not update deployed `bin` DLLs until the implementation passes the phase gate.

## Verification

### Automated

Run the focused tests after each owner group changes, then the complete suite:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
Get-ChildItem .UnitTests -File -Filter *.lua | Sort-Object Name | ForEach-Object { lua $_.FullName }
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
git diff --check
```

Do not accept `--no-build` results as the phase gate.

Because shared lifecycle code ships everywhere, build all six Release targets:

```powershell
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\WindowsClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\WindowsServer.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\LinuxClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\LinuxServer.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\OSXClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\OSXServer.csproj -c Release /p:UseSharedCompilation=false
```

The existing compatibility-hook obsolete warning may remain. No new warning is allowed.

### Client In Game

Perform at least two consecutive unload/reload cycles, including one while a Gunsmith window is open and one while an enabled fabricator UI is open.

Verify:

- the Gunsmith window closes cleanly and reopens after reload;
- no disposed texture, sprite, or `SpriteBatch` exception occurs;
- vanilla sprites are restored while unloaded and reapplied once after reload;
- Quick-slot layouts and lights restore while unloaded and apply once after reload;
- no orphan Gunsmith fabricator button or weapon slot remains while unloaded;
- vanilla fabricator category buttons work while unloaded and after reload;
- weapon tags, hidden slots, conditional visibility, and injected capacity are registered once;
- optional VCE projectile selection updates the barrel transform after the second reload;
- each part-change click produces one server request and one client response;
- NPC presets and pending Quick-part spawns do not fire from a disposed generation.

### Dedicated Server In Game

Perform at least two plugin reloads and verify:

- each Lua hook and networking receiver handles an event once;
- runtime stats and managed-item effects are reapplied once;
- Quick-slot capacity registration and barrel transforms are rebuilt once;
- no stale NPC preset or pending Quick-part state is applied after unload;
- client part changes remain server-authoritative and replicate once;
- shutdown and a second `Dispose` produce no exception.

## Commit Sequence

Keep two rollback boundaries, with the relevant tests in the same commit as the behavior they protect:

1. `fix: reset shared plugin state on reload`
   - owner-local shared reset methods;
   - optional selector reinitialization;
   - quick-spawn invalidation;
   - shared `Dispose` ordering and duplicate-clear removal;
   - shared lifecycle tests.
2. `fix(client): restore UI state during plugin reload`
   - Gunsmith GUI cache/resource cleanup;
   - safe sprite cleanup;
   - fabricator mutation restoration;
   - client reload tests.

Do not combine hook API migration, networking redesign, or module splitting with either commit.

## Phase Gate

The phase is complete when:

- every mutable shared static field is either reset by its owner or explicitly documented as immutable/process-lifetime state;
- shared runtime state is cleared exactly once by plugin-wide dispose;
- `Package`, optional Harmony bridge references, pending delegates, and runtime registries are empty after dispose;
- GUI handlers, layouts, sprite state, lights, and GPU resources are restored or disposed by the client owner;
- `Dispose` is safe when called twice;
- initialization after dispose registers one current hook/patch/receiver set;
- the optional selector setter is patched after the second initialization;
- focused and complete C# tests pass from a fresh build;
- all standalone Lua checks and Lua syntax checks pass;
- all six shipped C# targets build in Release;
- two-cycle client and dedicated-server reload matrices pass;
- no new dependency, public API, wire-format change, or compiler warning is introduced;
- `git diff --check` passes.

## Expected Result

- Plugin lifecycle: repeatable and idempotent.
- Shared static-state ownership: explicit and complete.
- Duplicate plugin-wide runtime clear: removed.
- Optional Harmony bridge: re-patchable after reload.
- In-flight Quick-part work: unable to mutate state after disposal.
- Client GUI and fabricator mutations: restored on unload.
- Generated client GPU resources: disposed by their owner.
- New dependencies and lifecycle abstractions: none.
- Gameplay, saves, networking contracts, and public APIs: unchanged.
