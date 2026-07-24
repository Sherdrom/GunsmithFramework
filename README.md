# GunsmithFramework

GunsmithFramework 是一个 Barotrauma 模组框架，用 Lua 描述枪械平台、武器、部件、挂点、Quick Slot 和 NPC 预设，用 C# 负责游戏内 UI、合成贴图、保存状态、Quick Slot 容器桥接和运行时属性。

这份 README 面向第三方模组作者。目标是让你从零创建自己的改枪模组，而不是修改 GunsmithFramework 本体。

## 需要准备

- Barotrauma 已启用 LuaCs / Lua For Barotrauma。
- GunsmithFramework 作为独立模组启用，并且加载顺序早于你的第三方枪械模组。
- 你的模组拥有自己的 `ModConfig.xml`、`filelist.xml`、XML 物品文件、Lua 配置文件和本地化文本。

第三方模组只需要调用 GunsmithFramework 暴露的 Lua API：

```lua
GunsmithFramework.RegisterPackage({
    modDir = ...,
    entry = "Lua/Scripts/MyGunsmithMod/Config.lua"
})
```

不要直接改 `Lua/Scripts/Gunsmith/*.lua`。

## 快速开始

建议目录结构：

```text
MyGunsmithMod/
  ModConfig.xml
  filelist.xml
  Items/Weapons.xml
  Items/Parts.xml
  text/English.xml
  Lua/Autorun/init.lua
  Lua/Scripts/MyGunsmithMod/Config.lua
  Lua/Scripts/MyGunsmithMod/Config/Platforms/ExamplePlatform.lua
  Lua/Scripts/MyGunsmithMod/Config/Parts/ExampleParts.lua
  Lua/Scripts/MyGunsmithMod/Config/Weapons/ExampleWeapon.lua
```

`ModConfig.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModConfig>
  <Lua File="%ModDir%/Lua/Autorun/init.lua" IsAutorun="true" />
</ModConfig>
```

`filelist.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<contentpackage name="My Gunsmith Mod" modversion="1.0.0" corepackage="False" gameversion="1.12.7.0">
  <Item file="%ModDir%/Items/Weapons.xml" />
  <Item file="%ModDir%/Items/Parts.xml" />
  <Text file="%ModDir%/text/English.xml" />
</contentpackage>
```

`Lua/Autorun/init.lua`：

```lua
GunsmithFramework.RegisterPackage({
    modDir = ...,
    id = "my_gunsmith_mod",
    name = "My Gunsmith Mod",
    entry = "Lua/Scripts/MyGunsmithMod/Config.lua",
    localizationFiles = { "text/English.xml" }
})
```

`Lua/Scripts/MyGunsmithMod/Config.lua`：

```lua
local package = GunsmithFramework.CurrentPackage()
local configPath = package.modDir .. "/Lua/Scripts/MyGunsmithMod/Config"

dofile(configPath .. "/Platforms/ExamplePlatform.lua")
dofile(configPath .. "/Parts/ExampleParts.lua")
dofile(configPath .. "/Weapons/ExampleWeapon.lua")
```

## 最小可运行示例

这个例子只有一个不可拆空的 `receiver` 根部件，能通过框架识别、合成贴图、打开 UI、保存状态。

`Config/Platforms/ExamplePlatform.lua`：

```lua
local platforms = GunsmithFramework.Config.platforms

platforms.my_platform = {
    canvas = { w = 128, h = 64 },
    rootSlots = {
        { path = "receiver" }
    },
    pathNameKeys = {
        receiver = "my.gunsmith.path.receiver"
    }
}
```

`Config/Parts/ExampleParts.lua`：

```lua
local parts = GunsmithFramework.Config.parts

parts.my_receiver = {
    type = "receiver",
    nameKey = "my.gunsmith.part.receiver",
    provides = { "my_receiver" },
    item = { virtual = true },
    visual = {
        texture = "%ModDir%/Items/my_receiver.png",
        source = { x = 0, y = 0, w = 128, h = 64 },
        attachPoint = { x = 64, y = 32 },
        order = 0,
        scale = 1.0
    }
}
```

`Config/Weapons/ExampleWeapon.lua`：

```lua
local weapons = GunsmithFramework.Config.weapons

weapons.my_rifle = {
    platform = "my_platform",
    roots = {
        receiver = {
            part = "my_receiver",
            socket = { x = 64, y = 32 }
        }
    },
    preview = {
        padding = 12,
        scale = 1.0,
        offset = { x = 0, y = 0 }
    },
    inventory = {
        scale = 1.0,
        rotation = 0,
        padding = 0
    },
    world = {
        scale = 1.0,
        rotation = 0,
        padding = 0,
        offset = { x = 0, y = 0 }
    }
}
```

`Items/Weapons.xml` 里对应的武器 item 必须有 `<GunsmithData />`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Items>
  <Item identifier="my_rifle" nameidentifier="entityname.my_rifle" category="Weapon" tags="weapon,gun,gunsmith">
    <Sprite texture="%ModDir%/Items/my_receiver.png" sourcerect="0,0,128,64" depth="0.55" />
    <Body width="128" height="32" />
    <Holdable slots="RightHand,LeftHand" controlpose="true" holdpos="0,0" aimpos="0,0" />
    <RangedWeapon barrelpos="64,32" />
    <GunsmithData />
  </Item>
</Items>
```

`text/English.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<infotexts language="English">
  <entityname.my_rifle>My Rifle</entityname.my_rifle>
  <my.gunsmith.path.receiver>Receiver</my.gunsmith.path.receiver>
  <my.gunsmith.part.receiver>Example Receiver</my.gunsmith.part.receiver>
