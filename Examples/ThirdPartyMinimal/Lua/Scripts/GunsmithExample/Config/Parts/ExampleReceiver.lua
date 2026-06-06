local parts = GunsmithFramework.Config.parts

parts.example_receiver = {
    type = "receiver",
    nameKey = "example.gunsmith.part.receiver",
    provides = { "example_receiver" },
    item = { virtual = true },
    visual = {
        texture = "%ModDir%/example_receiver.png",
        source = { x = 0, y = 0, w = 128, h = 64 },
        attachPoint = { x = 64, y = 32 }
    }
}
