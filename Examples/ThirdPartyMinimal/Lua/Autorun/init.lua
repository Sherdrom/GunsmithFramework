local package = {
    modDir = ...,
    entry = "Lua/Scripts/GunsmithExample/Config.lua"
}

GunsmithFramework = GunsmithFramework or {}
if GunsmithFramework.RegisterPackage then
    GunsmithFramework.RegisterPackage(package)
else
    GunsmithFramework.PendingPackages = GunsmithFramework.PendingPackages or {}
    table.insert(GunsmithFramework.PendingPackages, package)
end