</infotexts>
```

进游戏后运行控制台命令：

```text
GunsmithFrameworkValidate
```

如果输出 `0 errors`，拿起 `my_rifle` 按 `G` 应能打开完整改装 UI。

## Lua 类型约定

文档中的复合类型写法如下：

| 名称 | Lua 写法 | 说明 |
| --- | --- | --- |
| point | `{ x = 0, y = 0 }` | 坐标点或偏移。 |
| rect | `{ x = 0, y = 0, w = 128, h = 64 }` | 图片裁切矩形。 |
| string array | `{ "a", "b" }` | 必须是连续数组，不能写成键值表。 |
| path | `"receiver/barrel/muzzle"` | root slot 和 mount path 拼出的选择路径。 |

## 制作流程

推荐顺序：

1. 先写 XML 武器和部件物品，确认 Barotrauma 能加载你的模组。
2. 给 Gunsmith 武器 XML item 加 `<GunsmithData />`。
3. 写 `platform`，先只做一个 root slot。
4. 写 root part 和 weapon `roots`，让默认武器能显示。
5. 用 `GunsmithAnchorEditor/index.html` 取 `visual.source`、`visual.attachPoint`、`roots[path].socket`、`mount.anchor`。
6. 给 root part 增加 `mounts`，再逐步增加枪管、护木、枪托、瞄具等部件。
7. 需要快速改装时，再添加 `mount.quick` 和 `weapon.quickSlotBindings`。
8. 进游戏运行 `GunsmithFrameworkValidate`，再打开 UI 和 Quick UI 做最终检查。

坐标规则：

```text
root draw offset = root.socket - root visual.attachPoint * root visual.scale
child anchor = parent draw offset + (parent visual.attachPoint + mount.anchor) * parent visual.scale
child draw offset = child anchor - child visual.attachPoint * child visual.scale
```

如果不用 `visual.attachPoint`，也可以用 `visual.relativeOffset`，含义是相对锚点的绘制偏移。

## 玩家交互

- 持有 Gunsmith 武器时按 `G` 打开完整改装 UI。
- 武器有 Quick Slot 时按 `Shift+G` 打开快速改装 UI。
- 选择有 `part.item.identifier` 的部件时，框架会从角色物品栏寻找同 identifier 的真实 XML 物品，安装时消耗，拆除或替换时返还。
- `item = { virtual = true }` 的部件不需要真实物品，适合默认结构件、不可掉落内部件或纯视觉件。
- Quick Slot 使用武器自己的 `ItemContainer` 槽位。Lua 配置中的 `slot` 是从 `0` 开始的槽位索引。

## 包级配置

`GunsmithFramework.RegisterPackage(package)` 可接收字符串或 table。字符串会被视为 `entry`。

```lua
GunsmithFramework.RegisterPackage({
    modDir = ...,
    id = "my_pack",
    name = "My Pack",
    entry = "Lua/Scripts/MyPack/Config.lua",
    localizationFiles = { "text/English.xml", "text/Chinese.xml" },
    -- 可选：只在想覆盖框架 UI 文案前缀时填写。
    -- localizationPrefix = "my.gunsmith",
    -- 可选：只在需要引用其他 Gunsmith package 的 platform/part/preset 时填写。
    -- imports = { "shared_gunsmith_pack" },
    weaponTags = { "my_gunsmith_weapon" },
    partTags = { "my_gunsmith_part" },
    override = false
})
```

字段说明：

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `entry` | string | 必填 | 相对 `modDir` 的 Lua 配置入口。 |
| `modDir` | string | 从调用来源推断 | 模组根目录。Autorun 里通常写 `modDir = ...`。 |
| `modPath` | string | 无 | `modDir` 的别名。 |
| `id` | string | 从目录名推断 | 包 id，用于记录配置归属和重复定义检测。 |
| `name` | string | 从 filelist 或目录名推断 | 显示/日志用名称。 |
| `localizationFiles` | string array | 从 filelist 的 `<Text>` 推断 | 校验本地化 key 时读取的文本文件。 |
| `localizationPrefix` | string | `id .. ".gunsmith"`，也会尝试从已注册 key 推断 | 可选。覆盖此包的框架 UI 文案前缀；不写也能正常工作。 |
| `imports` | string array | `{}` | 可选。显式允许本包引用其他 package 拥有的 platform、part、weapon 或 NPC preset。默认禁止跨包引用。 |
| `weaponTags` | string array | 尝试从 XML item tags 推断 | 注册为 Gunsmith 武器标签。 |
| `partTags` | string array | 尝试从 XML item tags 推断 | 注入 Quick Slot 容器时用作可放入部件标签。 |
| `override` | boolean | `false` | 为 `true` 时允许覆盖其他包已有的同 id platform/weapon/part/preset。 |

owner 隔离规则：

- 每个通过 `RegisterPackage` 加载的 platform、weapon、part、NPC preset 都会记录所属 package。
- 默认只能引用本 package 拥有的配置。weapon 的 `platform`、root part、`defaultPart`、NPC preset 的 `weapon/parts`，以及 UI/Quick UI 的部件候选都会按 owner 过滤。
- 如果要复用其他包的配置，在当前包声明 `imports = { "other_package_id" }`。被 import 的包必须已经注册。
- `imports` 只允许引用，不允许覆盖同名配置。覆盖同名 platform/weapon/part/preset 仍然必须使用 `override = true`。
- 仍建议所有全局 key 使用模组前缀，例如 `my_mod_ar_platform`、`my_mod_receiver`，避免 duplicate key。
- 直接写全局 `GunsmithFramework.Config` 而不通过 `RegisterPackage` 不属于支持的第三方接口；校验器会把真实配置中的无 owner 条目标为错误。

配置入口内可用：

```lua
local package = GunsmithFramework.CurrentPackage()
local modDir = package.modDir
```

## Platform 配置

Platform 定义同一类武器共享的画布、根槽和路径显示名。

```lua
GunsmithFramework.Config.platforms.my_platform = {
    -- 可选：不写时使用所属 package 的 localizationPrefix，或框架默认前缀。
    -- localizationPrefix = "my.gunsmith",
    canvas = { w = 512, h = 160 },
    rootSlots = {
        { path = "receiver" , hidden = true }
    },
    requiredSlots = { "barrel", "stock" },
    pathNameKeys = {
        receiver = "my.gunsmith.path.receiver",
        hidden_root = "my.gunsmith.path.hidden_root",
        barrel = "my.gunsmith.path.barrel",
        stock = "my.gunsmith.path.stock"
    }
}
```

字段说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `canvas.w` / `canvas.h` | number | 是 | 合成整枪贴图的画布宽高。 |
| `rootSlots` | array | 是 | 根槽数组，必须写成 `{ { path = "receiver" } }`，不能写成键值表。 |
| `rootSlots[].path` | string | 是 | 根槽路径。根槽路径不能包含 `/`。 |
| `rootSlots[].hidden` | boolean | 否 | 隐藏根槽不直接显示在 UI 根层，它的子挂点会显示在根层。 |
| `pathNameKeys` | table | 是 | 路径段到本地化 key 的映射。root path 和 required slot 的每个路径段都需要 key。 |
| `localizationPrefix` | string | 否 | 可选。覆盖此 platform 的路径 UI 文案前缀；大多数第三方模组可以不写。 |
| `requiredSlots` | string array | 否 | 不能拆空的子槽。只有存在且仅存在一个 hidden root 时可用，路径写 hidden root 下的相对路径，不要带 hidden root 自身。 |

规则：

- root slot 永远必选，不要写 `required = true`。
- `requiredSlots` 必须是数组，不是 `{ barrel = true }` 这样的键值表。
- 如果没有 hidden root，就不要写 `requiredSlots`。

## Weapon 配置

Weapon 绑定一个 XML item identifier。键名必须等于 XML `<Item identifier="...">`。

```lua
GunsmithFramework.Config.weapons.my_rifle = {
    platform = "my_platform",
    -- 可选：不写时使用所属 package 的 localizationPrefix，或框架默认前缀。
    -- localizationPrefix = "my.gunsmith",
    roots = {
        receiver = {
            part = "my_receiver",
            socket = { x = 256, y = 80 }
        }
    },
    preview = {
        padding = 12,
        scale = 1.0,
        offset = { x = 0, y = 0 }
    },
    inventory = {
        scale = 0.8,
        rotation = 0,
        padding = 2
    },
    world = {
        scale = 1.0,
        rotation = 0,
        padding = 0,
        offset = { x = 0, y = 0 }
    },
    quickSlotTags = { "my_gunsmith_part" },
    quickSlotBindings = {
        muzzle = {
            slot = 1,
            itemPosOffset = { x = 0, y = 0 },
            rotation = 0
        }
    }
}
```

字段说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `platform` | string | 是 | 引用 `Config.platforms[platformId]`。 |
| `localizationPrefix` | string | 否 | 可选。覆盖此 weapon 的完整/快速改装 UI 文案前缀；大多数第三方模组可以不写。 |
| `roots` | table | 是 | 每个 platform root slot 都必须有对应 root 配置。 |
| `roots[path].part` | string | 是 | 默认 root part id。该 part 的 `type` 必须等于 root path。 |
| `roots[path].socket` | point | 是 | root part 在 canvas 上的连接点 `{ x, y }`。 |
| `preview.padding` | number >= 0 | 否，默认 `12` | UI 预览边距。 |
| `preview.scale` | number > 0 | 否，默认 `1.0` | UI 预览缩放。 |
| `preview.offset` | point | 否，默认 `{0,0}` | UI 预览偏移。 |
| `inventory.scale` | number > 0 | 否，默认 `1.0` | 物品栏图标缩放。 |
| `inventory.rotation` | number | 否，默认 `0` | 物品栏图标旋转角度，单位为度。 |
| `inventory.padding` | number >= 0 | 否，默认 `0` | 物品栏图标边距。 |
| `world.scale` | number > 0 | 否，默认 `1.0` | 世界贴图缩放。 |
| `world.rotation` | number | 否，默认 `0` | 世界贴图旋转角度，单位为度。 |
| `world.padding` | number >= 0 | 否，默认 `0` | 世界贴图边距。 |
| `world.offset` | point | 否，默认 `{0,0}` | 世界贴图偏移。 |
| `quickSlotBindings` | table | 否 | 将部件挂点上的 `mount.quick.key` 绑定到真实 ItemContainer 槽位。 |
| `quickSlotBindings[key].slot` | integer >= 0 | 是 | 真实容器槽位索引，从 `0` 开始。 |
| `quickSlotBindings[key].itemPosOffset` | point | 否 | Quick Slot 附件显示点相对挂点的偏移。 |
| `quickSlotBindings[key].rotation` | number | 否，默认 `0` | Quick Slot 附件显示旋转角度。 |
| `quickSlotTags` | string array | 否 | 覆盖动态注入 Quick Slot 的 containable tags。未写时使用 package `partTags`。 |

兼容字段：

- `weapon.quickSlots` 仍可作为 fallback 被读取，但不推荐新模组使用。它不会经过同等严格的可达性校验，也不会自动从 `mount.quick` 生成锚点。新配置应使用 `mount.quick` + `weapon.quickSlotBindings`。

如果必须维护旧配置，`weapon.quickSlots` 是数组，运行时会读取这些字段：

```lua
quickSlots = {
    {
        key = "muzzle",
        path = "receiver/barrel/muzzle",
        slot = 1,
        nameKey = "my.gunsmith.quick.muzzle",
        anchor = { x = 120, y = 40 },
        itemPosOffset = { x = 0, y = 0 },
        showWhenContained = { "my_suppressor_item" },
        rotation = 0
    }
}
```

这些字段不会由校验器完整保护。新内容仍应迁移到 `quickSlotBindings`。

## Part 配置

Part 是所有可安装部件、默认结构件和附件的配置。

```lua
GunsmithFramework.Config.parts.my_handguard = {
    type = "handguard",
    nameKey = "my.gunsmith.part.handguard",
    provides = { "my_ar_handguard" },
    excludes = { "my_conflicting_part" },
    item = {
        identifier = "my_handguard_item"
    },
    visual = {
        texture = "%ModDir%/Items/my_parts.png",
        source = { x = 0, y = 64, w = 160, h = 48 },
        attachPoint = { x = 10, y = 24 },
        order = 20,
        scale = 1.0
    },
    stats = {
        Ergonomics = 2,
        RangedSpreadReduction = 0.05
    },
    mounts = {
        {
            path = "muzzle",
            partType = "muzzle",
            nameKey = "my.gunsmith.path.muzzle",
            accepts = { "my_muzzle" },
            defaultPart = "my_flash_hider",
            anchor = { x = 150, y = 20 },
            visualOrder = 30,
            quick = {
                key = "muzzle",
                nameKey = "my.gunsmith.quick.muzzle",
                showWhenContained = { "my_flash_hider_item", "my_suppressor_item" }
            }
        }
    }
}
```

字段说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `type` | string | 是 | 部件类型。安装到 root slot 时必须等于 root path；安装到 mount 时必须等于 `mount.partType` 或 `mount.path`。 |
| `nameKey` | string | 是 | UI 显示名本地化 key。 |
| `uiOrder` | number | 否，默认 `0` | 配件列表排序权重，越小越靠前；相同权重按 part id 排序。`[空]` 始终位于最前。 |
| `provides` | string array | 是 | 兼容性标签。必须和目标 mount 的 `accepts` 至少有一个交集。 |
| `excludes` | string array | 否 | 与这些 part id 互斥。任一方向声明都会互斥。 |
| `item.identifier` | string | 条件必填 | 真实 XML 部件物品 identifier。非默认部件必须有 `item.identifier` 或 `item.virtual = true`。 |
| `item.virtual` | boolean | 条件必填 | `true` 表示不需要真实物品。 |
| `visual` | table | 否 | 视觉图层。缺少 visual 的部件仍可参与兼容性/默认树/属性，但不会绘制图层。 |
| `visual.texture` | string | visual 存在时必填 | 图片路径，支持 `%ModDir%`。 |
| `visual.source` | rect | visual 存在时必填 | `{ x, y, w, h }`，图片像素裁切区域。 |
| `visual.attachPoint` | point | 与 `relativeOffset` 二选一 | source 内连接点。 |
| `visual.relativeOffset` | point | 与 `attachPoint` 二选一 | 相对锚点的绘制偏移。 |
| `visual.order` | number | 否，默认 `0` | 图层绘制顺序。mount 上的 `visualOrder` 会覆盖它。 |
| `visual.scale` | number > 0 | 否，默认 `1.0` | 单个部件图层缩放。 |
| `stats` | table | 否 | 数值修正。键见“Stats 附录”，值必须是 number。 |
| `mounts` | array | 否 | 此部件安装后提供的子挂点。 |
| `quickAttachmentTransform.muzzleOutletOffset` | point | 否 | 高级可选字段。只对安装在 `muzzle` 或 `lower_rail` Quick Slot 路径上的部件有意义；`lower_rail` 是 Vanilla-Components-Expanded 下挂武器兼容路径。见下方专门说明。 |

`visual` 完整性规则：

- 如果写了 `visual`，必须有 `texture` 和完整 `source.x/y/w/h`。
- 必须写 `attachPoint` 或 `relativeOffset` 其中一个。
- `visual.offset` 是旧字段，不要使用。

### Mount 配置

`part.mounts` 必须是数组：

```lua
mounts = {
    {
        path = "optic_mount",
        partType = "optic",
        nameKey = "my.gunsmith.path.optic",
        accepts = { "picatinny_optic" },
        defaultPart = "my_iron_sight",
        anchor = { x = 70, y = -12 },
        visualOrder = 40,
        quick = {
            key = "optic",
            nameKey = "my.gunsmith.quick.optic",
            showWhenContained = { "my_scope_item" }
        }
    }
}
```

字段说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 子路径段。完整路径由父路径拼接，例如 `receiver/handguard/optic_mount`。 |
| `partType` | string | 否 | 接受的 part `type`。未写时使用 `path`。 |
| `nameKey` | string | 否 | 子槽显示名本地化 key。未写时用 platform `pathNameKeys[path]`。 |
| `defaultPart` | string | 否 | 父部件安装时自动安装的默认子部件。 |
| `accepts` | string array | 是 | 接受的 `provides` 标签。 |
| `anchor` | point | 否 | 父部件上的子挂点，坐标相对父部件 `visual.attachPoint`。 |
| `visualOrder` | number | 否 | 安装在此 mount 上的子部件图层顺序。 |
| `quick` | table | 否 | 将此 mount 暴露给 Quick Slot 系统。 |
| `quick.key` | string | quick 存在时必填 | 由 `weapon.quickSlotBindings[key]` 引用。 |
| `quick.nameKey` | string | 否 | Quick UI 显示名。 |
| `quick.showWhenContained` | string array | 否 | 只有槽内物品 identifier 在此列表中时，显示该原生槽。 |

默认部件规则：

- `defaultPart` 必须存在。
- 默认部件的 `type` 必须匹配 `mount.partType` 或 `mount.path`。
- 默认部件的 `provides` 必须被 `mount.accepts` 接受。
- 默认树最大深度为 32，不能形成循环。

### Quick Attachment 枪口出口偏移

`quickAttachmentTransform` 不是普通视觉对齐字段。普通部件贴图仍然只用 `visual.source`、`visual.attachPoint`、`mount.anchor` 和 `visual.scale` 对齐。

它目前只有一个可写字段：

```lua
quickAttachmentTransform = {
    muzzleOutletOffset = { x = 60, y = 0 }
}
```

含义：当这个部件被装在某个 Quick Slot 上，并且该 Quick Slot 的 key 是 `muzzle` 或 `lower_rail` 时，框架会把该 slot 的 anchor 当作附件基准点，再加上 `muzzleOutletOffset`，得到“实际枪口出口点”。其中 `lower_rail` 专门服务 Vanilla-Components-Expanded 下挂武器兼容，不是普通下导轨附件必须使用的 key。

这个点用于需要知道枪口位置的运行时效果，例如：

- 枪口 flash / spark 等带 Gunsmith barrel transform 的粒子。
- 使用 Vanilla-Components-Expanded 的下挂发射器/下挂武器发射点。
- 依赖 Quick Attachment barrel transform 的兼容补丁。

不需要它的情况：

- 普通瞄具。
- 普通握把。
- 只影响合成贴图的枪管、护木、枪托。
- 任何不需要“实际枪口出口点”的部件。

坐标规则：

- 坐标使用 Gunsmith canvas 像素。
- `{ x = 0, y = 0 }` 表示枪口出口就在该 Quick Slot 的 anchor 上。
- `x` 向右增加，`y` 向下增加。
- 不确定时先省略这个字段；省略时等价于没有额外枪口出口偏移。

典型用法是写在消音器、枪口装置、下挂榴弹发射器或霰弹枪这类 Quick Slot 附件 part 上：

```lua
parts.my_suppressor = {
    type = "muzzle",
    nameKey = "my.gunsmith.part.suppressor",
    provides = { "my_muzzle" },
    item = { identifier = "my_suppressor_item" },
    visual = {
        texture = "%ModDir%/Items/my_parts.png",
        source = { x = 0, y = 48, w = 64, h = 24 },
        attachPoint = { x = 4, y = 12 }
    },
    quickAttachmentTransform = {
        muzzleOutletOffset = { x = 60, y = 0 }
    }
}
```

Quick Attachment 枪口出口偏移目前识别两个 quick key：

- `muzzle`：主枪口。
- `lower_rail`：Vanilla-Components-Expanded 下挂发射器或下挂武器兼容路径；只有安装部件声明了 `quickAttachmentTransform.muzzleOutletOffset` 时才注册。不使用 Vanilla-Components-Expanded 时，普通下导轨附件不需要这个 key。

### XML StatusEffect 用法

Lua 里的 `quickAttachmentTransform.muzzleOutletOffset` 只负责告诉框架“当前枪口出口点在 canvas 上哪里”。XML 里的粒子效果还需要给对应 `StatusEffect` 加上 Gunsmith tag，框架才会把这个 StatusEffect 的生成位置改到当前枪口出口点。

需要添加的 tag 是：

```xml
statuseffecttags="Gunsmith_BarrelParticle"
```

典型写法：

```xml
<RangedWeapon barrelpos="0,0">
  <StatusEffect type="OnUse" target="This" statuseffecttags="Gunsmith_BarrelParticle">
    <!-- 这里保留你原本的枪口 flash / spark 粒子效果。 -->
    <ParticleEmitter particle="muzzleflash" particleamount="1" />
    <ParticleEmitter particle="spark" particleamount="3" />
  </StatusEffect>
