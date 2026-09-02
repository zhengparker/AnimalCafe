# Phase 7 — Interior Walls & Surface Customization Design

> 状态：`Approved — Studio Owner approved the Surface transaction and review amendment on 2026-08-27`
>
> 日期：2026-08-24；本轮修订：2026-08-27
>
> 本文档记录 Studio Owner 已批准的 Phase 7 设计。Implementation 必须先遵循对应 implementation plan 与 TDD gate，不得静默扩展 scope。

## 1. Goal

在固定墙体结构中扩展现有 `Decoration Mode`，让玩家能够：

- 更换 Floor、Paint、Wallpaper 与 Wainscoting；
- 放置、移动与 Store Wall Decor 和 wall-mounted Window；
- 在 Preview 中安全检查修改，并通过 Confirm 或 Cancel 决定是否提交；
- 使用适合手机的 Catalogue 浏览更多 Category 与 Item。

Phase 7 不建造、移动或删除墙体，不切割真实 Door / Window Opening，也不引入装修价格、库存或解锁系统。

## 2. Decoration Mode 与 Bottom Sheet

### 2.1 Mode Tabs

玩家从现有入口进入一个统一的 `Decoration Mode`。Bottom Sheet 顶部提供四个 Mode Tabs：

1. `Furniture`
2. `Floor`
3. `Wall`
4. `Wall Decor`

Tab 不只是 Catalogue filter；每个 Tab 会切换 Scene selection、Preview、validity feedback 与 actions 的编辑 Mode。

- Mode Tabs 采用从 Bottom Sheet 左上边缘向上伸出的 folder-tab / 标签页造型，不使用同一平面内的 pill segmented control。
- Active Tab 位于最高视觉图层、位置略高并使用明确 highlight；它必须完整盖在相邻 Inactive Tabs 前方，不能被任何 Inactive Tab 遮挡。
- Inactive Tabs 略微下沉到后层，并使用克制的阴影表达叠放关系。
- Tab 仍需满足 mobile touch target，叠放效果不得缩小实际可点击区域。

| Tab | 可选择内容 | 不响应的内容 |
|---|---|---|
| Furniture | Floor Furniture | Floor Surface、Wall Surface、Wall Decor |
| Floor | Floor Grid / Floor Surface | Wall、Furniture、Wall-mounted objects |
| Wall | 整面 Wall Surface | Floor、Furniture、Wall-mounted objects |
| Wall Decor | Wall Slots、Wall Decor、Window | Floor Surface、Furniture |

不同 Mode 的 Scene hit testing 必须隔离，避免玩家装修地面时误选墙面，或摆放墙饰时误选家具。

### 2.2 Catalogue 布局

- Bottom Sheet 内不再使用从上到下的单列 Furniture list。
- 每个 Item 使用接近正方形的大预览卡片。
- Furniture 卡片把 thumbnail 作为主要面积；正式版文字区尽可能小，只显示 Item name，不显示 Footprint size。
- 同一 Category 的 Item 横向排列并支持 horizontal scroll。
- 不同 Category 纵向排列；未来 Category 增加后，Bottom Sheet 支持 vertical scroll。
- Horizontal row 不显示 `Swipe →` 或 Scrollbar；右侧露出下一张卡片的一部分，提示仍可继续横向滑动。
- Vertical list 同样通过露出下一行的一部分提示仍可向下浏览。
- 当前 `Furniture` 只有一行 `Furniture` Category，并显示 4 个现有 Catalogue presets。
- 未来可扩展为 Tables、Chairs、Counters、Decor 等多行 Category，不改变整体结构。
- Catalogue thumbnail 使用预先生成并纳入版本控制的 Sprite；正常 builder、Catalogue 和 runtime 不为卡片运行 3D Camera / RenderTexture。
- 3 个 Wall Decor 与 2 个 Window thumbnail 必须保留对应真实游戏 prefab 安装到 Back-left Wall 时的轻微 `3/4` 机内视角，但最终只保留物品本身并导出 genuine alpha transparency；不保留墙面、地面、地脚线、黑底或 baked checkerboard。
- 这 5 张正式 thumbnail 属于 authored art assets。`BuildOrUpdateAssets` 只引用而不重烘焙；Validator 同时检查 imported `256×256`、approved hash、transparent border 与 non-empty item 语义。文件缺失或被替换时必须明确失败并由受控 Art intake 更新，不能在常规自动化中启动 GPU renderer 隐式修复。

