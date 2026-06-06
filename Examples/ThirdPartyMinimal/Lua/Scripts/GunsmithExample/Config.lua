local package = GunsmithFramework.CurrentPackage()

GunsmithFramework.RegisterPlatform(package, "example_platform", {
    canvas = { w = 128, h = 64 },
    rootSlots = {
        receiver = { required = true }
    },
    pathNameKeys = {
        receiver = "example.gunsmith.path.receiver"
    }
})

GunsmithFramework.RegisterPart(package, "example_receiver", {
    type = "receiver",
    nameKey = "example.gunsmith.part.receiver",
    provides = { "example_receiver" },
    item = { virtual = true },
    visual = {
        texture = "%ModDir%/example_receiver.png",
        source = { x = 0, y = 0, w = 128, h = 64 },
        attachPoint = { x = 64, y = 32 }
    }
})

GunsmithFramework.RegisterWeapon(package, "example_weapon_identifier", {
    platform = "example_platform",
    roots = {
        receiver = { part = "example_receiver" }
    }
})