</RangedWeapon>
```

工作方式：

- 没有 `Gunsmith_BarrelParticle` tag 的 `StatusEffect` 不会被 GunsmithFramework 改写位置。
- 带这个 tag 的 `StatusEffect` 必须由带 `RangedWeapon` 组件的武器 item 触发，否则框架会报错。
- 框架会优先使用当前 Gunsmith Quick Attachment barrel transform；如果没有注册 transform，则回退到 XML `RangedWeapon.BarrelPos`。
- 主枪口使用 `muzzle` quick key 注册为 `primary` barrel rule。
- 使用 Vanilla-Components-Expanded 时，VCE 的 projectile selector 会把选择 `0` 映射到 `primary`，选择 `1` 映射到 `lower_rail`。
- 不要再为不同枪口件写多套固定 `StatusEffect barrelpos` 或 `Distance` 来手动偏移枪口粒子；偏移应写在对应 part 的 `quickAttachmentTransform.muzzleOutletOffset`。

适合加这个 tag 的效果：

- 枪口 flash。
- 枪口 spark。
- 其他必须跟随 Gunsmith 当前枪口出口点的粒子。

不建议依赖这个 tag 的效果：

- 声音。
- 爆炸。
- 弹壳抛出。
- 不使用 `StatusEffect` world position 的自定义逻辑。

## 兼容性模型

安装一个 part 到某个 path 时，框架按以下顺序判断：

1. part id 必须存在。
2. part `type` 必须等于该 path 的部件类型。
3. 不能和当前已安装选择里的其他 part 互斥。
4. root slot 不检查 `accepts`，只检查 type。
5. 子 mount 必须存在 `accepts`，并且 `part.provides` 与 `mount.accepts` 有交集。

路径示例：

```text
receiver
receiver/barrel
receiver/handguard
receiver/handguard/top_rail
receiver/handguard/top_rail/optic_mount
```

路径段由 root slot 和各级 mount 的 `path` 组成。保存状态、NPC preset 和 UI 都使用同一套路径。

## XML 配置

### Gunsmith 武器 item

每个 Gunsmith 武器 XML item 都应有 `<GunsmithData />`：

```xml
<Item identifier="my_rifle" tags="weapon,gun,gunsmith">
  <Sprite texture="%ModDir%/Items/my_rifle_base.png" sourcerect="0,0,128,64" depth="0.55" />
  <Body width="128" height="32" />
  <Holdable slots="RightHand,LeftHand" />
  <RangedWeapon />
  <ItemContainer capacity="1" hideitems="true">
    <Containable items="my_magazine" />
  </ItemContainer>
  <GunsmithData />