### 2.3 Wall Catalogue

`Wall` Tab 当前包含三行：

1. `Wallpaper`
2. `Paint`
3. `Wainscoting`

每行独立 horizontal scroll；未来新增 Surface assets 时不改变 Category 结构。

Surface 没有数量限制。Surface 卡片以 swatch 本身为主要内容，不使用名称或状态文字占据卡片空间。状态规则为：

- `Available`：可选择但当前未使用；
- `Using`：在 swatch 中央显示绿色勾；
- `Preview`：使用明确的彩色外框；
- `Unavailable / Invalid`：如未来出现不兼容条件时使用，Phase 7 首批素材默认全部可用。

`Using` 依靠 check icon，`Preview` 依靠 outline shape，因此两者不只靠色相区别。卡片不显示 `Using`、`Preview` 或 `Available` 文字。

`Floor` Surface cards 复用与 `Wall` Surface cards 完全相同的 image-only、Using check 与 Preview outline 视觉语法。

`Wainscoting` row 必须始终提供一个 `None / No Wainscoting` 选项，让玩家移除当前 Wainscoting、恢复只显示 Base Surface。该卡片使用清楚的 crossed-circle icon，不依赖名称文字；它与其他 Surface 一样支持 Using check 与 Preview outline。

### 2.4 Preview 与 Tab switching

- 系统一次只允许一个 active Preview transaction。
- 有尚未 Confirm 的 Preview 时切换 Mode Tab，玩家必须先 `Confirm` 或 `Cancel`。
- 系统不自动保存，也不因切换 Tab 静默丢弃 Preview。
- 普通点击当前 Mode 不支持的 Scene object 不会切换 Mode，也不会打断 Preview。
- 每次新进入 Decoration Mode 默认打开 `Furniture`；同一次 Decoration Mode 内记住当前 Tab，退出后不跨 session 保存。
- 有 active Preview 时尝试退出 Decoration Mode，显示 `Continue Editing / Discard Changes` confirmation；绝不自动 Confirm。

### 2.5 Bottom Sheet snap states

Bottom Sheet 使用固定 snap states，不停留在任意高度：

- `Expanded`：显示 Tabs、Category rows、Item cards 与固定 footer。
- `Compact Preview`：显示 Tabs、简短 current-selection state 与固定 footer；隐藏 Category rows 与 cards。
- `Tabs Only`：只显示四个 Mode Tabs，仅在没有 active Preview 时可用。

Bottom Sheet 收起时四个 Tabs 始终可见。Active Preview 期间最低只能缩到 `Compact Preview`，保证 Confirm / Cancel 始终可见。

- Floor / Wall Preview 默认保持 Expanded，玩家可以主动下拉到 Compact Preview。
- Furniture / Wall Decor 创建或编辑 Preview 后，Catalogue 自动收起为 Compact Preview，腾出 Scene 操作空间。
- Phase 7 Bottom Sheet 使用带圆角的暖色大卡片外观；内容与面板边缘之间保留固定 inner margin，Item cards 紧密横向排列但不互相贴住。
- Folder Tabs 与 Sheet 顶边无缝连接，不得出现可见 gap；Tabs、Catalogue 内容与 footer 必须作为同一 Sheet hierarchy 一起移动。
- Expanded / Compact 的位置变化使用 `0.16s` 平滑 transition；动画只改变 UI layout，不创建、提交或丢弃 transaction。
- Collapsed / Compact handle 必须保留可读的 `Catalogue` label；builder 不得因清理 legacy expanded title 而误删该 label。

### 2.6 Mode actions 与 Surface footer

关键 actions 不放入 horizontal scroll 或 `...`：

- 上层工具行：按 Mode 显示 `Undo Last`、`Rotate`、`Apply All` 或 `Store`。
- 下层主要操作：明显的 `Cancel` 与 `Confirm`。
- Floor 与 Wall 属于 Surface Modes；它们的 actions 使用 Bottom Sheet 内部固定 footer，并在 Expanded / Compact 状态跟随 Sheet 一起移动。
- Floor footer：`Undo Last / Rotate / Apply All` + `Cancel / Confirm`。
- Wall footer：只显示 `Cancel / Confirm`，不显示 `Apply All`。
- 选中 Wall target 后 Wall footer 立即出现；`Cancel` 可用于退出当前 wall selection，`Confirm` 在完整 Preview state 与 confirmed snapshot 不同时才 enabled。
- Furniture：沿用 `Store / Rotate` + `Cancel / Confirm`。
- Wall Decor / Window：`Store` + `Cancel / Confirm`，不显示 Rotate。
- `Store` 只在编辑 existing instance 时显示；new Preview 不显示 Store。

