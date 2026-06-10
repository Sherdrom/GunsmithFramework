# GunSmith Anchor Editor 使用说明

`GunSmith Anchor Editor` 是 GunSmith 的外部坐标生产工具，用于快速制作 `source / attachPoint / roots.receiver.socket / mount.anchor`。它是单文件 HTML 工具，不需要安装依赖，也不会读取或修改 Lua 配置文件。

打开方式：

```text
GunsmithAnchorEditor/index.html
```

直接用浏览器打开即可。

## 工具用途

这个工具主要解决四件事：

- 从精灵图上框选 `visual.source`。
- 在 source 内取 `visual.attachPoint`。
- 在平台预览画布上取 `weapon.roots.receiver.socket`。
- 在父配件上取子配件的 `mount.anchor`。

工具会在下方预览画布里按 GunSmith 当前坐标规则近似拼接整枪，帮助快速判断部件是否对齐。最终效果仍以游戏内 GunSmith 合成为准。

## 界面区域

左侧是项目与工具：

- `精灵图`：导入一张或多张 PNG/JPG 等图片。选择当前 GunSmith 项目内的图片时，工具会按文件名和文件大小匹配项目相对路径。
- `查看图片`：切换上方 source 编辑画布显示的图片，不会修改部件 texture；框内右侧的 `×` 会从当前工程移除选中的导入图片，不会删除磁盘文件。
- `保存工程`：如果工程是通过支持文件写入的浏览器打开的，会直接写回当前 JSON；否则按当前模式导出 JSON。
- `导出相对路径 JSON`：选择新 JSON 位置保存当前工程，不写入图片 DataURL。
- `导出内嵌图片 JSON`：选择新 JSON 位置保存当前工程，并把图片 DataURL 写入 JSON。
- `打开工程 JSON`：继续之前的工具工程。
- `项目根目录`：当浏览器隐藏 JSON 的真实路径导致图片未加载时，选择包含 `filelist.xml` 的 Mod 根目录，工具会按 JSON 内保存的相对路径重新读取图片。
- `模式`：切换当前鼠标操作。
- `缩放`：调整上方精灵图画布缩放。
- `背景`：切换深色、白色、绿色背景。
- `画布宽/高`：平台 canvas 尺寸，AR 当前通常是 `512 x 260`。
- `root x/y`：当前 `roots.receiver.socket`。
- `填入 M4 示例`：载入现有 M4 坐标示例，适合理解工具行为。

中间是两个画布：

- 上方画布：精灵图/source 编辑区。
- 下方画布：当前武器拼接预览区。

右侧是部件与导出：

- 上方列表：当前工程里的部件。
- 表单：当前选中部件的字段。
- 导出区：生成可粘贴到 Lua 配置里的片段。

## 右侧字段含义

基础字段：

- `part id`：Lua part id，例如 `AR_barrel_std`。
- `名称`：显示名。
- `type`：配件类型，例如 `receiver / barrel / handguard`。
- `texture`：当前部件使用的图片。只有修改这个字段才会改变该部件导出的 `visual.texture`。
- `父部件`：当前部件挂在哪个父部件上；receiver 通常是 root。
- `path`：父部件 mount path，例如 `barrel / handguard / top_rail`。
- `order`：绘制顺序，越小越早绘制。
- `scale`：单个配件图层缩放。

坐标字段：

```text
source 起点 x / y      -> visual.source.x / visual.source.y
source 尺寸 w / h      -> visual.source.w / visual.source.h
attachPoint x / y      -> visual.attachPoint.x / visual.attachPoint.y
mount.anchor x / y     -> 当前部件挂到父部件时，父部件上的挂点
```

注意：`mount.anchor` 是相对父配件 `attachPoint` 的坐标，不是原始图片绝对坐标。

## 基本流程

1. 打开 `index.html`。
2. 导入精灵图。
3. 可先点击 `填入 M4 示例` 学习现有配置。
4. 新增或选择一个部件。
5. 切换到 `框 source`，在上方精灵图画布框出该部件。
6. 切换到 `取 attachPoint`，点击该部件的连接点。
7. 如果当前部件是 receiver，切换到 `取 rootSocket`，在下方预览画布放置 receiver 根点。
8. 如果当前部件是子部件，设置 `父部件`，切换到 `取 mount.anchor`，在下方预览画布点击父部件上的安装点。
9. 观察下方预览，必要时微调。
10. 点击 `生成 Lua 片段`。
11. 将生成结果复制到对应 Lua 配置文件。

## Source 框选与调整

在 `框 source` 模式下：

- 左键拖动空白处：新建 source 矩形。
- 鼠标停在选中 source 的边缘：拖动单边调整宽/高。
- 鼠标停在选中 source 的四角：斜向缩放矩形。
- source 框上的黄色小方块就是可拖拽调整点。

如果只需要移动 source 框本身，可以在 `框 source` 模式下用方向键微调 `source.x/y`。

## 取点与微调

取点模式：

- `取 attachPoint`：上方精灵图画布点击，设置当前部件的 `attachPoint`。
- `取 rootSocket`：下方预览画布点击，设置 `roots.receiver.socket`。
- `取 mount.anchor`：下方预览画布点击，设置当前部件相对父部件的 `mount.anchor`。

键盘微调：

