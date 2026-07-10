# Phase 1: Dead Code And Dependency Cleanup Plan

## Goal

Remove code and dependencies that have no confirmed production use without changing gameplay, public Lua APIs, save data, networking, UI behavior, or plugin lifecycle behavior.

This phase is deletion-only. Refactoring shared helpers, changing persistence, fixing `Dispose`, migrating hooks, and splitting large modules belong to later phases.

## Baseline

Before editing, record the current baseline:

- C# tests: 26 passing.
- Lua `QuickMod.ClearSlot` self-check: passing.
- First-party Lua files: passing `luac -p`.
- Windows client/server builds: passing with the known `ILuaCsHook.Add` deprecation warnings.
- Worktree: clean except for the planned phase changes.

Run:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
lua .UnitTests\QuickModClearSlotTest.lua
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

## Scope

### 1. Remove Confirmed Dead C# Parsing Code

Delete the unused private methods from `ClientProject/ClientSource/GunsmithComposer.cs`:

- `ParseRuntimeStats`
- `ParseIdentifierSet`

The live implementations are already owned by `GunsmithRuntimeStates.CreateState` in `SharedProject/SharedSource/GunsmithRuntimeState.cs`. Confirm immediately before deletion that the client copies still have no callers.

Expected reduction: about 49 lines.

Verification:

```powershell
rg -n "ParseRuntimeStats\(|ParseIdentifierSet\(" .AssemblyCSharpSource\GunsmithFramework --glob '*.cs' --glob '!**/bin/**' --glob '!**/obj/**'
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
```

After the edit, both parser names should only appear in `GunsmithRuntimeState.cs`.

### 2. Remove Confirmed Dead Internal Members

Delete these members after a final caller search:

- `GunsmithApi.TryGetRuntimeState`
- `GunsmithRuntimeStats.Get`
- `GunsmithRuntimeStats.HasValue`

Keep `GunsmithRuntimeStats.TryGet`; it is used by runtime stat application.

Do not remove public Lua functions merely because this repository has no callers. Third-party mods may call them, and public API cleanup requires a separate compatibility decision.

Expected reduction: about 9 lines.

Verification:

```powershell
rg -n "TryGetRuntimeState|\.Get\(StatTypes|\.HasValue\(StatTypes" .AssemblyCSharpSource\GunsmithFramework .UnitTests --glob '*.cs' --glob '!**/bin/**' --glob '!**/obj/**'
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
```

### 3. Test Direct Package References One At A Time

The production source does not directly reference these packages from `Build.props`:

1. `OneOf`
2. `FluentResults`
3. `LightInject`

Remove only one package per commit, then restore, build, test, and launch the plugin before trying the next package. A package with no source-level use may still be needed while resolving types exposed by the LuaCs plugin loader.

Use this order because `LightInject` is the most likely to participate in plugin service injection.

For each candidate:

1. Remove one `PackageReference`.
2. Delete no cache or lock files manually.
3. Restore and build the Windows client and server.
4. Run all tests.
5. Launch Barotrauma and confirm the client and server plugin both initialize.
6. Keep the removal only if every check passes.

Build commands:

```powershell
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\WindowsClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\WindowsServer.csproj -c Release /p:UseSharedCompilation=false
```

After all accepted removals, build the Linux and OSX client/server targets as the phase gate.

### 4. Evaluate And Optionally Remove PreJIT

`GunsmithApi.Initialize` currently calls `PreJitFirePathMethods`, which reflects over seven methods and calls `RuntimeHelpers.PrepareMethod`. The repository contains no benchmark or documentation showing that this optimization is required.

Handle this as a separate optional commit:

1. Record first-use behavior before removal, especially the first shot, first managed affliction, and first quick-attachment particle effect.
2. Remove the `PreJitFirePathMethods` call and both PreJIT helper methods.
3. Build and run the same scenarios again.
4. Keep the deletion unless a repeatable regression is observed.

Do not replace it with another warm-up framework or configuration flag.

Expected reduction: about 31 lines.

## Explicit Non-Goals

- Do not merge `GunsmithStats` and `GunsmithRuntimeStats`.
- Do not merge `IsFinite` or `Rotate` helpers in this phase.
- Do not change Lua/C# wire formats.
- Do not change persistence JSON.
- Do not change `Dispose` or static registries.
- Do not migrate deprecated LuaCs hooks.
- Do not split `Runtime.lua`, `Core.lua`, `GunsmithGui`, or the anchor editor.
- Do not remove or rename public Lua API members.

## Commit Sequence

Keep each rollback boundary small:

1. `refactor: remove duplicate unused runtime parsers`
2. `refactor: remove unused internal runtime members`
3. `build: remove unused OneOf reference` if verified
4. `build: remove unused FluentResults reference` if verified
5. `build: remove unused LightInject reference` if verified
6. `perf: remove unproven prejit warmup` if verified

Do not combine failed package experiments into the final history.

## Phase Gate

The phase is complete when:

- All confirmed dead code is deleted.
- Each retained package has a demonstrated build or runtime reason to remain.
- PreJIT is either deleted or retained with a recorded repeatable reason.
- C# tests pass.
- Lua self-check and syntax checks pass.
- All six shipped C# targets build in Release.
- Client and server plugins initialize in game.
- No new compiler warnings are introduced.
- `git diff --check` passes.

## Expected Result

- Confirmed reduction: about 58 lines.
- Optional additional reduction: about 31 lines from PreJIT.
- Possible dependency reduction: zero to three direct packages, determined by build and in-game verification.
- Behavior and public APIs remain unchanged.