## 3. Surface application model

### 3.1 Wall Layers

每面墙的 appearance layers 为：

```text
Wall geometry
→ Base Surface: Paint OR Wallpaper
→ Optional Wainscoting
→ Window / Wall Decor
```

- Paint 与 Wallpaper 二选一作为 `Base Surface`。
- Wainscoting 是独立可选覆盖层。
- 更换 Base Surface 不移除 Wainscoting、Window 或 Wall Decor。
- 更换或关闭 Wainscoting 不改变 Base Surface。
- Wall Surface appearance 不改变 wall geometry、Collider、Wall Slot occupancy 或 Navigation。

### 3.2 Wall selection

- 玩家点击一面墙时，更换的是该整面 wall segment，不按 Wall Slot 单格涂墙。
- `1 m` texture tile 是 authoring / repeat 单位，不是玩家的 Wall selection 单位。
- 玩家可以分别修改 Back-left 与 Back-right Wall。
- 进入 Wall Tab 后先在 Scene 点击目标墙；目标墙显示 selection highlight，Catalogue 的绿色 Using check 反映当前目标墙的 confirmed styles。
- 只选中目标、尚未产生修改时，可以直接点击另一面墙切换 target。
- 当前墙已有未确认修改时锁定 target；点击另一面墙或切换 Mode 必须先 Confirm 或 Cancel，不能静默切换或丢弃 Preview。
- Wall Mode 不提供 `Apply All`；一次 Wall transaction 只修改当前明确选中的一面 wall segment。

### 3.3 Wall Preview transaction

- `BeginWall(surfaceId)` 一次捕获该墙完整的 confirmed snapshot：`Base Surface` 与 `Optional Wainscoting`。
- 同一 transaction 内可以反复更换 Paint、Wallpaper 与 Wainscoting；不得为每个 layer 建立互相独立的 Preview transaction。
- Paint 与 Wallpaper 写入同一个 Base slot，因此始终互斥；Wainscoting 写入独立 slot，并支持 `None / No Wainscoting`。
- `HasChanges` 比较完整 Preview state 与 snapshot。玩家选回原始组合后，`Confirm` 必须重新 disabled。
- Catalogue 中绿色勾始终表示 confirmed Current；彩色 outline 表示 transaction 的 Preview。Confirm 后 Current 才更新。
- `Confirm` 原子提交当前墙完整组合；任一 layer 失败时不得产生 partial commit。
- `Cancel` 使用 snapshot 一次恢复 Base 与 Wainscoting，并清理 target highlight、Preview outline 与 footer state。
- 找不到 Surface target 或 Style 时拒绝该次选择，不改变当前 Preview；Confirm 失败时保留 Preview，允许重试或 Cancel。

## 4. Floor customization

### 4.1 Initial assets

首批提供三种 Floor Surface：

1. 暖色木地板
2. 浅色方砖
3. 深色石砖

首批素材全部直接可用，不收费、不扣货币、不设置解锁条件。

### 4.2 Application range

- 默认可对当前连续 Floor Region / 当前 Room 整体更换。
- 保留 `Single Grid` 模式，让玩家逐格搭配不同 Surface。
- Floor Tab 在 Mode Tabs 下方固定显示 `Whole Room | Single Grid` 双选 control。
- 每次新进入 Decoration Mode 默认 `Whole Room`；同一次 Decoration Mode 内切换其他 Tab 再返回 Floor 时记住当前选择。
- 有 active Floor Preview 时切换 Whole Room / Single Grid，必须先 Confirm 或 Cancel；一个 transaction 只属于一种 application range。
- `Apply All` 把当前 Surface 与当前 rotation 应用到当前 Room 的全部 Floor Grids。
- Single Grid 使用逐格 tap，不支持 drag-to-paint，避免与 Camera drag 冲突。
- Single Grid 第一次先选择目标 Grid，再选择 Surface；之后该 Surface 保持为当前铺设素材，玩家可继续逐格 tap，把同一 Surface 加入当前 Preview。
- 一次 Floor Preview 可以连续修改多格，最后统一 Confirm。
- Cancel 撤销本次全部未确认的 Floor changes。
- `Undo Last` 撤销最近一次 Surface change 或 rotation；`Apply All` 作为一个 undo step。
- Single Grid transaction 明确维护 `SelectedGrid`、`ArmedStyle` 与 `PreviewedGrids`：
  - `SelectedGrid` 在 Scene 显示 highlight；
  - 选择 Surface 后，它与当前 rotation 成为 `ArmedStyle`；
  - 继续点击其他 Grids 会把同一 ArmedStyle 加入当前 Preview；选择新 Surface 后再继续 tap 可组成混合花纹；
  - 所有 `PreviewedGrids` 在地块边缘保留小型绿色勾，直到 Confirm 或 Cancel；
  - Scene highlight 与绿色勾必须从 transaction state 派生，transaction 结束后统一清理，不单独持久化。

