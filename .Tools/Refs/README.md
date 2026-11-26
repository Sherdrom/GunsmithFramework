# Unstable build references

Compile-time references copied from Deep-Diving-Armory commit `028f013f`
(`fix: adapt DDA to Unstable game and plugin-based LuaCs`). Do not copy these
publicized assemblies over the game installation.

- Windows: Barotrauma **1.15.25.0**, publicized from the installed game in that snapshot.
  Event backing fields retain private visibility to avoid colliding with their public events.
- Linux and OSX: `Barotrauma.ReferenceAssemblies.Vanilla.Publicized` **1.15.24**;
  upstream `Mac` is stored as `OSX` here.
- Farseer, Lidgren, MonoGame and XNATypes: `Barotrauma.ReferenceAssemblies.Libraries` **1.15.24**.
- LuaCs client/server and MoonSharp: workshop package **3693624471**, snapshot
  from **2026-11-04**, with separate LuaCs assemblies for all six targets.
- `System.Collections.Immutable.dll`: **9.0.0.0**, required by the new game and
  LuaCs assemblies while this mod targets .NET 8.

Update all platforms and both targets together. Build all six projects under
`.AssemblyCSharpSource/GunsmithFramework` and run `.UnitTests/GunsmithTest/GunsmithTest.sln`.
Builds and offline tests do not establish in-game or multiplayer behavior.