- 方向键：每次移动 1 像素。
- 小键盘 `8 / 2 / 4 / 6`：同方向键。
- 按住 `Shift`：每次移动 10 像素。

微调对象取决于当前模式：

- `框 source`：移动 `source.x/y`。
- `取 attachPoint`：移动 `attachPoint.x/y`。
- `取 rootSocket`：移动 `roots.receiver.socket.x/y`。
- `取 mount.anchor`：移动 `mount.anchor.x/y`。

当输入框、下拉框或导出文本框正在聚焦时，方向键不会触发微调，避免影响文本编辑。

## 视图操作

两个画布都支持：

- 鼠标滚轮：以鼠标位置为中心缩放。
- 鼠标中键拖动：移动视图。

此外，切换到 `移动视图` 模式后，也可以用左键拖动画布。

背景颜色：

- `深色`：默认调参背景。
- `白色`：检查深色素材边缘。
- `绿色`：检查透明边缘和世界图抠图感。

## 部件列表

右侧部件列表用于维护当前工具工程里的拼接对象。

- `新增部件`：创建一个新部件，默认父部件为当前选中部件。
- `复制部件`：复制当前部件。
- `删除部件`：删除当前部件；如果其他部件挂在它下面，会自动改回 root。
- 点击部件列表项：切换当前选中部件。
- 点击部件左侧眼睛按钮：显示或隐藏该部件在下方预览中的图层；隐藏状态会保存到工程 JSON。
- 删除导入图片后，使用该图片的部件会自动切换到剩余的当前图片；如果没有剩余图片，部件会保留在无图片状态等待重新导入。

建议制作 AR 枪时的顺序：

1. receiver
2. barrel
3. handguard
4. pistol_grip
5. stock
6. rails / optics / foregrip 等附件

## 导出 Lua 片段

点击 `生成 Lua 片段` 后，导出区会生成：

- `weapon.roots`
- 当前部件 `visual`
- 当前部件的子挂点 `mounts`
- 一个 receiver 风格的完整 part 示例

工具不会自动填好 `accepts / provides / item.identifier`，这些仍需要按真实配置手动补齐。

导出后通常放到这些文件：

- 武器 roots：`Lua/Scripts/Gunsmith/Config/Weapons/.../<Weapon>.lua`
- receiver：当前武器配置文件中，或独立武器部件文件。
- AR 共享结构件：`Lua/Scripts/Gunsmith/Config/Parts/AR/Structural/`
- 共享附件：`Lua/Scripts/Gunsmith/Config/Parts/Shared/`

## 保存工程

`保存工程` / `导出相对路径 JSON` / `导出内嵌图片 JSON` 会保存：

- 当前导入的多张图片。相对路径导出只保存项目相对路径；内嵌图片导出会同时保存项目相对路径和 DataURL。
- 当前部件列表。
- source / attachPoint / anchor。
- roots.receiver.socket。
- canvas 尺寸。
- 背景色。
- 部件图层显示/隐藏状态。

这个 JSON 只给工具继续编辑使用，不是 GunSmith 运行时配置。

如果 JSON 里图片使用相对路径，路径按项目根目录计算。项目根目录以 `filelist.xml` 为标记：导入工程 JSON 时，工具会优先根据浏览器提供的 JSON 文件位置向上查找 `filelist.xml`，找到后再按相对路径读取图片。普通浏览器在单独选择文件时可能不会暴露 JSON 的真实父目录；这种情况下图片条目会保留但标记为未加载，点击 `项目根目录` 并选择包含 `filelist.xml` 的 Mod 根目录即可重新读取。
导入新版内嵌图片 JSON 后，工具会直接读取其中的相对路径。兼容旧版内嵌 JSON 时，也会用图片名和 DataURL 解码后的字节大小反向匹配项目相对路径；匹配不到时，相对路径导出会只保留图片名。
如果浏览器阻止从相对路径图片反读 DataURL，内嵌图片导出会请求选择一次项目根目录，并按相对路径读取图片文件。

## 常见问题

### 为什么工具里对齐了，游戏里还有一点偏差？

工具预览是为了快速取点，最终仍要以游戏内 C# 合成为准。出现偏差时优先检查：

- Lua 配置里的 `visual.scale` 是否和工具一致。
- `visual.scale` 是否和工具中一致。
- 复制 Lua 片段时是否贴到了正确部件。
- 父部件是否和工具里选择的父部件一致。

### mount.anchor 为什么是负数？

这是正常的。`mount.anchor` 是从父配件 `attachPoint` 出发的相对坐标。向左或向上时就会是负数。

### attachPoint 应该点哪里？

点“这个配件贴到父配件的位置”。

例如：

- 枪管：枪管尾部。
- 护木：护木后端。
- 握把：握把顶部。
- 枪托：枪托前端。
- 瞄具：瞄具底部导轨接触点。

### source 要不要包含透明边？

可以包含少量透明边，但不要太大。透明边过大可能让取点和图标裁切更难判断。

## 推荐检查清单

导出并粘贴配置后：

- 运行 `GunsmithFrameworkValidate`。
- 检查 M4/HK416 默认外观没有被影响。
- 检查新部件在 preview、inventory、world 中都没有明显裁切。
- 替换父配件后，子挂点能正确跟随。
- 进游戏做一次保存、退出、重载验证。