### 4.3 Rotation

- 每个 Floor Grid 的 Surface 可按 `90°` 增量旋转。
- Single Grid 连续铺设会保持当前 Surface 与 rotation，直到玩家主动更换；后续 Rotate 只改变之后铺设或当前重新选中的 Grid，不追溯修改其他已加入 Preview 的 Grids。
- Rotation 只改变该 Grid 的 texture orientation。
- Rotation 不改变 Grid position、occupancy、Collider 或 Navigation。
- 相同 Surface、相同 rotation 拼接时必须 seamless repeat。
- 玩家主动使用不同 rotation 时，允许形成刻意的方向分界。

### 4.4 Floor card state

- `Whole Room` 不显示绿色 Using check；选择 Surface 后只显示 Preview outline。
- 只有 `Single Grid` 已明确选中一个 Grid 时，Catalogue 才在该 Grid 当前正式使用的 Surface 上显示绿色 Using check。
- 绿色 check 的统一含义是：当前明确选中的 edit target 正式使用该素材。
- Single Grid 的 Scene 绿色勾与 Catalogue `Using` check 含义不同：Scene 勾表示该 Grid 已加入本次未确认 Preview，Catalogue 勾仍表示当前明确 target 的 confirmed Surface。
- Preview outline、Scene highlight 与 Scene check 都必须随 transaction 生命周期更新；Undo Last、Cancel、Confirm 或退出 Preview 后不得残留 stale feedback。

## 5. Texture authoring contract

完整 authoring 规则由 `Docs/Phase7_Wall_Surface_Texture_Authoring_Guide.md` 维护。本 Phase spec 依赖以下核心合同：

- Wallpaper、Wainscoting 和 Floor Surface 每张 texture 的 World 宽度对应 `1 Grid = 1 m`。
- 一个 texture tile 内可以包含任意花纹或多个 pattern units。
- 相同 texture tiles 拼接后必须 seamless repeat，不能出现异常宽边或明显接缝。
- Wallpaper 映射完整墙高，默认只横向 repeat。
- Wainscoting 从地面到统一角色腰部高度，腰线只能在顶部、地线只能在底部，且整体不纵向 repeat。
- Floor Surface 对应 `1 m × 1 m`，四条边必须支持二维 repeat。
- Surface texture 不创建额外 geometry、Collider、occupancy 或 Navigation obstacle。
- Wainscoting 的立体感只来自贴图、normal / bump 与墙面 layer rendering，不创建独立围栏式 geometry。
- Wainscoting normal 不得加入与 authored 板缝无关的 diagonal / crosshatch procedural pattern；bump 必须保持轻微，且 Wainscoting 不得单独投出围栏式阴影。
- Wall body 保持正常 architectural depth 与合理 shadow response；Wainscoting 仍固定在墙体下半部，不得漂浮到墙体上方。

## 6. Wall Decor 与 Window

### 6.1 Wall Slot contract

- 每面初始墙使用 `8 columns × 2 rows` 的 Wall Slot Grid。
- 每个 Slot 为 `1 m × 1 m`。
- Wall-mounted object 使用 author-defined integer `Footprint Width × Footprint Height`。
- 通用系统支持 `1 × 1`、`2 × 1`、`1 × 2`、`2 × 2`、`3 × 2` 等尺寸，只要不超过宿主墙边界。
- 初始墙垂直方向最多 2 Slots；object 不得跨墙角。
- Window 与 Wall Decor 共用 occupancy；同一 Slot 不能重叠。
- Wall-mounted objects 暂不提供自由旋转。

### 6.2 Initial production models

Studio Owner 制作：

- 1 个 `1 × 1` Wall Decor；
- 1 个 `2 × 1` Wall Decor；
- 1 个 `1 × 2` Wall Decor；
- 1 个正式 Window model。

