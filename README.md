# GunsmithFramework

**GunsmithFramework** 是一个面向 **Barotrauma** 的模块化改枪框架：

- **Lua** 负责声明式配置：枪械平台、武器、部件、挂点、Quick Slot 与 NPC 预设。
- **C#** 负责运行时能力：游戏内改装 UI、整枪贴图合成、存档持久化、Quick Slot 容器桥接与属性生效。

> [!NOTE]
> 本文档面向**第三方模组作者**，目标是帮你从零制作独立的改枪扩展包，无需修改 GunsmithFramework 本体。

---

## 目录

- [核心概念速览](#核心概念速览)
- [1. 环境准备与快速开始](#1-环境准备与快速开始)
- [2. 核心配置参考](#2-核心配置参考)
  - [2.1 包级配置（RegisterPackage）](#21-包级配置registerpackage)
  - [2.2 Platform（枪械平台）](#22-platform枪械平台)
  - [2.3 Weapon（武器）](#23-weapon武器)
  - [2.4 Part（部件）与 Mount（挂点）](#24-part部件与-mount挂点)
  - [2.5 配件互斥（excludes 与 exclusiveGroups）](#25-配件互斥excludes-与-exclusivegroups)
  - [2.6 贴图坐标、兼容性与存档限制](#26-贴图坐标兼容性与存档限制)
- [3. XML 接入与游戏内系统](#3-xml-接入与游戏内系统)
  - [3.1 武器与部件 XML（GunsmithData）](#31-武器与部件-xmlgunsmithdata)
  - [3.2 Quick Slot 完整指南](#32-quick-slot-完整指南)
  - [3.3 枪口出口偏移与粒子跟随](#33-枪口出口偏移与粒子跟随)
  - [3.4 工作台专用改装与 Fabricator 配方过滤](#34-工作台专用改装与-fabricator-配方过滤)
  - [3.5 NPC 预设（NPC Presets）](#35-npc-预设npc-presets)
- [4. 本地化配置](#4-本地化配置)
- [5. 数值属性（part.stats）](#5-数值属性partstats)
- [6. 校验、调试与接口速查](#6-校验调试与接口速查)

---

## 核心概念速览

### 配置层级关系

```text
Package（你的模组包，隔离归属与依赖）
 └─ Platform（共享画布尺寸、根槽与路径显示名）
     └─ Weapon（绑定 XML 武器 identifier、默认根部件与 Quick Slot 容器槽）
         └─ Part（贴图、属性、真实/虚拟物品）
             └─ Mount（子挂点，继续挂载下级 Part，可选暴露为 Quick Slot）
```

### 区分 4 种常见标识

写配置时最容易混淆的四个概念：

| 概念 | 出现位置 | 含义 | 示例 |
| --- | --- | --- | --- |
| **Lua Part ID** | `Config.parts.<id>`、`defaultPart`、`excludes` | Lua 中的部件配置键名 | `"my_suppressor"` |
| **XML Identifier** | `<Item identifier="...">`、`part.item.identifier`、`Config.weapons.<id>` | Barotrauma 实际物品 ID | `"my_suppressor_item"` |
| **Path（路径）** | `rootSlots[].path`、`mount.path`、NPC 预设 | 由根槽与各级挂点拼出的槽位路径 | `"receiver/barrel/muzzle"` |
| **Quick Key** | `mount.quick.key`、`weapon.quickSlotBindings.<key>` | 把挂点映射到真实容器槽的稳定键 | `"muzzle"` |

### 玩家操作与默认行为

- **按 `F1`**：手持 Gunsmith 武器时打开完整改装 UI。
- **按 `Shift+F1`**：武器配置了 Quick Slot 时打开快速改装 UI。
- **真实物品部件（`item.identifier`）**：安装时从角色物品栏消耗对应 XML 物品，拆除或替换时返还（放不下则掉落）。
- **虚拟部件（`item = { virtual = true }`）**：不消耗真实物品，适合默认结构件、不可掉落内部件或纯视觉件。

### 推荐制作顺序

1. 编写武器与部件的 XML，确认 Barotrauma 可正常加载。
2. 给武器 XML 添加 `<GunsmithData />`。
3. 新建 `platform`（先只放一个 `rootSlot`）。
4. 编写根 `part` 与 `weapon.roots`，确保默认武器可正确显示。
5. 使用内置工具 [`GunsmithAnchorEditor/index.html`](GunsmithAnchorEditor/index.html) 拾取 `visual.source`、`visual.attachPoint`、`roots[path].socket` 和 `mount.anchor`。
6. 逐步为部件添加 `mounts` 与子配件（枪管、护木、枪托、瞄具等）。
7. 需要快捷换装时，补充 `mount.quick` 与 `weapon.quickSlotBindings`。
8. 进游戏执行控制台命令 `GunsmithFrameworkValidate`，再实测完整 UI 与 Quick UI。

---

## 1. 环境准备与快速开始

### 运行环境

- **游戏版本**：Barotrauma Unstable `1.15.x` + 插件版 LuaCs（需在 LuaCs 设置中启用 **C# 模组**）。
  - 构建引用版本：Windows 为 `1.15.25`，Linux/macOS 为 `1.15.24`（详见 [`.Tools/Refs/README.md`](.Tools/Refs/README.md)）。
- **加载顺序**：将 GunsmithFramework 作为独立模组启用；使用下方的延迟注册写法时，**不限制模组加载顺序**。

### 推荐目录结构

```text
MyGunsmithMod/
├── ModConfig.xml
├── filelist.xml
├── Items/
│   ├── Weapons.xml
│   └── Parts.xml
├── text/
│   └── English.xml
└── Lua/
    ├── Autorun/
    │   └── init.lua
    └── Scripts/MyGunsmithMod/
        ├── Config.lua
        └── Config/
            ├── Platforms/ExamplePlatform.lua
            ├── Parts/ExampleParts.lua
            └── Weapons/ExampleWeapon.lua
```

### 最小可运行示例

这个示例仅包含一个 `receiver` 根部件，完成后即可被框架识别、合成贴图、打开改装 UI 并保存状态。

#### 第一步：配置模组清单与入口

**`ModConfig.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<ModConfig>
  <Lua File="%ModDir%/Lua/Autorun/init.lua" IsAutorun="true" />
</ModConfig>
```

**`filelist.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<contentpackage name="My Gunsmith Mod" modversion="1.0.0" corepackage="False" gameversion="1.12.7.0">
  <Item file="%ModDir%/Items/Weapons.xml" />
  <Item file="%ModDir%/Items/Parts.xml" />
  <Text file="%ModDir%/text/English.xml" />
</contentpackage>
```

**`Lua/Autorun/init.lua`**（同时支持直接注册与延迟注册）
```lua
local package = {
    modDir = ...,
    id = "my_gunsmith_mod",
    name = "My Gunsmith Mod",
    entry = "Lua/Scripts/MyGunsmithMod/Config.lua",
    localizationFiles = { "text/English.xml" }
}

GunsmithFramework = GunsmithFramework or {}
if GunsmithFramework.RegisterPackage then
    GunsmithFramework.RegisterPackage(package)
else
    GunsmithFramework.PendingPackages = GunsmithFramework.PendingPackages or {}
    table.insert(GunsmithFramework.PendingPackages, package)
end
```

**`Lua/Scripts/MyGunsmithMod/Config.lua`**
```lua
local package = GunsmithFramework.CurrentPackage()
local configPath = package.modDir .. "/Lua/Scripts/MyGunsmithMod/Config"

dofile(configPath .. "/Platforms/ExamplePlatform.lua")
dofile(configPath .. "/Parts/ExampleParts.lua")
dofile(configPath .. "/Weapons/ExampleWeapon.lua")
```

#### 第二步：声明 Platform、Part 与 Weapon

**`Config/Platforms/ExamplePlatform.lua`**
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

**`Config/Parts/ExampleParts.lua`**
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

**`Config/Weapons/ExampleWeapon.lua`**
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
    preview = { padding = 12, scale = 1.0, offset = { x = 0, y = 0 } },
    inventory = { scale = 1.0, rotation = 0, padding = 0 },
    world = { scale = 1.0, rotation = 0, padding = 0, offset = { x = 0, y = 0 } }
}
```

#### 第三步：添加 XML 物品与文本

**`Items/Weapons.xml`**（必须带有 `<GunsmithData />`）
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

**`text/English.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<infotexts language="English">
  <entityname.my_rifle>My Rifle</entityname.my_rifle>
  <my.gunsmith.path.receiver>Receiver</my.gunsmith.path.receiver>
  <my.gunsmith.part.receiver>Example Receiver</my.gunsmith.part.receiver>
</infotexts>
```

#### 第四步：游戏内验证

进游戏后打开控制台运行：

```text
GunsmithFrameworkValidate
```

输出 `0 errors` 后，拿起 `my_rifle` 按 `F1` 即可打开完整改装 UI。

---

## 2. 核心配置参考

> [!TIP]
> **本文档的 Lua 类型约定**：
> - `point`：`{ x = 0, y = 0 }`（坐标点或偏移）
> - `rect`：`{ x = 0, y = 0, w = 128, h = 64 }`（图片像素裁切区域）
> - `string array`：`{ "a", "b" }`（**必须是连续数组**，不能写成键值表）
> - `path`：`"receiver/barrel/muzzle"`（由 root slot 与各级 mount path 拼接）

### 2.1 包级配置（`RegisterPackage`）

`GunsmithFramework.RegisterPackage(package)` 接受一个配置表（或直接传字符串作为 `entry`）。若在 `Autorun` 执行时框架尚未加载，把同一个表放入 `GunsmithFramework.PendingPackages`，框架启动后会自动依次注册。

```lua
GunsmithFramework.RegisterPackage({
    modDir = ...,
    id = "my_pack",
    name = "My Pack",
    entry = "Lua/Scripts/MyPack/Config.lua",
    localizationFiles = { "text/English.xml", "text/Chinese.xml" },
    -- 可选：覆盖框架 UI 文案前缀，不写也能正常工作
    -- localizationPrefix = "my.gunsmith",
    -- 可选：显式引用其他包的配置
    -- imports = { "shared_gunsmith_pack" },
    weaponTags = { "my_gunsmith_weapon" },
    partTags = { "my_gunsmith_part" },
    override = false
})
```

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `entry` | string | **必填** | 相对 `modDir` 的 Lua 配置入口（可放在模组内任意路径）。 |
| `modDir` | string | 从调用来源推断 | 模组根目录；Autorun 中通常写 `modDir = ...`。 |
| `modPath` | string | 无 | `modDir` 的别名。 |
| `id` | string | 从目录名推断 | 包唯一 ID，用于配置归属隔离和重复定义检测。 |
| `name` | string | 从 filelist/目录推断 | 显示与日志用名称。 |
| `localizationFiles` | string array | 从 filelist `<Text>` 推断 | 校验本地化 key 时读取的文本文件列表。 |
| `localizationPrefix` | string | `id .. ".gunsmith"` 或自动推断 | 可选。覆盖此包的框架 UI 文案前缀（详见[第 4 节](#4-本地化配置)）。 |
| `imports` | string array | `{}` | 可选。允许本包引用其他已注册包的 platform/part/weapon/preset。 |
| `weaponTags` | string array | 从 XML tags 推断 | 注册为 Gunsmith 武器标签。 |
| `partTags` | string array | 从 XML tags 推断 | 注入 Quick Slot 容器时允许放入的部件标签。 |
| `override` | boolean | `false` | 为 `true` 时允许覆盖其他包同 ID 的 platform/weapon/part/preset。 |

在配置入口中可通过 `GunsmithFramework.CurrentPackage()` 获取当前包信息（如 `package.modDir`）。

#### Owner 隔离规则

- 通过 `RegisterPackage` 加载的所有 platform、weapon、part、NPC preset 都会自动记录所属 package。
- **默认禁止跨包引用**：weapon 的 `platform` / root part、`defaultPart`、NPC preset 以及 UI / Quick UI 候选项均按 owner 过滤。
- 如需复用其他包的内容，声明 `imports = { "other_package_id" }`（被引入的包必须已注册）。
- `imports` 仅允许**引用**，不允许覆盖同名配置；覆盖必须显式设置 `override = true`。
- 建议所有全局配置 ID 均带模组前缀（如 `my_mod_ar_platform`、`my_mod_receiver`），避免键名冲突。
- **不要**绕过 `RegisterPackage` 直接写全局 `GunsmithFramework.Config`；校验器会将无 owner 的条目报为错误。

---

### 2.2 Platform（枪械平台）

Platform 定义一类武器共享的合成画布、根槽结构与路径显示名。

```lua
GunsmithFramework.Config.platforms.my_platform = {
    canvas = { w = 512, h = 160 },
    rootSlots = {
        { path = "receiver", hidden = true }
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

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `canvas.w` / `canvas.h` | number | 是 | 合成整枪贴图的画布宽高（像素）。 |
| `rootSlots` | array | 是 | 根槽数组，必须写成 `{ { path = "receiver" } }`，不能写成键值表。 |
| `rootSlots[].path` | string | 是 | 根槽路径段，**不能包含 `/`**。 |
| `rootSlots[].hidden` | boolean | 否 | 设为 `true` 时隐藏该根槽，将其子挂点直接提升到 UI 根层显示。 |
| `pathNameKeys` | table | 是 | 路径段到本地化 key 的映射；root path 与 requiredSlots 的每一段都需要对应 key。 |
| `requiredSlots` | string array | 否 | 重要子槽列表。仅当**存在且仅存在一个 `hidden = true` 的根槽**时可用；路径写相对该 hidden root 的路径（不要包含 hidden root 自身）。 |
| `localizationPrefix` | string | 否 | 可选。覆盖该 platform 的路径 UI 文案前缀，大多数模组可省略。 |

> [!IMPORTANT]
> **重要部件（禁射）规则**：
> - `rootSlots` 中的根槽**天生就是重要槽**，无需也不应写 `required = true`。
> - 重要部件（根槽或 `requiredSlots`）允许玩家拆空，但**只要有任意重要槽为空，主武器就无法开火**，尝试开火时会提示玩家。
> - `requiredSlots` 必须是数组（如 `{ "barrel", "stock" }`），不能写成 `{ barrel = true }`；没有 hidden root 时不要写此字段。
> - 若武器使用 `SwitchableRangedWeapon` 切换到下挂武器（索引 `1`），下挂武器仍可开火，重要部件限制仅作用于主武器。

---

### 2.3 Weapon（武器）

Weapon 配置的键名必须与 XML `<Item identifier="...">` 完全一致。

```lua
GunsmithFramework.Config.weapons.my_rifle = {
    platform = "my_platform",
    requiresWorkbench = false,
    roots = {
        receiver = {
            part = "my_receiver",
            socket = { x = 256, y = 80 }
        }
    },
    preview = { padding = 12, scale = 1.0, offset = { x = 0, y = 0 } },
    inventory = { scale = 0.8, rotation = 0, padding = 2 },
    world = { scale = 1.0, rotation = 0, padding = 0, offset = { x = 0, y = 0 } },
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

| 字段 | 类型 | 必填 / 默认值 | 说明 |
| --- | --- | --- | --- |
| `platform` | string | **必填** | 引用 `Config.platforms[platformId]`。 |
| `roots` | table | **必填** | 必须为 platform 的每个 root slot 提供配置。 |
| `roots[path].part` | string | **必填** | 默认 root part ID，该 part 的 `type` 必须等于 `path`。 |
| `roots[path].socket` | point | **必填** | root part 在画布上的连接点 `{ x, y }`。 |
| `requiresWorkbench` | boolean | 否（`false`） | 为 `true` 时，普通 `F1` 键无法打开完整改装 UI，需通过工作台打开；`Shift+F1` 的 Quick UI 仍可用。 |
| `preview` | table | 否 | UI 预览渲染参数：`padding`（≥0，默认 `12`）、`scale`（>0，默认 `1.0`）、`offset`（默认 `{0,0}`）。 |
| `inventory` | table | 否 | 物品栏图标参数：`scale`（>0，默认 `1.0`）、`rotation`（度，默认 `0`）、`padding`（≥0，默认 `0`）。 |
| `world` | table | 否 | 世界贴图参数：`scale`（>0，默认 `1.0`）、`rotation`（度，默认 `0`）、`padding`（≥0，默认 `0`）、`offset`（默认 `{0,0}`）。 |
| `quickSlotBindings` | table | 否 | 将挂点的 `mount.quick.key` 绑定到武器 `ItemContainer` 槽位（从 `0` 开始）。每个绑定含 `slot`（必填，≥0 整数）、`itemPosOffset`（可选）、`rotation`（可选，默认 `0`）。 |
| `quickSlotTags` | string array | 否 | 覆盖动态注入 Quick Slot 的 containable tags；省略时沿用所属 package 的 `partTags`。 |
| `localizationPrefix` | string | 否 | 可选。覆盖该 weapon 的完整/快速改装 UI 文案前缀。 |

<details>
<summary>兼容旧字段：<code>weapon.quickSlots</code>（不推荐新模组使用）</summary>

`weapon.quickSlots` 仍可作为 fallback 被运行时读取，但不会经过同等严格的可达性校验，也不会从 `mount.quick` 自动推导锚点。新模组请统一使用 `mount.quick` + `weapon.quickSlotBindings`。

旧格式参考（数组）：
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

</details>

---

### 2.4 Part（部件）与 Mount（挂点）

Part 描述所有可安装部件、默认结构件与附件。

```lua
GunsmithFramework.Config.parts.my_handguard = {
    type = "handguard",
    nameKey = "my.gunsmith.part.handguard",
    uiOrder = 0,
    provides = { "my_ar_handguard" },
    excludes = { "my_conflicting_part" },
    groups = { "my_mod:long_handguard" },
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

#### Part 字段说明

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `type` | string | **是** | 部件类型。装在根槽时必须等于 root `path`；装在子挂点时必须等于 `mount.partType`（或未写时的 `mount.path`）。 |
| `nameKey` | string | **是** | UI 显示名本地化 key。 |
| `provides` | string array | **是** | 兼容性标签；必须与目标 mount 的 `accepts` 至少有一个交集。 |
| `item.identifier` | string | 条件必填 | 真实 XML 物品 identifier。非默认部件必须提供 `item.identifier` 或 `item.virtual = true`。 |
| `item.virtual` | boolean | 条件必填 | 为 `true` 表示无需真实物品。 |
| `uiOrder` | number | 否（`0`） | 配件列表排序权重，越小越靠前；同权重按 part ID 排序；`[空]` 始终排在最前。 |
| `excludes` | string array | 否 | 与指定 part ID 双向互斥（详见 [2.5](#25-配件互斥excludes-与-exclusivegroups)）。 |
| `groups` | string array | 否 | 所属分类组；配合 `Config.exclusiveGroups` 实现组间互斥。 |
| `visual` | table | 否 | 视觉图层配置。无 `visual` 的部件仍参与兼容、默认树与属性计算，但不绘制贴图。 |
| `visual.texture` | string | `visual` 存在时必填 | 贴图路径，支持 `%ModDir%`。 |
| `visual.source` | rect | `visual` 存在时必填 | `{ x, y, w, h }`，图片像素裁切区域。 |
| `visual.attachPoint` | point | 与 `relativeOffset` 二选一 | `source` 内的连接点坐标。 |
| `visual.relativeOffset` | point | 与 `attachPoint` 二选一 | 相对挂点锚点的绘制偏移。（旧字段 `visual.offset` 已废弃，请勿使用。） |
| `visual.order` | number | 否（`0`） | 图层绘制顺序；会被目标 mount 的 `visualOrder` 覆盖。 |
| `visual.scale` | number > 0 | 否（`1.0`） | 单个部件图层缩放。 |
| `stats` | table | 否 | 属性修正表，值必须为 `number`（详见[第 5 节](#5-数值属性partstats)）。 |
| `mounts` | array | 否 | 该部件安装后向下提供的子挂点数组。 |
| `quickAttachmentTransform` | table | 否 | 高级选项，仅含 `muzzleOutletOffset`（详见 [3.3](#33-枪口出口偏移与粒子跟随)）。 |

#### Mount 字段说明（`part.mounts` 必须是数组）

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | **是** | 子路径段，自动拼接到父路径后（如 `receiver/handguard/optic_mount`）。 |
| `accepts` | string array | **是** | 接受的 `provides` 标签列表。 |
| `partType` | string | 否 | 要求子部件的 `type`；省略时默认等于 `path`。 |
| `nameKey` | string | 否 | 子槽显示名本地化 key；省略时回退到 `platform.pathNameKeys[path]`。 |
| `defaultPart` | string | 否 | 父部件安装时自动安装的默认子部件。 |
| `emptyPart` | string | 否 | 选择 `[空]` 时在内部安装的虚拟占位部件，可继续提供下级挂点，且不会在 UI 中显示为第二个选项。 |
| `anchor` | point | 否 | 子挂点坐标，**相对父部件的 `visual.attachPoint`**。 |
| `visualOrder` | number | 否 | 覆盖安装在此挂点上的子部件 `visual.order`。 |
| `quick` | table | 否 | 暴露为 Quick Slot：`key`（存在 `quick` 时必填，对应 `weapon.quickSlotBindings`）、`nameKey`（可选）、`showWhenContained`（可选，string array，仅当槽内为指定 XML item 时显示原生容器槽）。 |

#### `defaultPart` 与 `emptyPart` 规则

- 两者指定的 part 都必须存在，且满足该 mount 的 `partType` 与 `accepts`。
- `emptyPart` 必须声明 `item = { virtual = true }`，且不能带 `item.identifier`。
- `emptyPart` 在状态中会被视为“已安装”，因此表示可用空挂点时**不要**将该路径列入 `requiredSlots`。
- 默认配件树最大深度为 **32**，不允许形成循环引用。

---

### 2.5 配件互斥（`excludes` 与 `exclusiveGroups`）

框架提供两种互斥方式，均在运行时按**双向互斥**处理，**无需反向重复声明**：

1. **少量点对点冲突 → 使用 `part.excludes`**
   ```lua
   parts.my_long_handguard.excludes = { "my_short_barrel" }
   ```
2. **大量类别间冲突 → 使用 `part.groups` + `Config.exclusiveGroups`**
   ```lua
   parts.my_long_handguard.groups  = { "my_mod:long_handguard" }
   parts.my_short_barrel.groups    = { "my_mod:short_barrel" }
   parts.my_short_barrel_v2.groups = { "my_mod:short_barrel" }

   GunsmithFramework.Config.exclusiveGroups["my_mod:short_barrel"] = {
       "my_mod:long_handguard"
   }
   ```
   - 同组配件之间**不会**互相冲突；只有两个配件所属的不同组在 `exclusiveGroups` 中声明了互斥关系时才会冲突。
   - 单个配件可加入多个组；组名是全局字符串，请统一加模组前缀（如 `my_mod:short_barrel`）。

---

### 2.6 贴图坐标、兼容性与存档限制

#### 贴图坐标换算公式

```text
root draw offset  = root.socket - root.attachPoint * root.scale
child anchor      = parent draw offset + (parent.attachPoint + mount.anchor) * parent.scale
child draw offset = child anchor - child.attachPoint * child.scale
```

> 推荐使用 [`GunsmithAnchorEditor/index.html`](GunsmithAnchorEditor/index.html) 可视化对齐，避免手算出错。

#### 兼容性校验顺序

尝试将某个 `part` 安装到 `path` 时，框架依次校验：

1. `part` ID 存在（且符合 owner / `imports` 权限）。
2. `part.type` 等于该 `path` 期望的部件类型。
3. 不与当前已安装的其他部件互斥（`excludes` / `exclusiveGroups`）。
4. 若为根槽：不检查 `accepts`，仅检查 `type`。
5. 若为子挂点：`mount.accepts` 必须存在，且与 `part.provides` 有交集。

#### 存档长度上限

- 武器保存状态的完整 JSON 上限为 **8192 字符**（含所有路径与部件 ID）。
- 换件前框架会校验长度，超限时直接拒绝换件并保留原存档，不会截断 JSON。配件树较深时，建议使用简短的路径段与 ID。

---

## 3. XML 接入与游戏内系统

### 3.1 武器与部件 XML（`GunsmithData`）

#### 武器 XML

每个 Gunsmith 武器必须包含 `<GunsmithData />`：

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

`<GunsmithData />` 可选属性：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `savedstate` | string | `""` | 持久化状态 JSON，由框架自动读写，不建议手写。 |
| `quickslotstart` | integer | 有 Quick Slot 时为 `1` | 动态注入 Quick Slot 子容器的起始槽位索引。 |
| `quickslotmax` | integer | 从 Lua `quickSlotBindings` 推断 | 需注入到的最大槽位索引（未注册 Lua 绑定时可手动指定）。 |
| `quickslotitems` | string | `""` | 注入 Quick Slot 的 `<Containable items="...">`。 |
| `quickslottags` | string | Lua 注册的 quick slot tags | 注入 Quick Slot 的 `<Containable tags="...">`。 |

**动态 Quick Slot 容器注入规则**：

- 若 Lua 绑定的最大 `slot` 超出武器 `ItemContainer` 现有容量，框架会自动追加隐藏 `SubContainer`（`capacity="1"`、`maxstacksize="1"`、`hide="true"`、`itempos="0,0"`、`setactive="true"`）。
- 若 Quick Slot 范围内复用了你在 XML 中手写的 `SubContainer`，框架会为其启用 `setactive="true"` 并补充相同的配件 items/tags 规则，同时**保留**原有的位置、隐藏设置与 `StatusEffect`（使手电等组件可正常工作）。
- 若武器自带弹匣槽，通常保留 slot `0` 给弹匣，Quick Slot 从 `1` 开始。

#### 部件 XML

真实部件只需编写普通 XML Item，其 `identifier` 必须与 `part.item.identifier` 一致（框架按 `identifier` 匹配，不按名称匹配）：

```xml
<Item identifier="my_suppressor_item" nameidentifier="entityname.my_suppressor_item" category="Weapon" tags="smallitem,my_gunsmith_part">
  <Sprite texture="%ModDir%/Items/my_parts.png" sourcerect="0,128,64,24" depth="0.55" />
  <Body width="64" height="24" />
</Item>
```

---

### 3.2 Quick Slot 完整指南

Quick Slot 将挂点与武器自身的真实 `ItemContainer` 槽位双向同步：

```text
part.mounts[].quick.key  ──(按 key 匹配)──▶  weapon.quickSlotBindings[key]  ──▶  武器 ItemContainer 槽位（0-based）
```

> [!IMPORTANT]
> `quickSlotBindings` 的键是 **`quick.key`**，**不是**完整部件路径。完整路径由框架根据当前已安装部件树自动解析。

#### 三步配置示例

**1. 在提供挂点的父部件上声明 `mount.quick`**
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
            path = "muzzle",        -- 决定路径段
            partType = "muzzle",
            nameKey = "my.gunsmith.path.muzzle",
            accepts = { "my_muzzle" },
            anchor = { x = 152, y = 20 },
            quick = {
                key = "muzzle",     -- 武器绑定用的稳定 key（可与 path 同名，但含义不同）
                nameKey = "my.gunsmith.quick.muzzle",
                showWhenContained = { "my_suppressor_item" } -- 可选：仅控制原生槽何时显示，不影响兼容性
            }
        }
    }
}
```

**2. 在武器上绑定真实容器槽位**
```lua
weapons.my_rifle.quickSlotTags = { "my_gunsmith_part" }
weapons.my_rifle.quickSlotBindings = {
    muzzle = {
        slot = 1,                           -- 必填，非负整数；同一武器下不可重复
        itemPosOffset = { x = 0, y = 0 },   -- 可选，相对 mount anchor 的显示偏移
        rotation = 0                        -- 可选，显示旋转角度
    }
}
```

**3. 确保配件 Part 与 XML 物品能互相匹配**
- `part.type` == `mount.partType`（或 `mount.path`）
- `part.provides` 与 `mount.accepts` 有交集
- `part.item.identifier` == XML `<Item identifier="...">`
- XML Item 带有 `weapon.quickSlotTags` 或所属包 `partTags` 允许的 tag

<details>
<summary>进阶示例：多层嵌套路径的 Quick Slot（如 <code>receiver/stock_mount/stock</code>）</summary>

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
            quick = { key = "stock" }   -- 写在最终提供 stock 挂点的父部件上
        }
    }
}

weapons.my_rifle.quickSlotBindings = {
    stock = { slot = 2 }                -- 仍写 quick.key，不要写完整路径
}
```

要点：
- 如果有多个可替换的 buffer tube 都希望支持快速换枪托，每个 buffer tube 都需要提供带相同 `quick.key = "stock"` 的 `stock` mount。
- 运行时仅为**当前已安装部件树中实际存在**的 Quick mount 生成槽位。

</details>

#### 运行时行为与常见校验报错

- **双向同步**：在 Quick UI 安装时，框架会从背包取对应物品放入对应槽位；玩家直接把物品拖进该槽位时，框架会反向同步配件选中状态；替换时旧物品返还背包（满则掉落）。
- **常见报错排查**：
  - `does not match a quick mount reachable from this weapon`：没有可达的 mount 声明该 `quick.key`（检查拼写、父链可达性、跨包权限）。
  - `.slot duplicates slot N`：两个绑定共用了同一个槽位号。
  - `.slot must be a non-negative integer`：`slot` 必须是 `0, 1, 2...` 非负整数。
  - Quick UI 中看不到某嵌套挂点：确认当前已安装的父部件确实声明了 `quick`（路径存在 ≠ 声明了 `quick`）。
  - 能显示但装不上真实物品：检查 `part.item.identifier`、XML identifier、XML tag 以及 `type`/`provides` 兼容性。

---

### 3.3 枪口出口偏移与粒子跟随

如果枪口附件（消音器、制退器）或下挂武器改变了实际枪口位置，想让枪口火光/火花等粒子自动跟随当前枪口，按下面两步配合使用。

> [!NOTE]
> **普通部件不需要此字段**：瞄具、握把、只改外观的护木/枪托均无需配置 `quickAttachmentTransform`，常规贴图对齐仍只看 `visual.attachPoint` 和 `mount.anchor`。

#### 1. Lua：在配件上声明 `muzzleOutletOffset`

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
        -- 单位为 Gunsmith canvas 像素（x 向右，y 向下），相对该 Quick Slot 的 anchor 偏移
        muzzleOutletOffset = { x = 60, y = 0 }
    }
}
```

- 目前仅识别两个 `quick.key`：
  - **`muzzle`**：主枪口（注册为 `primary` barrel rule）。
  - **`lower_rail`**：专为 **Vanilla-Components-Expanded (VCE)** 下挂武器准备的兼容路径（仅在所装部件声明了 `muzzleOutletOffset` 时注册；不使用 VCE 时普通下导轨附件无需使用该 key）。
- 省略此字段等价于 `{ x = 0, y = 0 }`（无额外偏移）。

#### 2. XML：给粒子 `StatusEffect` 添加 tag

```xml
<RangedWeapon barrelpos="0,0">
  <StatusEffect type="OnUse" target="This" statuseffecttags="Gunsmith_BarrelParticle">
    <ParticleEmitter particle="muzzleflash" particleamount="1" />
    <ParticleEmitter particle="spark" particleamount="3" />
  </StatusEffect>
</RangedWeapon>
```

**注意事项**：
- 只有带 `statuseffecttags="Gunsmith_BarrelParticle"` 的 `StatusEffect` 会被改写生成位置，且必须挂在带 `RangedWeapon` 的武器上，否则报错。
- 未注册 barrel transform 时自动回退到 XML `RangedWeapon.BarrelPos`；使用 VCE 时，projectile selector `0` 映射 `primary`，`1` 映射 `lower_rail`（下挂未声明偏移时自动回退主枪口）。
- 请勿再为不同枪口手写多套固定 `barrelpos` / `Distance` 来手动偏移粒子。
- **适用**：枪口火光（flash）、火花（spark）等需要跟随枪口的粒子。**不适用**：音效、爆炸、抛壳以及不依赖 `StatusEffect` 世界坐标的自定义逻辑。

---

### 3.4 工作台专用改装与 Fabricator 配方过滤

#### 工作台专用改装（`requiresWorkbench = true`）

- 加工台默认接受**所有** Gunsmith 武器；设置 `requiresWorkbench = true` 仅限制离开工作台后的**完整改装 UI（`F1` 键）**，不影响工作台接收武器，也不影响 `Shift+F1` 快速改装。
- **客户端私有虚拟枪械槽**：
  - 玩家将背包中的枪拖入枪械槽后，仅当前客户端保存引用并筛选配方；点击独立“打开”按钮才进入完整改装页。
  - 枪械**不会**离开玩家背包或进入加工台容器，其他玩家也不可见。
  - 拖出槽外松手取消选择，拖回槽内保留，拖入另一把枪直接切换（不会移动或丢弃背包中的枪）。
  - 改装请求仍由服务端校验枪械归属、工作台可用性与配置模式；制造队列沿用原版共享队列，造出的部件不会自动安装。

#### Fabricator 配方过滤

框架可在加工台左侧分类栏加入 **Gunsmith 分类按钮**：

- **原版 `fabricator` 默认启用**。
- **自定义加工台**只需在 `<Fabricator>` 上添加 `gunsmithframeworkbutton="true"`（无需额外手写 `ItemContainer`）：
  ```xml
  <Item identifier="my_gunsmith_fabricator" tags="my_gunsmith_fabricator" category="Machine">
    <Sprite texture="%ModDir%/Items/my_fabricator.png" />
    <Body width="160" height="96" />
    <Fabricator canbeselected="true" gunsmithframeworkbutton="true" />
  </Item>
  ```
- **仅过滤原版配方（不凭空生成）**：部件 XML 物品必须自带原版 `<Fabricate suitablefabricators="...">`，且物品 `identifier` 等于该枪理论上可安装的某个 `part.item.identifier`。
  ```xml
  <Item identifier="my_suppressor_item" nameidentifier="entityname.my_suppressor_item" category="Weapon" tags="smallitem,my_gunsmith_part">
    <Sprite texture="%ModDir%/Items/my_parts.png" sourcerect="0,0,64,24" />
    <Body width="64" height="24" />
    <Fabricate suitablefabricators="my_gunsmith_fabricator" requiredtime="8">
      <RequiredItem identifier="steel" />
    </Fabricate>
  </Item>
  ```
- **列表排序与展示**：
  - 未选择枪械时不显示配方；选枪后从武器 root parts 递归扫描所有**理论上可安装**的配件（不因当前已装配件动态缩窄）。
  - 默认按配件 `type` 分组（带组标题与分隔线，名称取 `pathNameKeys` / `path.<type>` / 框架类型名 / 原始 `type`），组内按名称排序。
  - 搜索、“仅显示可用”与原版排序下拉框（名称/技能/价格）均可用；切回原版分类或关闭加工台后清除选择并恢复原版状态。

> [!TIP]
> **Gunsmith 分类页为空？优先查这 3 项**：
> 1. 武器 XML 是否有 `<GunsmithData />`，且 Lua `Config.weapons[identifier]` 已注册。
> 2. 目标 part 是否有 `item = { identifier = "..." }`，且对应 XML 物品真实存在。
> 3. 该 XML 物品的 `<Fabricate suitablefabricators="...">` 是否包含当前加工台的 identifier。

---

### 3.5 NPC 预设（NPC Presets）

#### 1. Lua 中定义预设 Profile

```lua
local profiles = GunsmithFramework.Config.npcPresets.profiles

profiles.guard_rifle = {
    weapon = "my_rifle",    -- 可选但推荐：限定武器 XML identifier，不匹配时忽略并警告
    parts = {               -- path -> part id；未列出的路径保持默认
        receiver = "my_receiver",
        ["receiver/barrel"] = "my_barrel_short",
        ["receiver/handguard"] = "my_handguard",
        ["receiver/handguard/top_rail/optic_mount"] = "my_red_dot",
        ["receiver/handguard/bottom_rail/grip_mount"] = "my_vertical_grip",
        ["receiver/barrel/muzzle"] = "my_suppressor"
    }
}
```

#### 2. XML 中引用预设

```xml
<Item identifier="my_rifle" gunsmithpreset="guard_rifle">
  <Item identifier="my_magazine" />
</Item>
```

> [!WARNING]
> **不要**在 NPC XML 里把瞄具、握把、枪口等 Quick Slot 配件手动塞进武器容器。把它们写在 Lua profile 的 `parts` 中，框架会自动为带有 `part.item.identifier` 的 Quick Slot 部件生成真实物品（若无 `item.identifier`，则仅生效视觉与属性）。

**应用规则**：
- 先构建默认配置，再批量应用 `parts`，**整套替换完成后**再做互斥检查（不会被即将替换掉的默认件阻挡）；无效路径或冲突部件会被修剪并输出警告。
- 单机与服务端会保存应用后的预设；若武器已有非空 `SavedState`，不会覆盖。
- 多人客户端以服务端下发的配件选择为准，不受容器物品网络到达顺序影响。

---

## 4. 本地化配置

框架校验以下 5 处本地化 key：

1. `platform.pathNameKeys`
2. `part.nameKey`
3. `mount.nameKey`
4. `mount.quick.nameKey`
5. 可选 `localizationPrefix` 下的框架 UI / action / status / stat key

### 推荐命名风格

为模组使用统一前缀，在 `text/English.xml` 等文本文件中定义完整 key：

```xml
<infotexts language="English">
  <!-- 槽位与部件名（在 Lua 的 pathNameKeys / nameKey 中填写完整 key） -->
  <my.gunsmith.path.receiver>Receiver</my.gunsmith.path.receiver>
  <my.gunsmith.path.barrel>Barrel</my.gunsmith.path.barrel>
  <my.gunsmith.part.receiver>Receiver</my.gunsmith.part.receiver>
  <my.gunsmith.part.suppressor>Suppressor</my.gunsmith.part.suppressor>
  <my.gunsmith.quick.muzzle>Muzzle</my.gunsmith.quick.muzzle>
</infotexts>
```

### 关于可选字段 `localizationPrefix`

- **绝大多数第三方模组不需要填写 `localizationPrefix`**。不写时，框架 UI 文案会自动使用推断的包前缀，找不到时回退到框架默认的 `gunsmith.framework.*`（或原始 key 文本）。
- 它**只影响框架自带的 UI 文案**（标题、按钮、状态、属性名），**不会**自动给 `part.nameKey` 或 `pathNameKeys` 加前缀（这两者始终要写完整 key）。
- **覆盖优先级**：
  - Weapon UI：`weapon.localizationPrefix` → 所属 `package.localizationPrefix` → `gunsmith.framework`
  - Platform 路径 UI：`platform.localizationPrefix` → 所属 `package.localizationPrefix`

<details>
<summary>查看自定义框架 UI 文案示例（声明 <code>localizationPrefix = "my.gunsmith"</code> 时）</summary>

```xml
<infotexts language="English">
  <my.gunsmith.ui.title>Modify: {0}</my.gunsmith.ui.title>
  <my.gunsmith.ui.quick_title>Quick Modify: {0}</my.gunsmith.ui.quick_title>
  <my.gunsmith.ui.weapon_root>Weapon</my.gunsmith.ui.weapon_root>
  <my.gunsmith.ui.empty_part>Empty</my.gunsmith.ui.empty_part>
  <my.gunsmith.action.install>Install</my.gunsmith.action.install>
  <my.gunsmith.action.remove>Remove</my.gunsmith.action.remove>
  <my.gunsmith.stat.ergonomics>Ergonomics</my.gunsmith.stat.ergonomics>
  <my.gunsmith.stat.none>No stat changes</my.gunsmith.stat.none>
  <my.gunsmith.stattypes.RangedSpreadReduction>Spread Reduction</my.gunsmith.stattypes.RangedSpreadReduction>
</infotexts>
```

</details>

---

## 5. 数值属性（`part.stats`）

`part.stats` 的值必须为 `number`（非数字报 `ERROR`，未知 key 报 `WARN` 且不生效）：

```lua
stats = {
    Ergonomics = 3,
    RangedSpreadReduction = 0.05,
    WeaponsSkillBonus = 10
}
```

### 生效与显示规则

- **`Ergonomics`（操控值）**：GunsmithFramework 自有属性，影响瞄准抬枪速度并显示在 UI/物品提示中；支持负值，实际抬枪速度仍受最低速度限制。
- **其他键（Barotrauma `StatTypes`）**：在角色持有该武器时叠加到角色身上。
  - **百分比类属性**（大部分 `StatTypes`）：显示与生效下限均为 **`-100%`**（生效时先与角色原属性相加，再限制不低于 `-100%`，避免伤害等倍率变负）。
  - **平值类属性**（技能 bonus/override 与少数计数型字段）：直接按平值显示与叠加，不受 `-100%` 限制。

<details>
<summary>展开查看完整支持的 Stats 键列表（共 82 项）</summary>

#### 框架自有
`Ergonomics`

#### 战斗与武器
`AttackMultiplier`, `TeamAttackMultiplier`, `RangedAttackSpeed`, `RangedAttackMultiplier`, `RangedSpreadReduction`, `MeleeAttackSpeed`, `MeleeAttackMultiplier`, `NaturalMeleeAttackMultiplier`, `DualWieldingPenaltyReduction`, `TurretAttackSpeed`, `TurretPowerCostReduction`, `TurretChargeSpeed`, `ExplosionRadiusMultiplier`, `ExplosionDamageMultiplier`, `BallastFloraDamageMultiplier`

#### 移动、生存与感知
`MaximumHealthMultiplier`, `MovementSpeed`, `WalkingSpeed`, `SwimmingSpeed`, `PropulsionSpeed`, `FlowResistance`, `HoldBreathMultiplier`, `SoundRangeMultiplier`, `SightRangeMultiplier`, `BuffDurationMultiplier`, `DebuffDurationMultiplier`, `MedicalItemEffectivenessMultiplier`, `MedicalItemApplyingMultiplier`, `BuffItemApplyingMultiplier`, `PoisonMultiplier`, `CPRBoost`

#### 技能加成、覆盖与成长
`ElectricalSkillBonus`, `HelmSkillBonus`, `MechanicalSkillBonus`, `MedicalSkillBonus`, `WeaponsSkillBonus`, `ElectricalSkillOverride`, `HelmSkillOverride`, `MechanicalSkillOverride`, `MedicalSkillOverride`, `WeaponsSkillOverride`, `SkillGainSpeed`, `ExtraLevelGain`, `ElectricalSkillGainSpeed`, `HelmSkillGainSpeed`, `MechanicalSkillGainSpeed`, `MedicalSkillGainSpeed`, `WeaponsSkillGainSpeed`, `ExperienceGainMultiplier`, `MissionExperienceGainMultiplier`, `Apprenticeship`, `LockedTalents`

#### 维修、制造与工程
`RepairSpeed`, `MechanicalRepairSpeed`, `ElectricalRepairSpeed`, `MaxRepairConditionMultiplierMechanical`, `MaxRepairConditionMultiplierElectrical`, `DeconstructorSpeedMultiplier`, `RepairToolStructureRepairMultiplier`, `RepairToolStructureDamageMultiplier`, `RepairToolDeattachTimeMultiplier`, `IncreaseFabricationQuality`, `FabricationSpeed`, `GeneticMaterialRefineBonus`, `GeneticMaterialTaintedProbabilityReductionOnCombine`, `TinkeringDuration`, `TinkeringStrength`, `TinkeringDamage`

#### 经济、任务与物品栏
`ReputationGainMultiplier`, `ReputationLossMultiplier`, `MissionMoneyGainMultiplier`, `ExtraMissionCount`, `ExtraSpecialSalesCount`, `StoreSellMultiplier`, `StoreBuyMultiplierAffiliated`, `StoreBuyMultiplier`, `ShipyardBuyMultiplierAffiliated`, `ShipyardBuyMultiplier`, `HireCostMultiplier`, `MaxAttachableCount`, `InventoryExtraStackSize`

</details>

---

## 6. 校验、调试与接口速查

### 游戏内控制台命令

框架启动后会自动延迟执行一次校验，开发过程中每次新增平台、武器或部件后建议手动执行：

| 命令 | 说明 |
| --- | --- |
| `GunsmithFrameworkValidate` | 校验全部已注册配置，输出 `ERROR` / `WARN` 汇总。 |
| `GunsmithFrameworkValidationSelfTest` | 构造一份故意错误的配置，用于确认校验器正常工作。 |

**结果判读**：
- **`ERROR`**：配置不合法，对应功能可能完全不可用。
- **`WARN`**：可继续运行，但存在缺失 XML prefab、本地化 key 或无 mount 接受的 `provides` 等问题。
- **`0 errors`**：Schema 校验通过；仍需进游戏实测坐标、贴图、Quick Slot 与物品返还。

### 作者接口速查

```lua
-- 配置表（第三方模组主要使用这些表）
GunsmithFramework.Config.platforms
GunsmithFramework.Config.weapons
GunsmithFramework.Config.parts
GunsmithFramework.Config.exclusiveGroups
GunsmithFramework.Config.npcPresets.profiles

-- 常用函数
GunsmithFramework.RegisterPackage(package) -- 注册扩展包
GunsmithFramework.CurrentPackage()         -- 在入口脚本中获取当前包信息
GunsmithFramework.Apply(item)              -- 手动应用/刷新武器状态（高级用法）
GunsmithFramework.Open(item)               -- 手动打开完整改装 UI（高级用法）
GunsmithFramework.OpenQuick(item)          -- 手动打开快速改装 UI（高级用法）
```