</Item>
```

`GunsmithData` 字段：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `savedstate` | string | `""` | 持久化选择状态。通常由框架写入，不建议手写。 |
| `quickslotstart` | integer | 有 Quick Slot 时默认 `1` | 动态注入 Quick Slot 子容器的起始槽位。 |
| `quickslotmax` | integer | 从 Lua `quickSlotBindings` 推断 | 需要注入到的最大槽位。未注册 Lua quick slots 时可手动指定。 |
| `quickslotitems` | string | `""` | 注入 Quick Slot 的 `<Containable items="...">`。 |
| `quickslottags` | string | Lua 注册的 quick slot tags | 注入 Quick Slot 的 `<Containable tags="...">`。 |

动态 Quick Slot 注入规则：

- 框架读取武器的 `ItemContainer` 当前容量和已有 `SubContainer` 容量。
- 如果 Lua 绑定的最大 slot 超过现有容量，会自动追加隐藏 `SubContainer`。
- 追加的槽位 `capacity="1"`、`maxstacksize="1"`、`hide="true"`、`itempos="0,0"`、`setactive="true"`。
- 如果你的武器已有弹匣槽，通常保留 slot `0` 给弹匣，让 Quick Slot 从 `1` 开始。

### 部件 item

有真实物品的 part 需要一个普通 XML item，其 identifier 等于 `part.item.identifier`：

```xml
<Item identifier="my_suppressor_item" nameidentifier="entityname.my_suppressor_item" category="Weapon" tags="smallitem,my_gunsmith_part">
  <Sprite texture="%ModDir%/Items/my_parts.png" sourcerect="0,128,64,24" depth="0.55" />
  <Body width="64" height="24" />