自动化 fixture 负责覆盖 `2 × 2` 与更大尺寸，不要求为每个测试尺寸创建正式 model。

`Wall Decor` Tab 的 Catalogue 分为两行：

1. `Wall Decor`：首批 3 个墙饰；
2. `Windows`：首批正式 Window。

两行使用与 Furniture 相同的大 thumbnail + 最小 Item name 区，不显示 Footprint size 或数量，并可独立 horizontal scroll。

### 6.3 Visual bounds 与 depth

- Wall Decor 的正面宽度和高度主体必须留在声明的 Wall Footprint 内；允许很小的边框视觉突出。
- 普通 Wall Decor 可沿墙面法线向房间突出，最大 visual depth 约 `0.35 m`。
- 该 depth 不占 Floor Grid，也不阻挡 Navigation。
- selection Collider 可匹配可见模型，但不得成为角色行走障碍。
- 超过约 `0.35 m`、需要角色避让或功能交互的物件不属于普通 Wall Decor；未来作为特殊 functional wall furniture 设计。

### 6.4 Window rules

- Window 是 wall-mounted object，不切割 Wall geometry。
- Window 可以 move、add、Store，并与 Wall Decor 使用相同 overlap / bounds rules。
- Window 不设独立数量上限；Wall Slots 和 occupancy 自然限制数量。
- Window 可位于兼容的上层或下层 Slot。

### 6.5 Store

- Wall Decor 与 Window 都使用 `Store`，不使用永久 Delete 文案。
- Store 需要明确确认，成功后移除 instance 并释放 Wall Slots。
- 对应 Catalogue item 之后仍可再次无限放置。
- Phase 7 不处理售价、退款或有限库存。

### 6.6 Wall Decor interaction parity

- Wall Decor / Window 复用 Phase 6 Furniture 的 Catalogue → direct Preview → drag → Confirm / Cancel flow，不要求先点击目标 Wall Slot。
- 点击 Catalogue card 后，Preview 出现在 Camera viewport 中央附近最近的 deterministic Wall Slot；Catalogue 自动收起为 compact actions。
- Wall Decor / Window 与 Furniture flow 的主要差异是吸附 Wall Slot Grid，且不提供 Rotate。
- 编辑已有 Wall Decor / Window 时提供 Store、Cancel 与 Confirm；新 Preview 不显示 Store。
- 新物品 Confirm 后 Catalogue 保持 `Compact Preview`，刚提交的物品必须在下一次 tap 立即可选；不要求先切换 Mode 或点击其他区域刷新。
- Preview 可以从一面墙直接拖到另一面墙；pointer 经过墙角或无有效 Slot 区域时显示 Invalid。
- 最终 Confirm 时 object 必须完整位于同一面墙，不能横跨墙角。
- Preview ghost 的姿态由 confirmed target wall 的 local axes 决定：它必须垂直于地面、正面平行墙面，并沿墙面法线向房间轻微悬浮；不得平躺或吸附在 Floor plane。
- ghost 使用对应 Catalogue entry 的真实 prefab renderer；五个首批 Wall Decor / Window prefabs 都必须验证 visible renderer bounds、墙面朝向与 footprint 对齐。

## 7. Placement feedback

- Wall Decor / Window Preview 投影显示在目标墙面上。
- Valid 使用绿色 Wall Footprint projection 与中央绿色 `✓`；Confirm 可用。
- Invalid 使用红色 Wall Footprint projection 与中央红色 `×`；Confirm disabled。
- Footprint projection 始终位于当前可见 Wall/Wainscoting/rail/baseboard 的最外侧，仅 projection 前移；真实 prefab ghost 与 confirmed item 仍贴紧 Base Wall Surface。这样绿色/红色不因被墙饰遮挡而忽深忽浅，也不会让模型整体悬浮。
- Surface footer / Wall Decor actions 区域上方显示具体 Invalid reason，例如 `Overlaps another wall item`、`Outside wall bounds` 或 `Cannot cross wall corner`。
- Valid / Invalid 同时使用颜色、icon shape 与文字 reason，不只依赖色相。
- Surface Preview 只改变 appearance，不影响 attachment、occupancy 或 Navigation。
- Camera position 与 angle 保持不变，不切换正面墙视图。
- 当前目标墙保持正常显示并高亮；挡在 Camera 与目标墙真实平面之间的 Furniture / Wall Decor 临时淡化。距离上限必须取 rotated Wall 的真实 surface plane，不使用 world-axis-aligned Renderer AABB 的近侧面。
- 进入 Floor Mode 后，无需先选择地砖或建立 Preview，所有已 Confirm 的 Furniture 都临时淡化到约 `35%`，让 Floor 与 selected Grid 保持清楚；Wall、Wall Decor 与 Window 不跟随这条 Floor rule 淡化。
- 淡化不改变 object data、occupancy、Collider ownership 或 Mode selection boundary；切换 target、离开对应 Mode、Cancel、Discard、disable 或 fault cleanup 后必须精确恢复原 Materials 与 MaterialPropertyBlocks。Continue Editing 保持当前 Mode，因此对应淡化继续存在。
- Fade opacity 是 provisional tuning parameter，必须在 Unity Play Mode 使用正式 Scene 内容测试后由 Studio Owner 决定。

