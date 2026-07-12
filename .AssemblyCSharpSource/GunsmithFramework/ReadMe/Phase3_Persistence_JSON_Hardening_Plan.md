# Phase 3: Persistence JSON Hardening Plan

## Goal

Replace the hand-written saved-state JSON codec with the JSON support already provided by LuaCs, and validate client-submitted saved state as real JSON at the C# server trust boundary.

Keep the existing version 1 save shape, deterministic path order, maximum length, public Lua signatures, and domain-level compatibility filtering unchanged.

## Preconditions

- Phase 2 is complete or its remaining work is explicitly deferred.
- All current C# and Lua checks pass.
- At least one real version 1 saved-state string is retained as a compatibility fixture.
- The worktree contains no unrelated source edits.

Run before editing:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
lua .UnitTests\SharedLuaHelpersTest.lua
lua .UnitTests\QuickModClearSlotTest.lua
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

## Format Invariants

The persisted representation remains:

```json
{"v":1,"parts":{"receiver":"part_id","receiver/barrel":"barrel_id"}}
```

Preserve these rules:

- An empty string means no saved state and remains valid at the network layer.
- A non-empty saved state must be a version 1 JSON object.
- The root object contains `v` and `parts`.
- `v` is the number `1`.
- `parts` is an object whose property names and values are non-empty strings.
- Root paths are written first in platform order.
- Nested paths are written in the existing depth-then-name order.
- Missing optional roots are stored as `Gunsmith.EmptyPartId`.
- The maximum network/save length remains 8192 characters.
- Lua remains responsible for checking whether paths, parts, owners, and compatibility rules are valid for the current configuration.

Do not automatically rewrite or erase a non-empty malformed saved state while loading it. Invalid client submissions are rejected, but pre-existing malformed data remains available for diagnosis until an explicit valid save replaces it.

## Current Problems

### Lua Encoding

`Persistence.jsonEscape` only escapes backslashes and quotes. It does not correctly encode control characters such as newlines, carriage returns, or tabs.

### Lua Decoding

`Persistence.Decode` extracts JSON with Lua patterns. The pattern stops at escaped quotes, does not validate `v`, and can accept malformed or partial input.

The parameter is currently named `json`, which must be renamed before calling the global LuaCs `json.parse` API.

### C# Trust-Boundary Validation

`GunsmithData.IsValidSavedState` currently checks only length, prefix, and suffix. A malformed payload can pass that shape check, be stored, and then fail later in Lua.

## Scope

### 1. Add A Focused Standalone Lua Check

Add:

```text
.UnitTests/PersistenceJsonTest.lua
```

Use plain `assert`. Inject a small fake `json` table before loading `Persistence.lua` so the test verifies integration with `json.serialize` and `json.parse` without implementing another JSON parser.

The fake should:

- record the string in each one-element table passed to `json.serialize`
- return predetermined one-element JSON arrays for the test values
- return a version 1 table from `json.parse`
- throw for a malformed-input case so `pcall` behavior is exercised

Cover:

- deterministic root and nested-path ordering
- empty optional root encoded as `Gunsmith.EmptyPartId`
- keys and values routed through `json.serialize`
- valid version 1 input decoded into a new sanitized parts table
- empty string rejected by `Persistence.Decode`
- malformed JSON rejected
- unsupported version rejected
- missing/non-table `parts` rejected
- non-string or empty part entries ignored

The test must not contain a second production-style JSON encoder or decoder.

### 2. Use Native JSON String Encoding

In `Lua/Scripts/Gunsmith/Persistence.lua`:

1. Delete `jsonEscape` and `jsonUnescape`.
2. Keep the existing deterministic entry ordering.
3. Encode each path and part id by serializing a one-element table, then use its already-quoted array element.
4. Continue assembling the small version 1 envelope manually so property ordering remains deterministic.

The resulting entry should be assembled from already-quoted JSON strings:

```lua
local function encodeString(value)
    local encoded = json.serialize({ tostring(value) })
    return string.sub(encoded, 2, -2)
end
```

Do not serialize the whole `parts` table directly; Lua table iteration order is not the persistence contract.

### 3. Replace Pattern Decoding With `json.parse`

Rename the `Persistence.Decode` parameter from `json` to `value` or `savedState`, then:

1. Reject non-string and empty input.
2. Call `json.parse` through `pcall`.
3. Require a table result.
4. Require `data.v == 1`.
5. Require `data.parts` to be a table.
6. Copy only non-empty string keys with non-empty string values into a new table.
7. Return the sanitized table, including an empty table for a valid empty `parts` object.
8. Return `nil` for malformed or unsupported data.

Do not return `data.parts` directly. Copying prevents unexpected JSON values or shared parser state from leaking into selection logic.

### 4. Preserve Existing Load Semantics

Keep the current call structure in:

- `Persistence.Receive`
- `Runtime.applyServerSavedSelection`
- `NpcPresets.TryApply`

In particular:

- A non-empty raw saved state remains authoritative during load, even if decoding fails, so it is not silently overwritten by container synchronization.
- An existing non-empty saved state continues to block automatic NPC preset replacement.
- `ApplySavedParts` remains defensive when passed `nil`.
- Unknown or incompatible decoded entries continue to be ignored by `Core.IsValidPath` and `Core.IsPartCompatible`.

This phase fixes parsing and rejection; it does not add automatic recovery policy.

### 5. Validate Real JSON In C#

Update `GunsmithData.IsValidSavedState` to use `System.Text.Json.JsonDocument`, which is part of .NET 8 and requires no new package.