</Item>
```

安装时框架按 identifier 找物品，不按 name 找。若 XML item 不存在，校验器会警告。

### Fabricator 配方过滤

GunsmithFramework 可以在加工台左侧分类栏中加入一个 Gunsmith 分类按钮。玩家点击该按钮后，底部会显示一个专用枪械槽；放入 Gunsmith 枪械后，配方列表只显示这把枪理论上可安装的部件物品。

原版 `fabricator` 默认启用此按钮。自定义 Fabricator 需要在自己的 `<Fabricator>` 组件上加一个属性：

```xml
<Item identifier="my_gunsmith_fabricator" tags="my_gunsmith_fabricator" category="Machine">
  <Sprite texture="%ModDir%/Items/my_fabricator.png" />
  <Body width="160" height="96" />
  <Fabricator canbeselected="true" gunsmithframeworkbutton="true" />
</Item>
```

第三方作者不需要为枪械槽额外写 `ItemContainer`。框架会在加载启用的 Fabricator prefab 时自动注入一个隐藏的 1 格容器，专门用于放入 Gunsmith 枪械。这个枪械槽只在 Gunsmith 分类页显示；切回原版分类后，加工台会恢复原版输入栏和原版配方列表。

可显示的配方必须已经是 Barotrauma 原版 Fabricate 配方。GunsmithFramework 只负责过滤，不会凭空生成配方：

```xml
<Item identifier="my_suppressor_item" nameidentifier="entityname.my_suppressor_item" category="Weapon" tags="smallitem,my_gunsmith_part">
  <Sprite texture="%ModDir%/Items/my_parts.png" sourcerect="0,0,64,24" />
  <Body width="64" height="24" />
  <Fabricate suitablefabricators="my_gunsmith_fabricator" requiredtime="8">
    <RequiredItem identifier="steel" />
  </Fabricate>
