local function read(path)
    local file = assert(io.open(path, "r"))
    local source = file:read("*a")
    file:close()
    return source
end

local composer = read(".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithComposer.cs")
local api = read(".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithApi.cs")

local _, directSpriteCount = composer:gsub("Sprite clone = new%(texture,", "")
assert(directSpriteCount == 2)
assert(not composer:find("Sprite clone = new(original)", 1, true))
assert(not composer:find("clone.texture = texture", 1, true))
assert(api:find("state.WorldSprite.Remove();", 1, true))
assert(api:find("state.InventorySprite.Remove();", 1, true))

print("Runtime sprites own their generated textures and leave Sprite.LoadedSprites on cleanup")