Validation rules:

- reject strings longer than 8192 characters before parsing
- accept the empty string
- require a JSON object root
- require exactly one numeric `v` property equal to `1`
- require exactly one object `parts` property
- reject unknown or duplicate top-level properties for the version 1 schema
- require every `parts` property name to be non-empty/non-whitespace
- require every `parts` value to be a non-empty/non-whitespace JSON string
- reject malformed JSON, comments, trailing commas, arrays, nulls, and wrong value types
- catch `JsonException` and return `false`

Do not reproduce Lua domain validation in C#. The server boundary validates syntax and the saved-state schema; Lua validates whether the requested parts are meaningful and compatible.

Leave `GunsmithDataAccess.SetSavedState` unchanged. It is a compatibility bridge for trusted local Lua and older assembly generations, not the untrusted client event boundary.

### 6. Extend C# Saved-State Tests

Update `GunsmithDataTests`.

Accept:

- empty string
- current compact version 1 fixture
- valid nested paths
- escaped quotes, backslashes, control characters, and Unicode
- semantically equivalent JSON with harmless whitespace/property order differences

Reject:

- malformed JSON that still has the old prefix/suffix
- wrong or string version
- missing `v` or `parts`
- duplicate or unknown root properties
- `parts` as array/null/string
- numeric, boolean, object, array, or null part values
- empty/whitespace path or part id
- trailing comma or comment
- payload longer than 8192 characters

Keep the existing normalization and older-assembly-generation tests.

### 7. Exercise The Real LuaCs JSON Implementation

The standalone `lua` executable does not provide the LuaCs `json` global, so the fake-backed unit check is not enough by itself.

Add a small persistence round-trip block to the existing `Validation.RunSelfTest` command path. It should use the real runtime `json.serialize` and `json.parse` to assert:

- a version 1 encode/decode round trip
- nested paths
- quotes and backslashes
- newline/tab characters
- Unicode
- malformed and wrong-version rejection

Reuse the existing `GunsmithFrameworkValidationSelfTest` command. Do not add another console command or public self-test API.

## Explicit Non-Goals

- Do not introduce save version 2.
- Do not migrate UI or Lua/C# display specs to JSON.
- Do not add a JSON NuGet package or Lua library.
- Do not serialize the whole unordered Lua table.
- Do not change path sorting or root ordering.
- Do not change `Persistence.Encode`, `Decode`, or `ApplySavedParts` public signatures except renaming a local parameter.
- Do not move compatibility rules into C#.
- Do not automatically repair, erase, or overwrite malformed existing saves.
- Do not change NPC preset overwrite policy.
- Do not change the 8192-character limit.
- Do not change networking event types or message order.
- Do not combine this work with `Dispose`, hook migration, or module splitting.

## Verification

### Automated

Run after the Lua codec change:

```powershell
lua .UnitTests\PersistenceJsonTest.lua
lua .UnitTests\SharedLuaHelpersTest.lua
lua .UnitTests\QuickModClearSlotTest.lua
Get-ChildItem Lua -Recurse -Filter *.lua | ForEach-Object { luac -p $_.FullName }
```

Run after the C# validator change:

```powershell
dotnet test .UnitTests\GunsmithTest\GunsmithTest.sln -c Release /p:UseSharedCompilation=false
```

Because shared C# changes ship on every target, finish with all six Release builds:

```powershell
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\WindowsClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\WindowsServer.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\LinuxClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\LinuxServer.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ClientProject\OSXClient.csproj -c Release /p:UseSharedCompilation=false
dotnet build .AssemblyCSharpSource\GunsmithFramework\ServerProject\OSXServer.csproj -c Release /p:UseSharedCompilation=false
```

### In Game

Verify:

- an existing real version 1 weapon state loads unchanged
- saving the same selection produces stable output on repeated saves
- standard and Quick UI selections survive save/reload
- NPC presets still do not overwrite non-empty saved state
- client save requests with valid state replicate through the server
- malformed client state is rejected and the previous server state is rebroadcast
- `GunsmithFrameworkValidationSelfTest` passes its persistence assertions with the real LuaCs JSON implementation
- a malformed pre-existing state is not silently overwritten during load

## Commit Sequence

Keep two rollback boundaries:

1. `fix(lua): use native JSON for persisted state`
   - Lua codec
   - standalone Lua check
   - real LuaCs round-trip self-test
2. `fix: validate saved-state JSON at the server boundary`
   - `GunsmithData.IsValidSavedState`
   - expanded C# tests

Do not combine persistence behavior with unrelated cleanup.

## Phase Gate

The phase is complete when:

- hand-written JSON escaping, unescaping, and pattern parsing are removed
- version 1 output order and shape remain deterministic
- valid old version 1 fixtures decode successfully
- malformed and unsupported Lua input returns `nil`
- client-submitted state is parsed and schema-validated before storage
- Lua sanitization still filters entries before domain application
- standalone Lua checks pass
- the real LuaCs JSON round-trip self-test passes in game
- all C# tests pass
- all six shipped C# targets build in Release
- multiplayer valid-save and invalid-save behavior matches the plan
- no new dependency or public API is introduced
- `git diff --check` passes

## Expected Result

- Save version: unchanged at 1.
- Valid existing saves: compatible.
- Deterministic output: preserved.
- Hand-written JSON parser: removed.
- Server shape-only check: replaced with real schema validation.
- New dependencies: none.
- Automatic corrupt-save rewriting: none.