</Item>
```

匹配规则：

- 目标配方的 item identifier 必须等于某个可安装 part 的 `part.item.identifier`。
- 该配方仍必须适用于当前 Fabricator，也就是 `<Fabricate suitablefabricators="...">` 中包含当前加工台 identifier。
- 枪械槽为空，或放入的不是 Gunsmith 武器时，Gunsmith 分类页不会显示可制造配方。
- 放入 Gunsmith 武器后，框架从 weapon 的 root parts 出发，递归扫描这些 part 声明的 `mounts`，用候选 part 的 `provides` 和 mount 的 `accepts` 判断兼容性。
- 第一版列出的是“这把枪理论上可安装的所有部件”，不会根据当前已安装部件动态缩窄。
- 制造结果只产出部件物品，不会自动安装到枪上。
- 服务端会用同一套枪械槽和兼容性结果校验客户端请求；客户端伪造不兼容 recipe 时会被拒绝。

如果 Gunsmith 分类页为空，优先检查三件事：

1. 枪械 XML item 是否有 `<GunsmithData />`，Lua `Config.weapons[identifier]` 是否存在。
2. part 是否有 `item = { identifier = "..." }`，并且这个 XML item 真的存在。
3. 该 XML item 是否有适用于当前 Fabricator 的 `<Fabricate suitablefabricators="...">`。

### NPC preset

NPC 或人类预设 XML 中的武器 item 可以写：

```xml
<Item identifier="my_rifle" gunsmithpreset="guard_rifle">
  <Item identifier="my_magazine" />
</Item>
```

不要把瞄具、握把、枪口等 Gunsmith Quick Slot 附件直接塞进 NPC XML。让 Lua profile 负责这些部件，框架会为 Quick Slot 自动生成真实 contained item。

## NPC Presets

NPC preset 写在 Lua：

```lua
local profiles = GunsmithFramework.Config.npcPresets.profiles

profiles.guard_rifle = {
    weapon = "my_rifle",
    parts = {
        receiver = "my_receiver",
        ["receiver/barrel"] = "my_barrel_short",
        ["receiver/handguard"] = "my_handguard",
        ["receiver/handguard/top_rail/optic_mount"] = "my_red_dot",
        ["receiver/handguard/bottom_rail/grip_mount"] = "my_vertical_grip",
        ["receiver/barrel/muzzle"] = "my_suppressor"
    }
}
```

字段说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| profile key | string | 是 | XML `gunsmithpreset` 引用的名字。 |
| `weapon` | string | 否，但推荐 | 限定武器 XML identifier。写错武器时会忽略并警告。 |
| `parts` | table | 否 | path 到 part id 的选择表。未列出的路径使用默认选择。 |

规则：

- profile 会先构造默认选择，再应用 `parts`。
- 无效路径、不存在 part 或不兼容 part 会被警告，最终选择会被修剪。
- 服务端上如果武器已有非空 `SavedState`，不会用 NPC preset 覆盖它。
- Quick Slot profile 部件需要 `part.item.identifier`，否则只能影响视觉/属性，不能自动生成真实 contained item。

## Quick Slot 完整示例

Quick Slot 把 Gunsmith 挂点连接到武器自己的真实 `ItemContainer` 槽位。它由三部分组成：

```text
part.mounts[].quick.key
            ↓ 按 key 查找
weapon.quickSlotBindings[key]
            ↓ 使用 slot
武器 ItemContainer 中从 0 开始的真实槽位
```

`quickSlotBindings` 的键不是部件路径。完整路径由框架根据当前已安装的部件树自动生成；配置者只需要让 `mount.quick.key` 和 `quickSlotBindings[key]` 完全一致。

### 1. 在 mount 上声明 Quick Slot

Quick Slot 应写在“提供目标挂点”的父部件上。下面的枪管提供 `muzzle` 挂点：

```lua
parts.my_barrel = {
    type = "barrel",
    nameKey = "my.gunsmith.part.barrel",
    provides = { "my_barrel" },
    item = { virtual = true },
    visual = {
        texture = "%ModDir%/Items/my_parts.png",
        source = { x = 0, y = 0, w = 160, h = 40 },
        attachPoint = { x = 8, y = 20 }
    },
    mounts = {
        {
            path = "muzzle",
            partType = "muzzle",
            nameKey = "my.gunsmith.path.muzzle",
            accepts = { "my_muzzle" },
            anchor = { x = 152, y = 20 },
            quick = {
                key = "muzzle",
                nameKey = "my.gunsmith.quick.muzzle",
                showWhenContained = { "my_suppressor_item" }
            }
        }
    }
}
```

`path = "muzzle"` 决定挂点路径段；`quick.key = "muzzle"` 是武器绑定时使用的稳定键。两者可以同名，但含义不同。

### 2. 在 weapon 上绑定真实容器槽

武器使用同一个 key 把挂点绑定到真实槽位：

```lua
weapons.my_rifle.quickSlotTags = { "my_gunsmith_part" }

weapons.my_rifle.quickSlotBindings = {
    muzzle = {
        slot = 1,
        itemPosOffset = { x = 0, y = 0 },
        rotation = 0
    }
}
```

字段含义：

- `slot`：必填，非负整数，表示武器 `ItemContainer` 中从 `0` 开始的槽位索引。同一武器的多个绑定不能使用同一个槽位。
- `itemPosOffset`：可选，Quick Slot 附件显示点相对 mount `anchor` 的偏移，默认 `{ x = 0, y = 0 }`。
- `rotation`：可选，Quick Slot 附件的显示旋转角度，默认 `0`。
- `quickSlotTags`：可选，动态注入槽位允许放入的 XML item tags；省略时使用所属 package 的 `partTags`。

如果武器的 slot `0` 已用于弹匣，通常让 Quick Slot 从 `1` 开始。框架会根据绑定中的最大 slot 自动补充缺少的隐藏 `SubContainer`，因此不需要仅为 Quick Slot 手写一组 XML 槽位。自定义容器注入时可参考前文的 `GunsmithData.quickslotstart`、`quickslotmax`、`quickslotitems` 和 `quickslottags`。

### 3. 让真实 item 对应可安装 part

Quick Slot 中放入的 XML item 必须能映射回一个兼容 part：

```lua
parts.my_suppressor = {
    type = "muzzle",
    nameKey = "my.gunsmith.part.suppressor",
    provides = { "my_muzzle" },
    item = { identifier = "my_suppressor_item" },
    visual = {
        texture = "%ModDir%/Items/my_parts.png",
        source = { x = 0, y = 48, w = 64, h = 24 },
        attachPoint = { x = 4, y = 12 }
    },
    quickAttachmentTransform = {
        muzzleOutletOffset = { x = 60, y = 0 }
    }
}
```

对应的 XML item 至少需要使用同一个 identifier，并带有 `quickSlotTags` 或 package `partTags` 允许的 tag：

```xml
<Item identifier="my_suppressor_item" tags="smallitem,my_gunsmith_part">
  <!-- Sprite、Body 等普通 item 组件 -->
