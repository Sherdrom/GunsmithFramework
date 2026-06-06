local package = GunsmithFramework.CurrentPackage()
local configPath = package.modDir .. "/Lua/Scripts/GunsmithExample/Config"

dofile(configPath .. "/Platforms/ExamplePlatform.lua")
dofile(configPath .. "/Parts/ExampleReceiver.lua")
dofile(configPath .. "/Weapons/ExampleWeapon.lua")
