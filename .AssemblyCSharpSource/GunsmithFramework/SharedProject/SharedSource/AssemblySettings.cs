global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Collections.Concurrent;
global using System.Collections.Immutable;
global using System.Reflection;
global using System.Reflection.Emit;
global using System.Runtime.CompilerServices;
global using System.Linq;
global using Barotrauma;
global using Barotrauma.LuaCs;
global using Barotrauma.LuaCs.Data;
global using Barotrauma.Extensions;
global using HarmonyLib;
global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;

[assembly: IgnoresAccessChecksTo("Barotrauma")]
[assembly: IgnoresAccessChecksTo("BarotraumaCore")]
[assembly: IgnoresAccessChecksTo("DedicatedServer")]
[assembly: InternalsVisibleTo("GunsmithSharedTest")]
[assembly: InternalsVisibleTo("GunsmithClientTest")]
[assembly: InternalsVisibleTo("GunsmithServerTest")]

namespace GunsmithFramework;