</Item>
```

这里的三个值必须互相兼容：

```text
mount.partType / mount.path = "muzzle"
part.type                  = "muzzle"
mount.accepts              = { "my_muzzle" }
part.provides              = { "my_muzzle" }
```

### 多层路径示例

假设枪托的实际路径是 `receiver/stock_mount/stock`：

```lua
parts.my_receiver.mounts = {
    {
        path = "stock_mount",
        accepts = { "my_buffer_tube" },
        defaultPart = "my_buffer_tube",
        anchor = { x = -60, y = 0 }
    }
}

parts.my_buffer_tube = {
    type = "stock_mount",
    provides = { "my_buffer_tube" },
    item = { virtual = true },
    mounts = {
        {
            path = "stock",
            accepts = { "my_stock" },
            anchor = { x = -16, y = 0 },
            quick = { key = "stock" }
        }
    }
}

weapons.my_rifle.quickSlotBindings = {
    stock = { slot = 2 }
}
```

注意：

- `quick = { key = "stock" }` 写在 buffer tube 提供的 `stock` mount 上，不是写在 receiver 的 `stock_mount` mount 上。
- 绑定仍然写成 `stock = { slot = 2 }`，不要写成 `["receiver/stock_mount/stock"]`。
- 如果多个可替换的 buffer tube 都应该支持快速更换枪托，每个 buffer tube 都必须提供带相同 `quick.key = "stock"` 的 `stock` mount。
- 运行时只会为当前已安装部件树中实际存在的 Quick mount 生成槽位信息。
- `quick.showWhenContained = { "item_identifier" }` 只控制原生容器槽在装入指定物品时是否显示，不决定 part 兼容性。

### 运行时行为

- Quick UI 会列出 `type = "muzzle"` 且兼容此 mount 的部件。
- 安装 `my_suppressor` 时，框架从角色物品栏找 `my_suppressor_item`，再放入武器 slot `1`。
- 如果玩家直接把 `my_suppressor_item` 拖进 slot `1`，框架会反向同步选择为 `my_suppressor`。
- 若 slot 内已有其他物品，替换时会先清槽并返还角色物品栏；物品栏放不下时会掉落。

### 常见校验错误

运行 `GunsmithFrameworkValidate` 后：

- `does not match a quick mount reachable from this weapon`：没有任何该武器可达的 mount 声明同名 `quick.key`。检查 key 拼写、mount 所在 part 是否能从 weapon root 到达，以及跨 package 引用权限。
- `.slot duplicates slot N`：两个绑定使用了同一个真实容器槽位；为其中一个分配其他 slot。
- `.slot must be a non-negative integer`：slot 必须是 `0`、`1`、`2` 这样的整数。
- Quick UI 中没有某个嵌套挂点：检查当前安装的父部件是否真的提供该 mount。完整路径存在并不等于 mount 已声明 `quick`。
- 能显示但不能安装真实物品：检查 part 是否有 `item.identifier`、XML item identifier 是否一致，以及 `part.type`/`provides` 是否满足 mount 的 `partType`/`accepts`。

## 本地化

框架会校验以下 key 来源：

- `platform.pathNameKeys`。
- `part.nameKey`。
- `mount.nameKey`。
- `mount.quick.nameKey`。
- 包、platform 或 weapon 可选 `localizationPrefix` 下的 UI/action/status/stat key。

建议为每个包使用统一前缀：

```xml
<my.gunsmith.path.receiver>Receiver</my.gunsmith.path.receiver>
<my.gunsmith.path.barrel>Barrel</my.gunsmith.path.barrel>
<my.gunsmith.part.receiver>Receiver</my.gunsmith.part.receiver>
<my.gunsmith.part.suppressor>Suppressor</my.gunsmith.part.suppressor>
<my.gunsmith.quick.muzzle>Muzzle</my.gunsmith.quick.muzzle>
```

`localizationPrefix` 不是必填项。只有当你想把框架 UI 文案从默认 `gunsmith.framework.*` 换到自己的前缀时，才需要声明它。

如果你声明了 `localizationPrefix = "my.gunsmith"`，可以覆盖框架 UI 文案，例如：

```xml
<my.gunsmith.ui.title>Gunsmith: {0}</my.gunsmith.ui.title>
<my.gunsmith.ui.quick_title>Quick Gunsmith: {0}</my.gunsmith.ui.quick_title>
<my.gunsmith.action.install>Install</my.gunsmith.action.install>
<my.gunsmith.stat.ergonomics>Ergonomics</my.gunsmith.stat.ergonomics>
<my.gunsmith.stattypes.RangedSpreadReduction>Spread Reduction</my.gunsmith.stattypes.RangedSpreadReduction>
```

`localizationPrefix` 的作用是告诉框架：“这个包、平台或武器的框架 UI 文案去哪个 key 前缀下面找”。它不会自动给 `part.nameKey` 或 `pathNameKeys` 加前缀；这些字段仍然要写完整 key。因此，即使完全不写 `localizationPrefix`，只要 `pathNameKeys` 和 `nameKey` 指向的 key 存在，槽位名和部件名仍会正常显示。

完整例子：

```lua
GunsmithFramework.RegisterPackage({
    modDir = ...,
    id = "my_gunsmith_mod",
    entry = "Lua/Scripts/MyGunsmithMod/Config.lua",
    localizationFiles = { "text/English.xml" },
    -- 可选：不写也可以。写了以后，框架 UI 会优先找 my.gunsmith.ui.* / my.gunsmith.action.* 等 key。
    -- localizationPrefix = "my.gunsmith"
})

GunsmithFramework.Config.platforms.my_platform = {
    -- 可选：通常不必写；不写时继承所属 package 的前缀。
    -- localizationPrefix = "my.gunsmith",
    canvas = { w = 128, h = 64 },
    rootSlots = {
        { path = "receiver" }
    },
    pathNameKeys = {
        receiver = "my.gunsmith.path.receiver"
    }
}

GunsmithFramework.Config.parts.my_receiver = {
    type = "receiver",
    nameKey = "my.gunsmith.part.receiver",
    provides = { "my_receiver" },
    item = { virtual = true }
}