## 8. Stable data boundaries

- Wall Surface、Floor Surface 与 wall-mounted object attachment 使用 stable Surface / Slot identity。
- Scene rebuild 后 identity 必须稳定，为未来 Phase 17 Save / Load 保留合同。
- Phase 7 只建立可保存的数据边界，不提前实现完整 Save / Load UI。
- Confirm 才提交正式 Layout；Preview、Undo 与 Cancel 都不能静默污染正式数据。

## 9. Current asset set

当前 Phase 7 production set 已准备：

- Paint：Cream、Sage、Terracotta；
- Wallpaper：Cream Floral、Sage Sprig；
- Wainscoting：Warm White + Rail、Sage Plain；
- Floor：Warm Wood、Light Tile、Dark Stone；
- 3 个 Wall Decor：`1 × 2` Painting、`2 × 1` Wood Shelf、`1 × 1` Monitor；
- 2 个 Window Catalogue entries：`1 × 1` 与 `1 × 2`；
- Catalogue thumbnails / Surface swatches、Wall Valid / Invalid projection materials 与 Wall Slot edit display；
- Wallpaper / Wainscoting texture authoring guide。

正式素材仍需通过 import、prefab bounds、thumbnail 与 Unity Play Mode 视觉验收；“文件存在”不等于 Studio Owner 已完成视觉 acceptance。

## 10. Provisional tuning 与 derived artifacts

以下项目不再需要 pre-implementation product decision，但必须在 implementation / verification 中完成：

- Bottom Sheet exact heights、drag thresholds 与 card size 依据多分辨率测试调整，但不得改变已确认的三种 snap states、`0.16s` transition、gapless folder tabs 或 fixed Surface footer。
- Camera fade opacity 依据 MainCafe 正式内容的 Unity Play Mode review 调整，由 Studio Owner 做最终视觉验收。
- Exact test matrix、implementation tasks 与 manual acceptance checklist 从本 spec 派生，不引入新的 player-facing rules。
- Figma Phase 7 reference board `38:3` 已由 Studio Owner 确认整体方向；Unity implementation 仍需另行做 in-game manual acceptance。

本轮 implementation 必须重新检查以下 regression scope；失败项属于本轮修复，不得标记为既有问题后跳过：

1. Bottom Sheet 圆角、暖色面板、inner margin、紧密 cards 与标题不重叠；
2. Folder Tabs active layering、与 Sheet 无 gap、跟随 `0.16s` Expanded / Compact transition；
3. Wainscoting 位于墙体下半部、无 crosshatch / fence-like shadow，Wall 保持可读 architectural depth；
4. Catalogue Current check 与 Preview outline 的完整生命周期；
5. Wall Decor 真实 prefab ghost、墙面 footprint、drag 与 valid / invalid；
6. Wall Decor / Window deterministic in-game thumbnails；
7. Scene 初始不显示临时预放 Window，但 Catalogue 保留两个 Window entries；
8. Confirm 后 Window 只在当前 runtime session 存在，reload 后清除。

## 11. Approval gate

本文件原始设计与 2026-08-27 Surface transaction amendment 均已由 Studio Owner 批准。下一步顺序：

1. Studio Owner review 本轮写入的 spec 内容；
2. 使用 `writing-plans` 修订现有 implementation plan 与 test matrix；
3. Studio Owner 确认 implementation 可以开始；
4. 按 TDD 先观察 focused RED，再进行 minimal implementation 与 regression；
5. 完成 automated verification 后，由 Studio Owner 在 Unity Play Mode 验收交互与视觉 tuning。
