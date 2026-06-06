local platforms = GunsmithFramework.Config.platforms

platforms.example_platform = {
    canvas = { w = 128, h = 64 },
    rootSlots = {
        receiver = { required = true }
    },
    pathNameKeys = {
        receiver = "example.gunsmith.path.receiver"
    }
}