GunsmithFramework.Config.weapons.my_rifle = {
    platform = "my_platform",
    -- 可选：通常不必写；只有这把武器需要不同 UI 文案前缀时才写。
    -- localizationPrefix = "my.gunsmith",
    roots = {
        receiver = {
            part = "my_receiver",
            socket = { x = 64, y = 32 }
        }
    }
}
```

对应 `text/English.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<infotexts language="English">
  <my.gunsmith.ui.title>Modify: {0}</my.gunsmith.ui.title>
  <my.gunsmith.ui.quick_title>Quick Modify: {0}</my.gunsmith.ui.quick_title>
  <my.gunsmith.ui.weapon_root>Weapon</my.gunsmith.ui.weapon_root>
  <my.gunsmith.ui.empty_part>Empty</my.gunsmith.ui.empty_part>
  <my.gunsmith.action.install>Install</my.gunsmith.action.install>
  <my.gunsmith.action.remove>Remove</my.gunsmith.action.remove>
  <my.gunsmith.stat.ergonomics>Ergonomics</my.gunsmith.stat.ergonomics>
  <my.gunsmith.stat.none>No stat changes</my.gunsmith.stat.none>

  <my.gunsmith.path.receiver>Receiver</my.gunsmith.path.receiver>
  <my.gunsmith.part.receiver>Receiver</my.gunsmith.part.receiver>
</infotexts>
```

这个例子中：

- UI 标题、按钮、状态和 stat 显示会使用 `localizationPrefix .. ".ui.title"`、`localizationPrefix .. ".action.install"` 这类 key。
- 槽位名来自 `platform.pathNameKeys.receiver`，所以要写完整的 `my.gunsmith.path.receiver`。
- 部件名来自 `part.nameKey`，所以要写完整的 `my.gunsmith.part.receiver`。
- 如果完全不写 `localizationPrefix`，框架 UI 会使用推断出的 package 前缀；推断不到时使用默认 `gunsmith.framework`。这不会影响显式写好的 `pathNameKeys` 和 `part.nameKey`。
- 如果 weapon 写了 `localizationPrefix`，weapon UI 优先用 weapon 的前缀；否则用拥有该 weapon 的 package 前缀；再否则用框架默认 `gunsmith.framework`。
- platform 路径 UI 优先用 platform 的 `localizationPrefix`；否则用拥有该 platform 的 package 前缀。一般第三方模组不需要单独给每个 platform 写这个字段。

未提供时会回退到 key 文本或框架默认文本。

## Stats 附录

`part.stats` 中可写以下数值键。值必须是 number。

`Ergonomics` 是 GunsmithFramework 自己使用的操控值，会影响瞄准抬枪速度，并显示在 UI 中。

其他键会作为 Barotrauma `StatTypes` 加到持有该 Gunsmith 武器的角色身上。UI 中大部分 StatTypes 按百分比显示；技能 bonus/override 和少数计数型字段按平值显示。

```lua
stats = {
    Ergonomics = 3,
    RangedSpreadReduction = 0.05,
    WeaponsSkillBonus = 10
}
```

完整键列表：

```text
Ergonomics
ElectricalSkillBonus
HelmSkillBonus
MechanicalSkillBonus
MedicalSkillBonus
WeaponsSkillBonus
HelmSkillOverride
MedicalSkillOverride
WeaponsSkillOverride
ElectricalSkillOverride
MechanicalSkillOverride
MaximumHealthMultiplier
MovementSpeed
WalkingSpeed
SwimmingSpeed
PropulsionSpeed
BuffDurationMultiplier
DebuffDurationMultiplier
MedicalItemEffectivenessMultiplier
FlowResistance
AttackMultiplier
TeamAttackMultiplier
RangedAttackSpeed
RangedAttackMultiplier
TurretAttackSpeed
TurretPowerCostReduction
TurretChargeSpeed
MeleeAttackSpeed
MeleeAttackMultiplier
RangedSpreadReduction
RepairSpeed
MechanicalRepairSpeed
ElectricalRepairSpeed
DeconstructorSpeedMultiplier
RepairToolStructureRepairMultiplier
RepairToolStructureDamageMultiplier
RepairToolDeattachTimeMultiplier
MaxRepairConditionMultiplierMechanical
MaxRepairConditionMultiplierElectrical
IncreaseFabricationQuality
GeneticMaterialRefineBonus
GeneticMaterialTaintedProbabilityReductionOnCombine
SkillGainSpeed
ExtraLevelGain
HelmSkillGainSpeed
WeaponsSkillGainSpeed
MedicalSkillGainSpeed
ElectricalSkillGainSpeed
MechanicalSkillGainSpeed
MedicalItemApplyingMultiplier
BuffItemApplyingMultiplier
PoisonMultiplier
TinkeringDuration
TinkeringStrength
TinkeringDamage
ReputationGainMultiplier
ReputationLossMultiplier
MissionMoneyGainMultiplier
ExperienceGainMultiplier
MissionExperienceGainMultiplier
ExtraMissionCount
ExtraSpecialSalesCount
StoreSellMultiplier
StoreBuyMultiplierAffiliated
StoreBuyMultiplier
ShipyardBuyMultiplierAffiliated
ShipyardBuyMultiplier
MaxAttachableCount
ExplosionRadiusMultiplier
ExplosionDamageMultiplier
FabricationSpeed
BallastFloraDamageMultiplier
HoldBreathMultiplier
Apprenticeship
CPRBoost
LockedTalents
HireCostMultiplier
InventoryExtraStackSize
SoundRangeMultiplier
SightRangeMultiplier
DualWieldingPenaltyReduction
NaturalMeleeAttackMultiplier
```

未知 stat key 会产生 warning，不会作为有效属性应用。非 number 值会产生 error。

## 校验和调试

启动后框架会自动延迟运行一次配置校验。你也可以在游戏控制台手动运行：

```text
GunsmithFrameworkValidate
```

自测命令会构造一份故意错误的配置，用来确认校验器能输出错误：

```text
GunsmithFrameworkValidationSelfTest
```

开发时建议每次新增平台、武器或部件后都运行一次 `GunsmithFrameworkValidate`。常见输出含义：

- `ERROR`：配置不合法，相关功能可能完全不工作。
- `WARN`：配置可继续运行，但可能找不到 XML prefab、本地化 key 或没有任何 mount 接受某个 `provides`。
- `0 errors`：schema 层面通过，仍需进游戏检查坐标、贴图、Quick Slot 和物品返还。

## 作者接口速查

常用 Lua 表：

```lua
GunsmithFramework.Config.platforms
GunsmithFramework.Config.weapons
GunsmithFramework.Config.parts
GunsmithFramework.Config.npcPresets.profiles
```

常用 Lua 函数：

```lua
GunsmithFramework.RegisterPackage(package)
GunsmithFramework.CurrentPackage()
GunsmithFramework.Apply(item)
GunsmithFramework.Open(item)
GunsmithFramework.OpenQuick(item)
```

通常第三方模组只需要 `RegisterPackage` 和 `Config.*`。`Apply/Open/OpenQuick` 适合你自己写额外交互入口时调用。
