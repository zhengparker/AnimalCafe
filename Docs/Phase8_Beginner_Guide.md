# AnimalCafe Phase 8 Beginner Guide — Completion & Review Record

> 当前状态（2026-09-21）：**Completed — merged to main**。Studio Owner 确认朋友 review 完成，授权合并 PR #7、完成 Phase 8、本地同步及归档后清理。PR 已合入 `main`（`8ee0026247c2c3200bd1bc9ac471b7007e4a4f32`）；合并后回归及归档清理均完成，最终结果见第 37 节。以下带日期的 manual Pending / 未 commit / 未 merge 仅表示当时状态，不覆盖最新收尾记录。

> 历史工作（2026-09-13）：Owner 已批准手机 UI 调整，当时正在实现与验证，详见第 27 节。当时尚未 Ready for manual review；下方第 26 节及更早数字仅代表历史验证。

> 历史状态（2026-09-13，上一轮）：五项 UI polish 的当轮记录见第 26 节。EditMode **683/683**、PlayMode **585/585**、原生截图流程 **15/15** PASS，四种尺寸共 **80 张**截图已复核。这些结果不代表第 27 节正在进行的手机调整；Owner 与手机真机验收仍 **Pending**。

> 历史状态（2026-09-12）：参考图排版两步已完成，停在 **Ready for manual review**。家具／墙饰浮动按钮收紧间距并关闭 tooltip；左上 HUD、右上装修入口和底部目录／固定 Pick-up 已按参考图调整。最终定向 EditMode **57/57**、PlayMode **210/210**、独立完整 Editor PlayMode **849/849**、截图流程 **13/13**，均 0 failed／skipped／inconclusive；34 张截图通过独立技术复核。完整清单及一次性 review 入口见 **第 15 节**。Owner／原生 Overlay／真机验收仍 Pending；历史截图误覆盖仍见 **15.6**。保留现有素材、模型、footprint 和 gameplay；没有 commit／push／merge。

> 历史状态（2026-09-09）：Owner 批准进一步调整为红绿灯式 footprint，最新范围、截图与复测见 **8.7**。本轮 focused EditMode **15/15 PASS**、直接 PlayMode **84/84 PASS**，4 组截图完成独立技术复核，交回 **Ready for manual review**。8.5 的 813/235、8.6 的 10/84 为历史证据，不代表本轮完整 Phase 验证。新增 Owner manual 仍 **Pending**；icon、模型、Theme/UI 与 Exit 保留。历史 M1–M17 Owner PASS、M18 technical PASS 保留，M6 incompatible Slot 子项仍未覆盖；不自动关闭 Phase 8。

## 1. 这次要检查什么

Phase 8 只负责三件事：把功能设备放到合适位置、自动算出 Employee / Customer anchors、判断布局是否具备未来营业条件。它不包含 cafe day loop、NPC movement、Order、Queue、NavMesh / pathfinding agents、economy 或 Save；readiness 满足营业条件只表示布局准备好了，并不表示正式经营已经实现。

你只需要判断玩家实际看到和操作到的内容是否清楚、稳定、符合预期；**不要在本轮自己修改 Scene、Prefab、asset 或 code**。

## 2. 玩家视角：改动前与改动后

- 改动前：Decoration Mode 只能处理普通 furniture，系统不知道哪台设备负责收银、做咖啡或取餐，也不能判断布局是否适合未来营业。
- 改动后：Furniture Tab 多了 Cash Register、Coffee Machine 两行和 Pick-up Point button。玩家仍用熟悉的 Preview、Move、Confirm、Cancel、Store 操作；系统自动放置 Employee / Customer anchors，并在 Confirm 或 Store 后更新 readiness，有问题时显示提示。
- 例子：把一台收银机、一台咖啡机和一个取餐点放到合适 Counter Slots，并保持顾客与员工路线连通。确认后若没有 blocking 或 warning，顶部 readiness 提示会隐藏；有问题时显示短状态，可展开处理清单。

## 3. 重要文件与关键概念

| 文件 | 用途 |
|---|---|
| `Assets/Scripts/Layout/FunctionalSurfaceLayout.cs` | 保存已确认的桌面设备、Pick-up Points 和 Slot occupancy。 |
| `Assets/Scripts/Layout/InteractionAnchorResolver.cs` | 自动计算 Employee / Customer anchors；玩家不手动设置。 |
| `Assets/Scripts/Layout/GridReachabilityEvaluator.cs` | 分别检查顾客与员工 Grid 连通性，不负责 NPC 移动。 |
| `Assets/Scripts/Layout/LayoutReadinessEvaluator.cs` | 汇总设备、anchors 和两套网络，生成 ready / blocking / warning 结果。 |
| `Assets/Scripts/Decoration/SurfaceMountedPreviewView.cs`、`PickUpPointIndicatorView.cs` | 显示设备 Preview、Pick-up indicator 与随支撑家具移动的表现。 |
| `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` | 完整 automated/manual cases 与 18 项人工结果 ledger。 |
| 本 guide 第 37 节 | 最新合并、回归、Owner 收尾决定与清理记录；第 6–36 节保留各轮历史证据。 |

关键概念：`Surface Slot` 是 Counter 上可放功能设备的位置；`anchor` 是未来 Employee 或 Customer 互动时应站的 Grid cell；`Preview` 只是临时状态，只有 Confirm / Store 成功后才改变正式 layout 和 readiness。顾客与员工使用两套独立连通网络；Pick-up 的两个角色 anchor 必要时可以共用一个 cell。

## 4. 开始前

1. 在 Unity Hub 打开已同步的主项目：`E:\Unity\Project\AnimalCafe`，branch 为 `main`；旧 Phase 8 worktree 仅为历史开发路径，不再作为试玩入口。
2. 使用 Unity `6000.5.5f1`。
3. 打开 `Assets/Scenes/MainCafe.unity` 并进入 Play Mode；在 Game view 右侧点击 `Decoration` button 进入 Decoration Mode。Catalogue 默认打开 `Furniture` tab；如果当前是其他 tab，点击 `Furniture`。
4. P8-M-018 使用 validation Scene：`Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`。
5. 如需再次人工复测，在 `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` 的“Manual Execution Ledger”追加本次日期与结果，不覆盖历史验收记录。
6. 不要运行 `Build Assets` 或任何 `Configure ...` menu；它们会修改 assets / Scenes，不属于本次 manual review。
7. readiness 提示位于 Game view 顶部。初始化时显示当前布局的问题，Confirm / Store 成功后刷新；无 blocking 或 warning 时隐藏。Preview / Cancel 不改写已确认报告，展开详情时可显示待确认备注。


判定方法很简单：预期现象全部出现、没有新的 Console error/exception，记 `PASS`；任何步骤无法完成、结果与预期不同、或 Console 有新的 error/exception，记 `FAIL`。`FAIL` 时记录 case ID、操作步骤、Scene 状态、Console message，并附截图或短视频；**停止修复，交回团队处理**。

## 5. 手动验收清单（早期验收基线）

本节保留早期 manual cases 的操作与预期，便于对照历史 ledger；不是当前版本逐字、逐像素的 UI 规范，也不表示需要重新执行已完成的验收。后续 P8R 已调整 Pick-up 图标、CR 方向提示、Catalogue 名称和 readiness 短提示等表现；实际变更按下方带日期的对应记录核对，当前完成状态以第 37 节为准。当前 readiness 的快速说明见上方第 2、4 节。

### A. Catalogue 与设备放置（P8-M-001–005）

| Case | 操作 | PASS 判断 |
|---|---|---|
| P8-M-001 | 进入 Decoration Mode，打开 Furniture Tab；查看三行，鼠标分别放在物品行与 Catalogue 外层转滚轮，再按住拖动横向 / 纵向浏览。 | 滚轮不移动 Catalogue 内容，镜头仍可缩放；拖动浏览正常，三行清楚且无重叠、截断。 |
| P8-M-002 | 选 Cash Register，移到空闲 Counter Slot，观察悬浮并 Rotate 后 `✓`；再 Move → Cancel → Move → Confirm。 | 未确认 ghost 保持模型原色并悬浮于桌面，红绿 footprint 留在桌面；确认后设备落回 Slot；Cancel 回原位；不需设置 anchor；Console clean。本轮原色复测见 8.8，仍 Pending。 |
| P8-M-003 | 对 Coffee Machine 重复主要放置、Move、Cancel、Confirm 流程。 | 只使用桌面 Slot；actions 一致；不出现 Customer anchor 设置。 |
| P8-M-004 | 把 CR/CM 移到 Floor，松手再抓 ghost 拖回桌面；也尝试已占用 Slot 和 `✓`。 | 地面 ghost 保持模型原色、悬浮可见，仅 footprint 红色；invalid 时 Confirm 不可用；回兼容空闲 Slot 可确认；未确认不改变原设备与 occupancy。 |
| P8-M-005 | 在不同 Slots 各放至少两个 CR 和两个 CM，再分别选择、移动、旋转、Store。 | 各实例独立；操作一个不会影响另一个。 |

### B. Pick-up Point 与支撑家具（P8-M-006–011）

| Case | 操作 | PASS 判断 |
|---|---|---|
| P8-M-006 | 点击 Pick-up Point button，把 indicator 移到空闲 / 已占用桌面及地面，松手后重新抓起并拖回桌面。 | 细长灰白立体倒四方棱锥与 `1 × 1` footprint 清楚分离；只有 footprint 变绿 / 红；地面仍悬浮可见且不能 Confirm，回空闲桌面可确认。 |
| P8-M-007 | Create + Confirm 一个 Pick-up；Move → Cancel；Move → Confirm；Store。 | 只有 Store、`✓`、`×` 等适用 actions；没有 Rotate；每步符合现有 furniture interaction。 |
| P8-M-008 | 连续点击 Pick-up Point button，创建至少两个 points。 | 第二次创建新实例；不覆盖第一个；不允许与 point、CR 或 CM 重叠。 |
| P8-M-009 | 创建 points 后退出 Decoration Mode，再重新进入。 | 退出时 arrows/footprints 不可见；重新进入时 confirmed indicators 在正确 Slots。 |
| P8-M-010 | 移动、旋转一个带 CR/CM/Pick-up 的 Counter。 | 桌面内容随 Counter 移动/旋转；无掉落、漂移、重复或旧位置残留；readiness 更新。 |
| P8-M-011 | 对有桌面内容的 Counter 尝试 Store。 | Store 被阻止；提示列出需要先移除的内容；Counter 与内容均保留。 |

M6 执行限制（2026-09-08）：当前 MainCafe 没有可供测试的“类型不兼容 Slot”，该子项仍记“未覆盖”。Studio Owner 已批准地面也保留悬浮模型并显示红色 footprint 的修复，拖离 Slot 后隐藏不再是接受标准。M10 可使用三张 `Counter Module` 分别带 CR、CM、Pick-up；每张 Module 只有一个 `slot.0`。

### C. Layout Readiness（P8-M-012–015）

#### P8-M-012：做出一组可营业布局

从刚进入 Play Mode 的 MainCafe 开始，不移动原来那张 Counter。先做下面这个最小例子：

1. Furniture 行选择 `Counter Module`，新增两张 Counter，和原 Counter 排成同一排；相邻两张之间留一个完整空格，全部保持初始方向、不 Rotate。Grid 坐标如下。
2. 在第一张的桌面放 `Cash Register` → `✓`。点击底部 `Catalogue` handle 重新展开，再在第二张放 `Coffee Machine` → `✓`。
3. 再展开 Catalogue，点击 `Pick-up Point`，放到第三张桌面 → `✓`。每张 Counter 的唯一 Surface Slot 都是 `slot.0`。
4. 保持三张 Counter 前后两排为空，不在入口的蓝色区域放家具。

| 桌面内容 | Counter 的 Grid cell | Employee anchor | Customer anchor |
|---|---|---|---|
| Cash Register，使用原 Counter | `(2,3)` | `(2,4)` | `(2,2)` |
| Coffee Machine，新增 Counter | `(4,3)` | `(4,4)` | 无 |
| Pick-up Point，新增 Counter | `(6,3)` | `(6,4)` | `(6,2)` |

坐标供核对数据；正常 MainCafe 不会画出 anchors。可以对照[自动验证截图](../outputs/p8r-fix-ready-layout.png)摆放。该例已通过真实 MainCafe 的 Catalogue / Confirm 自动化流程，但截图和自动化不代替本项人工验收。

PASS：显示精确文本 `布局已准备好，可以营业`。

#### 本轮修复的回归观察点

这些仍属于原有的 18 项 manual cases，不预先标成 PASS：

- P8-M-002 / 003 / 007：Store CR、CM、Pick-up 时先出现确认框；Cancel 保留原物件和 Preview，Confirm 才移除。确认框打开时不能产生第二个 Preview。
- P8-M-004 / 016：同一分类内点击其他家具仍需先 `✓` 或 `×`；2026-09-12 获批的新规则允许直接切换到另一个 Tab，先取消未确认 Preview，再进入目标分类，已确认布局不变。重复点击当前 Tab 保留 Preview；详见第 26 节。
- P8-M-012 / 017：每次 Confirm 后，通过实际 Catalogue handle 展开，继续创建下一件设备及 Pick-up；不能出现 button 没反应。
- P8-M-014：每一条原因应对应自己的设备、中文角色与站位坐标；玩家文本不显示 GUID / Slot ID。长消息可以在顶部面板内滚动，不能越出屏幕；清空后的面板不应挡住场景操作。原始 IDs 只留在 diagnostics。
- P8-M-004 / 017：Pick-up 周围没有可用站位时，action bar 应明确提示 `Pick-up needs a free adjacent cell`，即使顶部还保留上一条正式 readiness。
- P8-M-016 / 017：退回场景、反复进入/退出、关闭 Scene 时，Console 不应出现新的 exception。Pinch 的自动化兼容已覆盖，真实手机验收仍不计入本轮 Windows manual PASS。

#### P8-M-013：每次只缺一种必要功能

从 P8-M-012 的 valid 状态开始：

1. Store 所有 CR，确认出现 `暂时不能营业：还需要至少一个收银机`；再放回并 Confirm 一个有效 CR，恢复 ready。
2. Store 所有 CM，确认出现 `暂时不能营业：还需要至少一台咖啡机`；再放回并 Confirm 一台有效 CM，恢复 ready。
3. Store 所有 Pick-up，确认出现 `暂时不能营业：还需要至少一个取餐点`；再放回并 Confirm 一个有效 Pick-up。

PASS：三种 type-specific message 都正确，而且每次恢复当前类型后才测下一种。

#### P8-M-014：额外设备局部失败

1. 保留 P8-M-012 的有效三件组合。
2. 在另一张 Counter 上新增并 Confirm 一个额外 CR 或 CM。
3. 在只挡住这个额外设备某个 anchor、不会挡住原三件组合的位置，放置并 Confirm 一件 Floor furniture。
4. Confirm 后查看顶部 `可以营业，但...` warning，核对设备类型、中文角色、站位坐标与阻挡原因。

PASS：顶部文本以 `可以营业，但` 开头，说明例如 `互动位置被阻挡` 的原因，并用中文角色与站位坐标区分受影响位置；不显示长 GUID、`slot.0` 或英文 Employee / Customer。原有效组合仍可用。内部 stable IDs 保留用于开发诊断，不属于玩家可见验收要求。

固定摆法（按当前源码静态核对，尚未在 Unity 实测这组坐标）：保留 M12，额外 Counter + CM 放在 `(6,6)`，都不 Rotate；再把空 `Counter Module` 放到 `(6,7)` 并 Confirm。预期仅额外 CM 的员工站位被挡，显示可营业 warning。完成后先 Store 额外 CM，再 Store `(6,6)` 支撑与 `(6,7)` 阻挡桌子，恢复 M12，避免影响 M15。

#### P8-M-015：每件都 valid，但凑不成完整动线

1. 保留 P8-M-012 的有效三件组合。
2. 用已 Confirm 的 Counter / Floor furniture 横向排成分隔带；让 CR 与 Pick-up 的 Customer anchors 朝 Entrance 一侧，让三个设备的 Employee anchors 在另一侧。不要直接占住任何 anchor cell。
3. 在 employee 一侧，从分隔带向后墙再排一列已 Confirm 的家具，放在 CM 或 CR 的 employee anchor 与 Pick-up employee anchor 之间，把 employee 侧分成左右两个可走小区域。
4. Confirm 最后一件家具；确认每个设备没有单独出现 `互动位置被阻挡` 或 `互动位置无法到达`。

PASS：显示 `暂时不能营业：还没有完整且可到达的营业动线`。

固定摆法（按当前源码静态核对，尚未在 Unity 实测这组坐标）：恢复 M12，使用九张空 `Counter Module`。先填入 `(0,3)、(1,3)、(3,3)、(5,3)、(7,3)`，与原三张桌子组成横跨 8 格的完整一排；再填入 `(3,4)、(3,5)、(3,6)、(3,7)`，从 CR 与 CM 之间那张桌子向员工一侧延伸至边界。逐张 Confirm，不移动原设备，也不占 `(2,4)、(4,4)、(6,4)、(2,2)、(6,2)`。这让 CR 员工站位与 CM / Pick-up 员工站位分属两块区域，而客人站位仍通向入口。

如果某个设备先变成 individual invalid：撤销/Store 刚加的分隔，恢复 P8-M-012，再把分隔线向外移一格重试。

### D. 回归、稳定性与 debug（P8-M-016–018）

2026-09-08 Owner 决定：M16、M17 人工 PASS；Validation Scene 过于杂乱、不便人工核验，M18 改由 Codex 执行同等要求的 real Scene 技术检查并记录独立证据。以下 M18 手工清单保留作对照，不再要求 Owner 重做；不整理或保存 Scene、不改游戏功能。

| Case | 操作 | PASS 判断 |
|---|---|---|
| P8-M-016 | 完成一次 Floor furniture、Wall Decoration、Floor style、Wall style 的 Preview、Confirm、Cancel。 | 现有操作、Camera、Pause、UI、Store 没有明显退化。 |
| P8-M-017 | 同一 Play Mode 内多次创建、移动、旋转、Cancel、Store，并多次进出 Decoration Mode。 | 没有新的 Console error/exception/持续 warning；没有 duplicate views 或残留 Preview。 |
| P8-M-018 | 直接打开已经 debug-enabled 的 `Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`；不要运行 Configure。按下面的精确清单检查。 | marker 与 resolver 一致；Rotate / Move 后没有 stale 或 duplicate marker；普通 MainCafe 的 debug visuals 为 off。 |

P8-M-018 精确清单：

先退出原来的 Play Mode，再打开 validation Scene，重新进入 Play Mode 和 Decoration Mode。在这个 Scene 内重新按 P8-M-012 的三 Counter 示例放置并 Confirm CR、CM 和 Pick-up，再执行下面的检查。fresh Scene 只有初始 Counter；上一场 Play Mode 的 runtime 布局不会保留，不要在设备还未创建时寻找 markers。

1. 在 `InteractionAnchorDebugRoot` 下找到 `AnchorDebug_<Role>_<instanceId>`。
2. Employee markers 是蓝色；Customer markers 是橙色。
3. Cash Register 有一枚 Employee 和一枚 Customer marker，位于相反两侧；Coffee Machine 只有 Employee marker。
4. Pick-up 的 Employee / Customer anchors 自动选择、可以共用一个 cell，而且 action bar 没有 Rotate。
5. Rotate CR / CM，或 Move / Rotate supporting Counter 后点击 Confirm；不要 Rotate Pick-up。
6. 每次 Confirm 后等待下一帧，再确认 markers 跟随 resolver 的新结果更新；Preview / Cancel 保留已确认布局的 markers。旧位置没有 stale marker，同一角色/instance 没有 duplicate marker。
7. 回到 `Assets/Scenes/MainCafe.unity` 的 Play Mode，确认没有 `AnchorDebug_...` visuals。

## 6. 交回结果

### 2026-09-09 Phase 1–8 bug review 修复（UX 实施前快照；最新结果见 8.4）

本小节记录先前五项 bug 修复：修改位于 `codex/phase-8-functional-furniture`，基于 `4c878bc`；当时没有 commit / push / merge，也没有修改 Scene、Prefab、材质、字体或 ProjectSettings。之后批准的三项 UX 修改包含 P8 字体更新，最新范围与结果见 8.4。下面的 PASS 是当时的 automated technical evidence，不是新增 Owner manual PASS。

| 修复 / regression case | 最新结果 |
|---|---|
| Floor 单格未选新 style 时直接 Rotate，真实 Confirm 立即启用 | 已完成 / PASS |
| 重新编辑 90° / 180° / 270° 的 tile，首次 Rotate 从原角度继续；Undo / Cancel 不改 confirmed layout | 已完成 / PASS |
| Catalogue 鼠标及 Touch 斜向拖动只移动选定轴；Hide、Collapse、disable、CompactPreview、TabsOnly、Hidden、Sheet drag 都释放 owner / 横轴锁 | 已完成 / PASS |
| Pinch 按实际距离连续缩放，相同总位移拆成不同帧得到相同结果；小位移、零值、finite guards、上下限与旧 mouse wheel 保留 | 已完成 / PASS |
| BuildAssets 在任何副作用前拒绝 loaded dirty project assets，报告路径；无全局 SaveAssets，不保存 unrelated Material，内存修改与磁盘 bytes / meta 保留 | 已完成 / PASS |
| 原 Phase 8 Scene → Phase 6 real Touch 顺序组合，真实 HUD 点击、Scene 切换与 runtime isolation | 已完成 / PASS，原 25/26 现 26/26 |

该阶段 XML 均在 `TestResults/`，不代替 8.4 的 UX 实施后验证：

| Suite | 结果 | 文件 |
|---|---|---|
| 完整 Editor PlayMode，两个 assemblies 同次运行 | **781/781 PASS**：core 679 + real Scene/Input 102 | `fix-20260909-all-play-final.xml` |
| 直接相关 EditMode：surface sessions、P8 builder、Scene setup safety、migration 与 validator | **83/83 PASS** | `fix-20260909-edit-final.xml` |
| 原始 26-case Touch control，无临时诊断 | **26/26 PASS** | `fix-20260909-exact-touch-control-final.xml` |

以上 failed / skipped / inconclusive 均为 0；26-case control 是 PlayMode 子集，不与 781 相加。本轮未重新运行 full EditMode 或 standalone Player，也未执行 Android/iOS 真机验收。9 月 4 日的全量结果仍只是历史 baseline。

TDD 证据：`fix-20260909-floor-builder-red.xml` 为 4 项预期失败；`fix-20260909-floor-drag-red.xml` 为 10/15（5 项预期失败）；`fix-20260909-pinch-red-touch-diagnostic.xml` 为 30/37（6 个 Pinch 失败 + 原 Touch 失败）。独立 review 另找出 Sheet 直接切换状态绕过清理，`fix-20260909-sheet-interrupt-red.xml` 为 3/7，随后补齐最小修复，最终 781 已包含这 4 条新增边界。一次测试 API 名称写错导致 compile failure，修正后重跑；编译失败不算有效 RED。

Touch 根因：前序 plain P8 Scene fixtures 留下真实 InputActionAsset 的 runtime binding cache；下一 InputTestFixture reset 后，旧 cache 未进入新 registry，Point / Click controls 为空，但 raw Touch 已正确消费。现在在前序 Scene unload 后、下次 reset 前，确认 actions disabled 且无存活 UI module 引用，再 Dispose 其 runtime state。真实 serialized asset / bindings、输入时间与原 assertions 不变，临时日志已移除。诊断对照 `fix-20260909-pinch-touch-green.xml` 37/37，HUD 有 Down / Up / Click；最终无诊断的完整 suite 与原 control 再次 PASS。

Pinch 当前沿用已有 40 px 样例幅度：40 px = 一个 `ZoomSpeed`，更小距离按比例缩放；不新增 serialized setting。这个校准解决帧数依赖，不等于不同手机 DPI 下的最终手感验收。

独立 Engineering / QA code review：最终没有未处理的 Critical / Important；不是“证明全游戏绝无 bug”。本轮没有扩展 Slot compatibility domain，M6 incompatible 子项保持未覆盖，不擅自改为 PASS / N/A。

#### 本轮最小人工复测（待 Owner 执行）

无需重做历史 M1–M18；只检查本轮改变的玩家手感：

1. `Floor → Single Grid`：选择已有地砖，不先选 style，点 Rotate；Confirm 应立即可点。Confirm 后重新点该格并 Rotate，应继续转 90°，而不是回到固定角度。再试 Undo / Cancel，原布局应恢复。
2. 打开有多行的 Catalogue：斜向上/下拖只滚分类列表，斜向左/右拖只滚物品行；拖动中收起再打开，应仍可正常横拖。鼠标滚轮继续缩放 Camera，不滚 Catalogue。
3. 用可用 Touch 测试环境做相同距离的快/慢 pinch：缩放应连续，没有每帧跳一整步；最小/最大距离仍有限制。真实 Android/iOS 手感不由 Editor 自动化替代，既定 Phase 51 真机 gate 不在本轮自动提前完成。

BuildAssets 与 fixture cleanup 已由自动化验证；不要求你对真实未保存资产执行破坏性尝试。修复交付停在 targeted manual review；下方历史验收不因这些新改动自动重新标为 PASS。

### M11–M18 最新验收记录（2026-09-08）

- Studio Owner 先明确反馈“11-15也通过了”，随后确认“16 - 17都pass”；M1–M17 人工 PASS。
- Owner 明确授权 M18 改由 Codex 代测，并批准补充测试代码与验收记录。现为 **17 项 Owner manual PASS + 1 项 Codex technical PASS**；不是 18 项用户人工 PASS，也不代表整个 Phase 完成。
- 最终专项证据：`outputs/phase8-m18-delegated-20260908/m18-play-final.xml` **15/15 PASS**，failed / skipped / inconclusive 均为 0；包含 4 个新增 M18 tests 与 11 个直接 regression cases。独立 QA 已读取 XML，Spec / Quality verdict 均为 PASS，无新的阻挡项。
- M18 覆盖：real Validation Scene 内通过 Catalogue / Confirm 创建 CR、CM、Pick-up；核对精确 marker 角色、数量、蓝/橙 palette、Grid cell、世界位置与朝向；CR/CM 四向旋转以及三张支撑桌 Move / Rotate 后 Confirm 更新；Preview / Cancel 保留 confirmed markers；Pick-up 双角色共用 cell 且无 Rotate；从已填充 Validation 切换 MainCafe 后无 debug 对象，包括 inactive 残留。
- 断言可靠性：单个新增 test 包含 7 种 runtime-only 反例（错位置、错颜色、缺失、重复、旧 marker、整个 root 偏移 / 旋转），不计作 7 个独立 tests。`m18-negative.xml` 为故意错位的预期失败；独立 QA 发现旧断言只查局部坐标后，`m18-root-gap-red.xml` 先证明 root 偏移漏检，再补世界坐标检查并通过最终 GREEN。两份 RED 不是正常运行的生产 bug，也不计入通过数。
- 前置证据：`anchors-edit.xml` 17/17、`existing-play.xml` 5/5 PASS，均位于同一 outputs 目录；这些已有 tests 只作相关回归，不单独当作完整 M18 验收，也不与最终 15 项相加冒充去重总数。
- 本轮只修改 `Phase8ValidationScenePlayModeTests.cs` 与四份验收文档；两份 Scene、项目配置、dependencies 及直接相关 runtime 源码与执行前一致，未保存或整理 Scene。旧 Touch 顺序验证问题及 M6 incompatible Slot 未覆盖子项不在此次修复范围，Phase 8 / Task 10 仍 In Progress，不进入 Phase 8R；未 commit / push / merge。

### M6 与玩家提示修复（2026-09-08，历史复测记录；最新进度见上节）

- Studio Owner 在修复后明确反馈“现在好了，继续11-15的步骤”；M6 复测 PASS，当时 manual 为 **10 PASS / 0 FAIL / 8 Pending**。这不代表未覆盖的 incompatible Slot 子项已被测试，也不替代 M14 的独立验收。
- 原 M6 FAIL：倒四方棱锥太宽、绿色模型与 footprint 混在一起、立体感不足；要求细一些、按实体模型表现，落到地面时也显示 red invalid footprint。已按反馈修复并由 Studio Owner 接受。
- 玩家提示：截图中的中文正常，但长 instance / support IDs、`slot.0` 与英文 role 被拼入可见文本。已批准保留内部 diagnostics，玩家只看中文原因与必要的站位定位；M14 已同步取消“可见 stable ID”要求。
- 已实现外观：棱锥宽深从 `.56m` 改为 `.28m`，高 `.50m`，独立灰白 opaque Lit 材质及硬边面；footprint 单独使用 valid / invalid 色。地面保留悬浮模型与红 footprint，可重新抓起拖回桌面，Cancel 恢复来源。
- Store 有内容的 Counter 时保留原有清单能力：玩家看到 `收银机（1），咖啡机（1），取餐点（1）` 等类型与数量，不再显示 ID；精确 blocker IDs 仍在 diagnostics。
- 已按测试先行完成修复与直接回归，包括 M7–M10 对应行为；不新增系统，不自动关闭旧 Touch 顺序问题。自动化通过不代替 M6 视觉复测。

本次修改范围：

- Runtime：`DecorationModeController.cs`、`PickUpPointIndicatorView.cs`、`PlacementFeedbackMapper.cs`、`ValidationMessageView.cs`；只调整表现、提示和既有 ground Preview 路径，不改 layout domain / Router。
- Authoring：`Phase8AssetBuilder.cs` 的局部 Pick-up 更新入口、`Phase8FeedbackAssets.cs` 的专用字库；通过 Unity API 更新 Pick-up prefab 内 mesh、独立 `M_PickUpPoint_Indicator.mat` 与 P8 字体。原 prefab GUID / mesh local ID / footprint 引用保持，原共享 Phase 7 材质和 Phase 5 字体不变。
- Tests：Phase 8 asset / feedback / controller contract、functional interaction / view、feedback lifecycle；现有 MainCafe opt-in 截图 helper 记录真实运行状态，不保存生产 Scene。
- 已更新本 guide、Phase 8 design / test-cases 与 Roadmap；不新增 Phase / system / feature，未 commit / push / merge。

验证记录位于 `outputs/phase8-manual-m6-20260908/`。RED 保留 `feedback-red-edit.xml`、`feedback-view-red-edit.xml`、`assets-red-edit.xml`、`m6-red-core.xml` 与 `blocker-list-red-core-verified.xml`：分别复现可见 IDs / 英文角色、缺中文字形、旧宽度 / 全模型着色、地面隐藏 / 不悬浮，以及收纳清单缺失。最初 `blocker-list-red-core.xml` 是测试 fixture 缺 Prefab，不计有效 RED；已改用完整真实 harness 后重跑。

初次 `m6-green-edit.xml` 为 **31/36，非 GREEN**：首次生成字库后，源 OTF 的会话内 dirty 状态触发 Scene setup 的保护检查。未绕过 dirty guard；fresh Editor 重新运行 `direct-regression-edit.xml` 为 **75/75 PASS**，源字体磁盘 hash 不变。首轮 `direct-regression-core.xml` 为 **373/374，非 GREEN**，随后补齐中文收纳清单；最终 `direct-regression-core-verified.xml` 为 **374/374 PASS**。最终通过 XML 的 failed / skipped / inconclusive 均为 0。

最终真实 Scene / Input：`direct-regression-scene-verified.xml` **15/15 PASS**（MainCafe 6、Validation Scene 2、M1 wheel 7），failed / skipped / inconclusive 均为 0。使用原始 Game view 尺寸的 opt-in capture 已查看：`pickup-table.png`、`pickup-ground.png`、`readable-feedback.png`；灰白立体模型、红 / 绿 footprint 和无原始 IDs 的中文提示可见。截图采用临时 ScreenSpaceCamera，仅作自动化视觉参考，不代替原生 Overlay 操作与 Studio Owner 接受。没有把额外截图运行重复计入通过数量。

资产核对：MainCafe、Phase 8 Validation Scene、共享 Phase 7 footprint material、Phase 5 源字体的 hash 保持；Pick-up prefab 与 P8 字体 `.meta` 保持，模型重复更新的 GUID / mesh local ID / footprint 引用测试通过。旧 Pick-up prefab 与 P8 字体副本位于同一 outputs 子目录的 `asset-backups/`，可以恢复本轮资产变化。没有保存生产 Scene。

Studio Owner 已确认上述 M6 修复复测通过。当时 M1–M10 人工 PASS，后续 M11–M18 的结果见本节顶部最新记录。M6 的 incompatible Slot 子项仍无法用当前素材构造，保持“未覆盖”，不擅自改为 N/A。

独立 Engineering / QA bounded review：未发现新阻挡项，已独立读取三套最终 XML；随后 Studio Owner 确认 M6 复测 PASS。当时 manual 是 **10 PASS / 0 FAIL / 8 Pending**，旧 Touch 顺序问题继续开放；不是 Phase closeout 或 merge-ready。

### M1/M2 manual feedback 修复（2026-09-08，历史复测记录）

- M1：两层 Catalogue ScrollRect 不响应滚轮；保留原有鼠标 / touch 拖动和 Camera wheel zoom。
- M2：CR/CM 未确认 ghost 复用普通 furniture 的 hover height；footprint 留在 Surface Slot 或地面。地面只有临时显示坐标，仍 invalid 且不可 Confirm；斜视镜头下可松手再抓起。Confirm 回正式 Slot，Cancel 恢复来源；无 Counter 时仍显示 invalid ground ghost。
- 修改限于 `DecorationCatalogueView.cs`、`DecorationModeController.cs`、`SurfaceMountedPreviewView.cs`；测试为新增 `Phase8CatalogueWheelInputPlayModeTests.cs`（含 metadata）与扩展 `Phase8FunctionalSurfaceInteractionPlayModeTests.cs`；文档为本 guide、Phase 8 design / test-cases 与 Roadmap。没有新增 system / feature，没有修改 Router、layout domain 或 Scene / Prefab。
- Focused GREEN：`19/19 PASS`（M1 7 + M2 12），`outputs/phase8-manual-m1m2-20260908/m1m2-green-verified.xml`。
- Direct EditMode：`61/61 PASS`，覆盖 transaction、layout、support、Controller contract 和程序集边界；`outputs/phase8-manual-m1m2-20260908/direct-regression-editmode.xml`。
- Direct Core PlayMode：`195/195 PASS`，`outputs/phase8-manual-m1m2-20260908/direct-regression-core.xml`；包含全部 M2 新增 12 cases、Phase 8 core、Phase 6/7 UI 和直接相关的普通家具 hover / Camera regression。
- Direct Scene / Input：**32/33，非整体 PASS**，`outputs/phase8-manual-m1m2-20260908/direct-regression-scenes-safe-clock.xml`。其中最终 M1 `7/7 PASS`、原有 Phase 8 Scene `8/8 PASS`；一个旧 Touch 顺序问题尚未解决，见下方。上述最新 Core / Scene 结果再次覆盖全部 19 个 M1/M2 新增 cases，均 PASS；没有删除或跳过失败案例。
- 所有最新通过套件的 failed / skipped / inconclusive 均为 0；Scene suite 为 1 failed、0 skipped / inconclusive。本轮按任务风险执行 focused / direct regression，没有重新宣称 9 月 4 日的 full EditMode / Windows Player 结果属于本次代码。
- 最终资产审计：`assets-before.csv` 中 1,388 个非 C# Assets / ProjectSettings 文件 hash 零差异，0 missing / unexpected；`.cs` 和 `.cs.meta` 是本次 source 改动，不计入资产基线。没有修复或覆盖 Scene / Prefab，无需恢复资产；未 commit / push / merge。

#### 旧 Scene / Input 顺序验证问题调查历史（2026-09-09 已解决，见本节顶部）

`Phase6DecorationRealTouchTests.CompositeOrder_MainCafeDecorationTouchThenPhase5ProductionTouchRestoresRuntimeIsolation` 在接于既有 Phase 8 Scene tests 后运行时，首次 Touch 点击 Decoration HUD 未进入；没有新的生产异常证据。排除新增 M1 fixture 后，旧 26 cases 仍为 `25/26`（`preexisting-scene-order-control.xml`）；原有 Touch fixture 单独运行 `18/18 PASS`（`phase6-real-touch-control.xml`）。这些文件均在上述同一个 outputs 子目录。因此已排除“必须先执行新增 M1 fixture 才失败”，但**完整 root cause 未确认，也未声称旧问题已修复**。本轮不修改旧 Phase 6 fixture；此项保持开放，Phase closeout 前需处理，不混入历史九项已接受 Minor。

中间的混合 228-case runs 与首次 33-case run 不是 GREEN。M1 输入诊断发现初始 Mouse event 未消费；新 helper 改用项目已有的 `device.lastUpdateTime + 0.000001` 安全测试时间，保留单次 wheel、消费确认和生产 Camera 断言，最终 M1 7/7。Unity 的 Editor→PlayMode transition-window 丢弃规则是该时钟调整的依据；未记录实际 transition-window 数值，因此不把这一机制写成已完整证明的根因。

Engineering / QA bounded code review：PASS；Studio Owner 现已确认 M1/M2 复测 PASS，但当前 overall Scene regression **不是 PASS**，不意味着 Phase 8 完成。

M1/M2 修复后，Studio Owner 曾明确反馈“1和2也pass了”，因此关闭这两项此前的 manual FAIL；不是由 automated PASS 推定。当前最新进度以本节顶部 M11–M18 验收记录为准。

### 本轮 review 修复记录（2026-09-04）

下面的“已完成”只指修复与对应 automated regression，不代表 manual PASS。

| 修复 / test case | Automated 状态 |
|---|---|
| 不同时产生普通家具与功能设备 Preview；当时的 Tab 不隐式 Cancel 规则 | 历史 PASS；跨 Tab 行为已由第 26 节获批规则取代 |
| CR / CM / Pick-up Store：确认后才删除；Cancel、Back、退出均安全 | 已完成 / PASS |
| invalid existing Pick-up 可进入 Preview 并移动恢复；各模式保留 pinch | 已完成 / PASS |
| 真实 Catalogue handle 重开后可继续创建 CR、CM、Pick-up，兼容 Phase 6 prefab | 已完成 / PASS |
| 多项 readiness 原因分别对应自己的设备、role、cell 与 IDs | 已完成 / PASS |
| 中文 / ASCII glyph 完整，长提示在顶部 SafeArea 内换行与滚动 | 已完成 / PASS |
| Pick-up 无站位时显示专用 action bar 原因，保留上一条正式 readiness | 已完成 / PASS |
| 损坏 binding / definition / direction 始终 blocking；缺方向设备不从 report 消失 | 已完成 / PASS |
| 拒绝 default 0×0 WallFootprint，避免零占用墙面物件 | 已完成 / PASS |
| Scene Configure：先校验后保存，dirty 状态提前拒绝，加载 / active / 回滚安全 | 已完成 / PASS |
| 唯一 loaded Scene 回滚；首次 Copy 前检查；反馈层修复实际落盘且重复配置稳定 | 已完成 / PASS |
| Scene 卸载时反馈 cleanup 不抛异常；真实 MainCafe 三 Counter 示例可达到 ready | 已完成 / PASS |
| Phase 6 migration 严格认可 Phase 8 反馈层；snapshot 只排除真实 TMP 临时渲染节点 | 已完成 / PASS |
| Phase 6 validator 认可 layout 与 tracker 生命周期；配置、可编辑字段、ignoreLayout 仍严格，验证不修改 UI / Scene | 已完成 / PASS |
| 已验证目标的临时 dirty 状态通过重新加载恢复；不额外 Save、不清除用户 dirty 状态 | 已完成 / PASS |

### 2026-09-04 全量自动化证据（历史 baseline，早于本次 M1/M2 修改）

| Suite | Result | XML（位于 `outputs/`） |
|---|---|---|
| Phase 8 focused EditMode | `190/190 PASS` | `p8r-final-focused-editmode-verified.xml` |
| Phase 6 migration / validator / layout guards | `372/372 PASS` | `p8r-final-phase6-migration-validator.xml` |
| Full EditMode | `1,689/1,689 PASS` | `p8r-final-full-editmode-round3.xml` |
| Full core PlayMode | `647/647 PASS` | `p8r-final-full-core-playmode.xml` |
| Full EditorSceneLoading | `80/80 PASS` | `p8r-final-full-editor-scene-loading.xml` |
| Real Windows Standalone PlayerWithTests | `619/619 PASS` | `p8r-final-full-standalone-windows64.xml` |

上表各套 XML 的 failed / skipped / inconclusive 均为 `0`。完整 EditMode 再次覆盖并通过全部 `190` 项 Phase 8 focused cases 与全部 `372` 项 Phase 6 migration / validator cases；后者包含 `18` 项 layout / tracker guards。core PlayMode 包含全部 `81` 个 Phase 8 runtime cases，真实 Scene suite 包含 `8` 个 Phase 8 cases。这些子集不另行重复计算；RED 与中间 diagnostic 文件不计为最终 GREEN。

历史完整 EditMode 的 `p8r-final-full-editmode.xml` 有 67 项失败，`p8r-final-full-editmode-verified.xml` 有 2 项失败，均不能计作 GREEN。共同原因与 tracker 生命周期已稳定复现并修复，Phase 6 兼容修复共新增 23 项 guards；最新第三轮完整回归已全部通过。最新全量 authority 只使用 `p8r-final-full-editmode-round3.xml`。

最终资产核对已通过：`1,984` 个 Assets + ProjectSettings files 与 `outputs/p8r-final-assets-baseline.csv` 零 hash 差异，`0` missing / unexpected files。第三轮的 `37` 个测试副作用资产已逐项备份并恢复，可恢复副本位于 `outputs/p8r-final-test-side-effects/`；逐文件审计为 `p8r-final-side-effect-audit.csv`，汇总为 `p8r-final-side-effect-summary.csv`。最后的生命周期修复只改 Editor validator 与 test source，没有改 runtime 或 Scene 产物；旧基线与 source 副本仍保留。

2026-09-04 五道 gate 历史记录：Automated 与最终资产核对 `PASS`；Engineering `PASS`；QA `PASS`；Production 内容 review `PASS`（P8-M-018 已补齐 fresh Scene 创建设备的前置步骤）；当时 Studio Owner Manual 18 项全部 `Pending / 未执行`。当前人工与修复进度以上方 2026-09-08 记录为准。

2026-09-04 当时停在 `Ready for Manual Review`；后续修复与验收结果以本节顶部记录为准。旧 Scene/Input 问题于 2026-09-09 修复；M6 未覆盖子项保留。Phase 完成与正式进入 Phase 8R 仍需另行决定。

九项 accepted/deferred Minor 的完整 register 在 `.superpowers/sdd/2026-09-02-phase-8-functional-furniture-layout-readiness/task-10-report.md`。这些是已知 test / diagnostic / polish limitations，不是隐藏 blocker，也不会自动成为 Phase 8R scope。

## 7. 已知限制与下一步

本节以下为 2026-09-09 的历史交接快照；最新收尾、已知限制和下一步以第 37 节为准。历史本地 `task-10-report.md` 不保证存在于 fresh clone，不能据此声称当前仓库有完整的旧 Minor register。

当前九项 Minor 主要是缺少少数直接 regression、边缘 diagnostic 不够完整，以及一个环境敏感的 performance threshold；完整清单和影响说明见上面的 `task-10-report.md`。它们不是本轮隐藏 blocker，也不会自动变成承诺功能。

本轮验证没有注入真实磁盘 I/O 故障，也不替代窗口 / SafeArea 变化与操作手感的人工检查。自动截图使用临时 ScreenSpaceCamera 渲染，只供摆放和可读性参考，不是原生 Overlay 的逐像素验收。

M18 专项技术代测及独立复查已 PASS，Owner 无需重做；M1–M17 历史人工 PASS。2026-09-09 五项修复与旧 Touch 问题已技术验证，人工复测见第 6 节。前三项 UX 的历史验证与人工步骤见 8.4；后续批准的 UX2 选项 2–5 和 footprint 以 8.5 为准，其他未列入 UX2 的建议仍未实施。M6 incompatible Slot 子项仍未覆盖，正式 Phase closeout / Phase 8R 切换另行决定。

最新补充：原色模型与墙饰向外悬浮已获批并实施，当前 Preview 合同、301 项回归与人工复测以 **8.8** 为准；8.5–8.7 保留既有 UX 与配色历史。

## 8. Phase 1–8 UX 与 Indicator Review（最新 Preview 交接见 8.8）

### 评审范围与总体判断

本节 8.1–8.3 保留实施前 review 的背景与候选建议；Owner 首先批准前三项，历史交回见 8.4，随后又批准 UX2 选项 2–5 与半透明光感 footprint，范围见 8.5。候选表中的其他造型、icon、Exit 文案与新系统没有自动加入范围。

在 bug gate 通过后，按 Parker Game Studio 的 UX / Art 职责分开检查：UX 关注操作、反馈和恢复；Art 关注落点、状态与模型的可读性，最终视觉喜好由 Owner 决定。依据是当前 branch 的代码、Phase 1–8 guides，以及 9 月 8 日已验收的 `outputs/phase8-manual-m6-20260908/pickup-table.png`、`pickup-ground.png`、`readable-feedback.png`。本轮没有新拍原生 Overlay 截图，也没有假装进行了真人/真机 playtest；旧截图为 640×480、临时 ScreenSpaceCamera，只能作观察参考，不能据此判定正式手机字号或对比度达标/不达标。

整体上，`选择 → Preview → Confirm / Cancel` 的骨架合理，状态回滚也可靠。现阶段更像稳定的装修与营业布局工具；最大体验成本是玩家需要自己理解“为什么不行、接下来点哪里”。当前 Phase 不包含客人、订单、经营收益或 Save，所以这些尚未出现不算 bug，也无法据此评价完整经营循环的乐趣或长期留存。

以下 A/B/C 是实施前建议优先级，不是 bug severity；表中的“已实现”记录第一轮三项。后续 UX2 是否获批、实施到哪一步，以 8.5 为准，不把整张候选表视为已批准。

### 8.1 操作流程建议

| 优先级 | 实施前摩擦 / 玩家例子 | 建议与边界 |
|---|---|---|
| A · 历史实现 | active Preview 时，切 Tab / 选新物品被拒绝；展开 Catalogue 又隐藏 actions，容易像按钮没反应 | 当时提供“返回编辑”并保留 Preview；跨 Tab 阻挡后来由第 26 节的自动 Cancel 规则取代，同分类选新家具的限制不变。 |
| A · 已实现 | invalid Preview 是红色，但顶部仍写“可以营业” | 顶部标为“已确认布局：已就绪 / 还需调整”；下方单独显示当前编辑。未发布过 readiness 时顶部留空，Preview 不改变已发布结果。 |
| A · 已实现 | placement Toast 约1.8秒后消失，玩家思考时可能忘记原因；通用英文不够具体 | 当前编辑提示持续可见；合法后改为“位置有效，可以确认”。离开台面提示“请将物件移到柜台上的摆放位”；其他原因仍分别映射，不实施完整 localization system。 |
| A | Exit modal 的 `Discard Changes` 容易被理解为撤销整次装修 | 文案明确“放弃本次预览并退出；已确认摆放保留”，不改 transaction、不加额外确认层。 |
| B | 连续新增三件设备，需要反复点 Catalogue handle | 最小方案是明确写“继续添加”；进一步比较“新增 Confirm 后回到原 Catalogue 位置、编辑已有物件后保持收起”。后者改变已验收流程，需 Owner 选择，不默认连放。 |
| B | 切 Tab 重建分类行并回到列表顶部，看过的 Wallpaper 需要重新找 | 本次 Decoration session 内记住各 Tab / 各行的浏览位置；不涉及 Save。保留 M1 已接受的拖动浏览与 Camera wheel zoom。 |
| B | 新设备按 support InstanceId 排序选首个有效 Slot，不一定在玩家正看的柜台 | 候选规则：优先屏幕内、靠近画面中心的空闲 Slot，再用稳定 fallback；不自动 Confirm、不强行移动 Camera。需验证无 Slot、occupancy、屏幕边缘等情况。 |
| B | Floor 的 `Undo Last / Apply All / Confirm` 不容易理解各自范围 | 使用“撤销上一步 / 铺满整个房间 / 确认本次修改”，旁边显示受影响格数；Apply All 仍只改 Preview。避免把 Undo 误说成全局撤销已 Confirm 的装修。 |
| C | 新玩家仍依赖外部 Guide | 空闲时给一句可忽略提示：“选物品 → 拖动 → ✓确认；×取消”；设备行注明“放在柜台上”。首次教程、任务引导、进度保存是新 feature，另行设计。 |
| C | 固定 PanSpeed 在不同 zoom 下可能有不同的屏幕跟手感 | 先在近/中/远 zoom、不同屏幕做等距离拖动比较，再决定是否按 Camera size 校正。连同 edge auto-pan 调校；这是待测候选，不是已确认手机缺陷。 |

### 8.2 Footprint 与画面 Indicator 建议

建议先分开四件事：“这个物体是什么”“我选中了什么”“现在放得是否合法”“已经确认了什么”。当前绿色同时被 UI Accent、valid Preview、confirmed Pick-up footprint 使用；普通家具/设备 ghost 还会整体染色。颜色可以保留，但每种状态需要自己的形状与显示时机。

候选状态表（不是已批准 palette，实际颜色需在木色、深灰地面、浅墙和不同光照下试）：

| 状态 | 颜色方向 | 形状 / 时机 |
|---|---|---|
| 已选中、尚未移动 | 米白或中性亮色 | 四角 bracket 或清晰外轮廓；不先暗示一定 valid |
| 有效 Preview | 清楚的薄荷绿 / 青绿 | 完整占用边框 + 浅填充 + 小 ✓；保持真实 footprint cell 大小 |
| 无效 Preview | 珊瑚红 | 边框 + ×；必要时加斜纹，只标出冲突格；不能仅靠换色 |
| 已确认、未选中 | 中性或淡出 | 默认减弱/隐藏大面积占地色；取餐点本体保留，选中时再强调落点 |
| 入口保留区 | 保留现有蓝色语义 | 门/进出方向图标 + 虚线边界，与设备 footprint 区分 |

具体可选改进：

1. **先减轻整块填充。** 当前取餐点的绿色/红色块几乎盖住柜台顶面。试验浅填充配清楚边框，让材质仍可见；透明度仅作试验变量，不先锁定数字。边框不能偏离真实占用区域。
2. **在落点本身加 ✓ / × 或形状。** 即使用户不看 action bar，也能读懂是否合法。颜色不是唯一信息渠道；这是参考 [W3C Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html) 的设计原则，不是在宣称本游戏已完成 WCAG 认证。
3. **区分“不能放”和“还没确认”。** 无效时显示具体原因；有效但未确认时保留轻边框/小提示。确认后短暂反馈再收敛，避免全部桌面永久亮绿。
4. **灰白取餐锥体先保留。** Owner 已接受它的宽深0.28m、高0.50m、opaque Lit 和硬边；优先试轮廓、接触阴影或轻量底座，让它在浅墙前也清楚，不重新把本体染成红绿。技术上需验证遮挡、透明层级与 mobile 成本。
5. **如果要换造型，先比较三个方向。** A 保留当前细锥体并改善轮廓（改动最小）；B 小圆盘杯子图标 + 短指针（更像交互标记）；C 小型“取餐”桌牌（更像店内道具，但需新资产）。B/C 都是待 Owner 选的视觉/资产方案，本轮不制作。
6. **普通家具/CR/CM ghost 尽量保留原材质。** 此项中的“模型原材质、仅 footprint 承载红绿”现已单独获批并实施，见 8.8；没有新增局部轮廓。普通家具/CR/CM 原悬浮高度不变。
7. **功能朝向只在需要时显示。** 选中收银机/咖啡机时，可用短箭头标出员工操作侧；不要给 Pick-up 加 Rotate 或虚构固定方向。完整 employee/customer 或动线路径层保持默认关闭，另行决定是否做玩家可切换帮助层。
8. **失败最好标在出问题的位置。** 除中文原因外，候选可短暂标记被挡住的 cell / 设备，减少玩家用坐标猜位置。点击错误定位、路线可视化都涉及新增交互，不能混作本轮 bug fix。
9. **ActionBar 提高动作辨识度。** 当前 compact 命中区已是48×48 logical pixels，不应只因640×480截图就宣称尺寸不合规；应在正式比例看图标是否足够清晰、✓/×是否好区分。考虑更大的图标、清楚的 Confirm 主次和简短标签；不能依赖 Hover 才知道按钮作用。
10. **让辅助图形安静下来。** 非编辑/未选中时弱化网格与 footprint；只在选中或出错时加强信息，避免所有辅助图同时抢注意力。若加脉冲/确认动画，短暂、可减弱，复用 Reduced Motion，不做持续闪烁。

关键轮廓/图标须在实际背景下量测，不凭色值保证可读性；对比检查参考 [Xbox Accessibility Guideline 102](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102)。本轮没有执行正式 contrast audit。

### 8.3 建议先做什么

第一小步已批准并实现：**Preview 与正式 readiness 分层、无效原因持续可见、编辑锁定与返回编辑**。这些不改变核心摆放规则；Exit modal 的文字调整仍是未实施建议。

历史建议的下一步曾是比较 **footprint 边框/浅填充/非颜色标记**，再讨论 Catalogue 与初始落点。Owner 后来批准的实际顺序和边界见 8.5：只采用指定的 Catalogue / readiness / Slot / Floor 改进及半透明光感 footprint，现有取餐锥体、模型与 icon 保留。

确认后全局 Undo、复制连放、自动定位、完整教程、动线帮助层都是独立 feature 候选；没有自动加入 Phase 8R scope。正式 Android/iOS 手指遮挡、DPI、SafeArea、近远景 camera 手感仍需真机验证；本轮建议不是 Owner 视觉接受。

### 8.4 三项 UX 修改历史交回（2026-09-09，Ready for manual review）

本小节是 UX2 实施前的历史 authority：`codex/phase-8-functional-furniture`，当时 HEAD 为 `4c878bc`，修改尚未 commit / push / merge。之前五项 bug 修复均保留。下表 789/227 只证明这一轮三项 UX，不是 8.5 的 UX2 PASS。

已修改的 runtime files：`DecorationModeController.cs` 管理编辑提示与安全返回；`DecorationActionBarView.cs` 持续显示当前原因并放在下方操作区；`DecorationCatalogueView.cs` 增加说明和 Return 按钮、预留滚动区域；`PlacementFeedbackMapper.cs` 区分已确认布局与当前编辑的文案。`Phase8FeedbackAssets.cs` 只补 P8 静态字体字符；`NotoSansSC-Phase8 SDF.asset` 的 GUID 与 font/material/atlas local file IDs 保留。

没有修改 Scene、Prefab、footprint、模型、Camera、ProjectSettings、Save 或 placement domain；没有重建全部 P8 assets。Floor/Wall 的当前说明显示在 Catalogue 内，不与 ActionBar 重复；普通家具沿用 Catalogue 里的现有 DisplayName，设备和取餐点使用中文名称。

| Test case | 已实现行为与 automated 结果 | Owner manual |
|---|---|---|
| UX-01 | 首次未 Confirm 前顶部为空；已有 report 在 Begin / Move / Rotate / failed Confirm / Cancel 时不被 Preview 覆盖。已完成 / PASS | Pending |
| UX-02 | 无效原因超过 2.3 秒仍真实可见；合法后旧错误被 valid 文案替换；编辑结束后隐藏。已完成 / PASS | Pending |
| UX-03 | 展开 Catalogue 保留具体原因；拒绝新物件/换 mode 时说明原因；Return 只恢复同一 Preview 和 actions，readiness version 不变。已完成 / PASS | Pending |
| UX-04 | Store modal 独占输入；Cancel Store 保留 Preview 与错误；成功 Store 才清除并发布。支撑家具 Store blocked 后，旧 Collapse / 再次 Store 再 Cancel 都保留具体 blocker 文案与 diagnostics。已完成 / PASS | Pending |
| UX-05 | Floor/Wall 连续换两个素材仍编辑同一 target/baseline；不出现新建锁定，不强制 Return。已完成 / PASS | Pending |
| UX-06 | Return 使用现有 UI pointer ownership；重复 Configure 不重复触发；结束后移除说明并恢复原 viewport；字体覆盖新文案，真实 Scene 上下状态区不重叠。已完成 / PASS | Pending |

| 最新 suite | 结果 | XML（`TestResults/`） |
|---|---|---|
| 完整 Editor PlayMode | **789/789 PASS**：core 686 + real Scene/Input 103 | `ux-20260909-all-play-verified.xml` |
| 全部 Phase 8 EditMode + surface session regression | **227/227 PASS** | `ux-20260909-edit-verified.xml` |

两份 XML failed / skipped / inconclusive 均为 0，不把子集相加。没有重跑 full EditMode、standalone Player 或 Android/iOS 真机。独立 code review 与最终 scope / test-evidence review 无未处理 Critical / Important。

TDD 证据：`ux-20260909-red-play.xml` 发现首次顶部泄漏与缺少 Return；`ux-20260909-font-red.xml` 发现 37 个新字形缺失；`ux-20260909-modal-collapse-red.xml` 复现旧 Collapse 覆盖 Store 错误；`ux-20260909-visual-red.xml` 两项预期失败捕获上下反馈区域碰撞与不可执行的落点文案。最终全部纳入上表验证。新增测试中的 fixture 输入采样、immutable snapshot 断言和 namespace 编译错误也已修正；它们不计为 production bug 或有效功能 RED。

完整回归最初另有两条旧文案断言失败，已按批准的新 UX 迁移：P6 MainCafe real Touch 仅 4 additions / 3 deletions 的 expected copy / header 断言；P7 Wall Mounted 仅四条错误和 valid 文案断言。没有改其 Touch 时序、操作、Confirm disabled、layout、footprint 或 cleanup 检查，也没有删除/跳过 tests。

真实 Scene 截图：`outputs/ux-20260909-valid-preview.png`、`ux-20260909-invalid-preview.png`、`ux-20260909-expanded-catalogue.png`。已检查上下反馈分区、三行说明和 Return 可见、首行物品不被遮挡。截图为 640×480、临时 ScreenSpaceCamera 的技术证据，不是原生 Overlay 逐像素验收，也不代替手机 DPI/SafeArea 或 Owner 视觉接受。

#### 你接下来怎么测试（6 步）

在这个 P8 worktree 打开 `Assets/Scenes/MainCafe.unity`，进入 Play Mode / Decoration Mode：

1. 新开 Scene，第一次 Confirm 前把设备拖到地面，等待至少 3 秒：顶部不冒出 Preview 错误；下方持续显示“正在编辑 / 尚未确认”和回到柜台的指引。
2. Confirm 后再编辑已有物件，移到非法位置：顶部已确认报告不变。移回合法位置后显示“位置有效，可以确认”；Cancel 恢复原物件并清当前提示。
3. 保持 invalid Preview，展开 Catalogue，点同一分类内另一个家具：仍保留原 Preview；点“返回编辑”恢复原物件、位置和 actions。跨 Tab 现按第 26 节直接取消旧 Preview，不再要求先确认或取消；既有 Notice 后续精简以较新章节为准。
4. 重复上一步，改用原有 Collapse 按钮：同样能恢复编辑。编辑有台面物件的柜台并尝试 Store 后，收起/展开目录不应把移除台面物件的说明变成“位置有效”。
5. 编辑已有设备/取餐点并打开 Store：后面的 UI 不可穿透。Cancel Store 保留 Preview、位置和原错误；真正 Confirm Store 后才移除并更新已确认布局。
6. Floor、Wall 各连续选择两个素材，仍能在当前 Preview 内换样式；分别试 Confirm 和 Cancel。最后看一下字体、下方提示与 Catalogue 的间距是否舒服。

这一轮当时停在 **Ready for manual review**。上表 Owner manual 仍为 Pending；不自动宣布 Phase Completed 或进入 Phase 8R。之后追加获批的范围单独记录如下。

### 8.5 UX2：选项 2–5 与半透明光感 footprint（2026-09-09）

本节记录提亮前 UX2 baseline：当时实现与技术核验已完成，交回 **Ready for manual review**；下面 7 项 Owner manual 全部 **Pending**。本节 813/235 与截图不替代后续 8.6 的亮色验证；8.4 的 789/227 继续仅作历史证据。

已批准范围：

- **选项 2 — Catalogue。** 同一次 Decoration session 记住各 Tab 的上下位置与各 CategoryId 行的左右位置，重建布局后恢复并限制在有效范围；退出再进入重新开始。没有 active Preview 时收起入口显示“继续添加”；有 Preview 时保留原 Catalogue 标签与“返回编辑”限制。Confirm 后仍收起，只有玩家点入口才展开，不连续自动放置。
- **选项 3 — 已确认布局提示。** 顶部先显示紧凑摘要；存在更多原因时可“查看详情 / 收起详情”，完整保留 blocking 与 warnings、中文角色和位置说明。无报告时仍留空，展开/收起不重新发布 readiness，Preview 不覆盖已确认报告。
- **选项 4 — 新设备起点。** 只对 Cash Register / Coffee Machine 从合法空 Slot 中优先选择 Camera 屏幕内、距画面中心最近的 Slot；同距、没有可见合法 Slot 或缺少 Camera/marker 时沿用稳定 fallback。这里的“屏幕内”按 Slot 投影判断，不新增遮挡检测。Pick-up Point 的初始排序保持原样，不自动移动 Camera 或 Confirm。
- **选项 5 — Floor。** 显示“整个房间 / 逐格涂抹”和“已改 N 格”；N 比较当前 Preview 与已确认地板的真实差异，重复涂抹不虚增，Undo 可减少。按钮说明为“撤销上一步 / 铺满整个房间 / 确认本次修改”；Whole Room 原有 Undo 禁用规则保留，撤销只影响本次 Preview。
- **Footprint。** 普通家具地面、墙饰、台面 CR/CM、Pick-up Point 四条显示路径采用半透明、柔和边缘的绿/红光感填充，底下材质仍能看见。占用尺寸、位置和有效性规则不变；白色取餐 icon/锥体、物体模型、入口蓝色区域保留。没有新增 Light、Bloom、模型换色或 Camera 规则。

选项 1 保留现有 icon；Exit modal 文案不动。不新增 Save、全局 Undo、自动连放、定位 Camera 或完整 visual redesign，不自动关闭 Phase 8 / 进入 Phase 8R。

本轮修改文件按用途分组：

- `DecorationCatalogueView.cs`：浏览记忆与“继续添加”入口。
- `ValidationMessageView.cs` + `PlacementFeedbackMapper.cs`：已确认布局的摘要、详情与原完整报告。
- `DecorationModeController.cs` + `DecorationActionBarView.cs`：设备 Slot 起点、Floor 范围/格数与 UI 接线。
- `GridHighlightView.cs` + `WallMountedPreviewView.cs` + `SH_FootprintLight.shader` + `Phase8FootprintLightAssets.cs`：四条 footprint 路径的柔和透明填充及限定 authoring。
- 两份 footprint Prefab、`M_FootprintLight.mat`、P8 static font（由 `Phase8FeedbackAssets.cs` 补字形）：限定资产更新，保留 Scene、模型与既有配置。

| UX2 技术核验 | 当前结果 | 最终证据 |
|---|---|---|
| 完整 Editor PlayMode | **813/813 PASS**：core 707 + real Scene/Input 106 | `TestResults/ux2-20260909-all-play-verified.xml`，exit 0 |
| 全部 Phase 8 EditMode + SurfaceSession regression | **235/235 PASS**；不是 full EditMode | `TestResults/ux2-20260909-edit-verified.xml`，exit 0 |
| Footprint / 字体 focused asset tests | **8/8 PASS**，已包含在 235 内，不重复相加 | `TestResults/ux2-20260909-assets-green.xml`，exit 0 |
| Full regression 前后资产一致性 | **1,393 files PASS**；changed / missing / unexpected 均 0 | `TestResults/ux2-20260909-assets-audit.json` |
| 独立 code review、最终 XML/source audit + 7 张截图技术复核 | **PASS；无未处理 Critical / Important** | 本轮独立 review / evidence audit 与下方已查看截图；不是 Owner manual PASS |
| 新增 Owner manual | 7 项 Pending | 逐项记录在配套 Test Cases 的 UX2 ledger |

上述三份 XML 的 failed / skipped / inconclusive 均为 0，runner exit code 与 XML 均已核对。本轮未执行 full EditMode、standalone Player 或 Android/iOS 真机；窄屏检查使用 runtime 真实 Prefab 的 **320 logical pixels** fixture，不是手机设备。

资产一致性基线采于本轮已批准的 footprint/font authoring 完成后，检查 full regression 前后非 C# Assets + ProjectSettings 的 1,393 个文件 hash，不包含 C# / `.cs.meta`。这证明回归没有产生资产漂移，不是声称本轮没有资产修改：本次资产变更限于新 shader/material、两份 footprint Prefab 和 P8 static font，没有 Scene / ProjectSettings 改动。

完整 PlayMode 的中间结果曾为 **810/813，非 GREEN**。随后保留原行为断言，迁移三处旧预期：Floor Confirm 的中文文案；完整 readiness 原因需点击“查看详情”后检查；footprint 由 Cube 改 Quad 后，在父空间验证旋转后的 `1 × 1` 平面几何。没有删除或跳过这些断言；最终 813/813 才是本轮 authority。

以下 7 张均由根代理与独立 UX reviewer 实际查看，来自真实 `MainCafe` 的 **640×480、临时 ScreenSpaceCamera** 技术截图；不是原生 Overlay 逐像素或手机 DPI/SafeArea 验收，也不代替 Owner 视觉接受：

- [Readiness 紧凑摘要](../outputs/ux2-20260909/readiness-compact.png)
- [Readiness 完整详情](../outputs/ux2-20260909/readiness-details.png)
- [Floor 范围与真实格数](../outputs/ux2-20260909/floor-scope.png)
- [普通家具 footprint](../outputs/ux2-20260909/furniture-footprint.png)
- [墙饰 footprint](../outputs/ux2-20260909/wall-footprint.png)
- [有效 Preview](../outputs/ux2-20260909/ux-20260909-valid-preview.png)
- [无效 Preview](../outputs/ux2-20260909/ux-20260909-invalid-preview.png)

#### 你接下来怎么测试（7 步，全部 Pending）

在当前 P8 worktree 打开 `Assets/Scenes/MainCafe.unity`，进入 Play Mode / Decoration Mode。沿用已有场景与第 4–5 节的摆放方法，不运行 Build Assets / Configure menu。每步完成后按 `UX2-M-001` 到 `UX2-M-007` 分别反馈 PASS/FAIL；若当前内容不足以滚动或没有可用测试条件，记下该限制，先保留 Pending。

1. **UX2-M-001 — 找回浏览位置。** 先结束当前 Preview；在有足够内容的 Tab 上下滚动，并横向拖一行。切另一个 Tab 浏览，再切回，原位置应恢复，各行互不串位。退出装修再进入，应回到新 session 的起点。
2. **UX2-M-002 — 继续添加。** 选家具或设备，Confirm 后目录保持收起，入口显示“继续添加”；点它才展开。再开始一个 Preview，展开目录时仍显示当前编辑与“返回编辑”，换物件仍须先 Confirm/Cancel；不会自动多放一件。
3. **UX2-M-003 — 摘要与完整原因。** 先 Confirm 一次生成已确认报告，按第 5 节分别观察缺设备和可营业但有 warning 的组合。有多项原因时点“查看详情”，检查每条 blocking/warning、角色和位置都可读，再收起；单一简单原因可以没有详情按钮。拖动 invalid Preview 时，上方报告不变。
4. **UX2-M-004 — 新设备起点。** 把想使用的空柜台移到画面中心，分别选 Cash Register、Coffee Machine：Preview 应优先落在屏幕内靠中心的合法空 Slot。占住中心 Slot 后再选一次，应跳过它；画面不会自己平移，仍需你 Confirm。Pick-up Point 继续沿用旧起点规则。
5. **UX2-M-005 — Floor 范围。** 选逐格模式与不同于当前地板的样式，涂两格，看真实计数增加；试重复涂同格、“铺满整个房间”和“撤销上一步”，计数应相应保持/增加/减少。Cancel 后地板回到已确认状态。换 Whole Room 后 Undo 仍禁用，使用 Confirm/Cancel 完成本次修改。
6. **UX2-M-006 — 看 footprint。** 分别编辑普通家具、墙饰、CR/CM、Pick-up Point，比较合法绿与非法红；设备/取餐点可拖到地面观察非法状态，墙饰可用重叠位置。底下地板/柜台/墙面材质应可见，边缘不偏离真实占用；Confirm 仍受原规则限制。白色取餐标记、模型的既有颜色/悬浮规则与入口蓝区应保持原样。
7. **UX2-M-007 — 小窗口与清理。** 在较窄 Game 窗口展开完整详情，正文可以滚动，详情按钮不遮字；点击/拖动通知或 Catalogue 不应同时拖动场景。检查 Floor 中文按钮与说明是否完整可读；Cancel、退出再进入后没有旧 Preview、旧详情或 Console 新 exception。

所有新增 manual 结果继续为 **Pending**。历史 M1–M17 Owner PASS、M18 technical PASS 原样保留；M6 incompatible Slot 未覆盖子项仍需独立处理。本节交接不代表 Phase Completed，也不授权 commit / push / merge。

### 8.6 Footprint 亮色 follow-up（2026-09-09，上一版历史；最新见 8.7）

Owner 批准只提亮 footprint 并保留透明度；本版实现参数为 `_TintBrightness = 4`、`_TintSaturation = 1.5`，不是 Owner 亲选的数值。opacity **0.45**、softness **0.12**、bounds 与 depth 规则不变。模型、白色 icon、Theme/UI、入口蓝区和既有操作规则不动，没有 Light/Bloom。

主要修改 `SH_FootprintLight.shader` 与 `Phase8FootprintLightAssets.cs`；`M_FootprintLight.mat` 仅增加上述两个 float。没有修改 Prefab / Scene；截图 helper 改存 `outputs/footprint-bright-20260909/`，旧 `outputs/ux2-20260909/` 原样保留作提亮前对照。

新像素测试使用真实 **ThemeValid / ThemeInvalid / WallValid** tint，连同旧 7 项共 **10/10 PASS**：`TestResults/footprint-bright-20260909-edit-green.xml`，failed/skipped/inconclusive 均 0，test runner / authoring exit 0。有效 RED 是 10 项中的 7 PASS + 3 项预期亮度 FAIL；最初 `Assert.Multiple` 不兼容导致的编译错误不计 RED。直接 PlayMode **84/84 PASS**：`TestResults/footprint-bright-20260909-play-green.xml`，MainCafe 10 + WallTouch 58 + FootprintLight 2 + FunctionalSurfaceView 14，failed/skipped/inconclusive 均 0、exit 0。本轮未重跑完整 Phase / full EditMode / Player / Android/iOS 真机，84 项不是旧 813 项的重跑。

独立 code review 无 Critical/Important；已记录 Minor：**WallInvalid 没有独立像素单测**。根代理与独立 UX reviewer 已完成 4 组前后截图复核，无新增过曝、串色、遮盖或不透明厚板问题，Visual technical 无阻挡；Owner 亮度偏好仍 Pending。旧 `outputs/ux2-20260909/` 同名文件保留作对照。新图仍是真实 MainCafe 640×480 临时 ScreenSpaceCamera，不代表全部状态组合或手机验收：

- [普通家具提亮后](../outputs/footprint-bright-20260909/furniture-footprint.png)
- [墙饰提亮后](../outputs/footprint-bright-20260909/wall-footprint.png)
- [有效 Preview 提亮后](../outputs/footprint-bright-20260909/ux-20260909-valid-preview.png)
- [无效 Preview 提亮后](../outputs/footprint-bright-20260909/ux-20260909-invalid-preview.png)

限定范围已核对：两份 Prefab、Theme、font、四份 View 源码与 MainCafe 共 9 个文件 hash 不变；material 精确 diff 只有两个新增 float，opacity 0.45 / softness 0.12 未变。

人工复测（全部 **Pending**）：

1. 在 MainCafe 编辑普通家具、墙饰、CR/CM 与 Pick-up Point，查看绿/红 footprint 是否比 8.5 更明亮，同时仍看得见底下材质；补看 WallInvalid。
2. 对照边缘、占用尺寸与遮挡：柔边宽度、落点、深度关系不应变化，模型/白 icon/Theme UI/入口蓝区应保持原样。
3. 试拖动、Confirm、Cancel，再退出装修；操作和合法性判断不变，记录是否刺眼、颜色难分或有残留。

当前交回 **Ready for manual review**，Owner manual 仍需实际反馈；不 commit / push / merge，不自动 Phase closeout。

### 8.7 红绿灯式 Footprint（2026-09-09，配色沿用；最新 Preview 见 8.8）

Owner 看过 8.6 后要求更鲜艳、接近红绿灯，并批准提高投影自身发光亮度。当前实现参数由开发调试选择：`_TintBrightness = 6`、`_TintSaturation = 3`、`_LightIntensity = 1.5`，不是 Owner 指定数值。仍保留 **0.45 不透明度、0.12 柔边**，真实占用边界与 depth 不变。

仅调整 `SH_FootprintLight.shader`、`Phase8FootprintLightAssets.cs` 和生成的 `M_FootprintLight.mat`；对应像素测试增加覆盖，截图 helper 换新目录。shader 先将 RGB 乘 opacity，再以 `Blend One OneMinusSrcAlpha` 混色，避免 LDR 在混色前截断发光值；RGB 仍按 `光色 × opacity + 底色 × (1-opacity)` 合成。没有新增 Light / Bloom，也没有修改 Camera/HDR 配置、Scene、Prefab、Theme、模型/icon 或操作规则。

**本轮自动化已完成：**

- **PASS — 四种真实配色：** ThemeValid、ThemeInvalid、WallValid、WallInvalid 均比 8.6 更亮；补齐旧版 WallInvalid 未独立单测的缺口。
- **PASS — 透明与柔边：** HDR float readback 验证截断前底色贡献约 55%、边缘回归底面；原纯 RGB smoke、材质独立性、depth/mesh、dirty guard 与 authoring 幂等检查保留。
- **PASS — 亮灰/暖底：** 实际 LDR 像素保留明显红绿分离，不因提亮变成白色光板。注意最终显示仍可能截断主色高光，不能承诺所有亮底的每个通道都保留完整 55% 纹理。
- **PASS — focused EditMode 15/15：** `TestResults/footprint-signal-20260909-edit-final-green.xml`。有效 RED 为 `footprint-signal-20260909-red.xml` 的 11 PASS + 4 预期亮度 FAIL；中间 `edit-green.xml` 实为 11/15（LDR 提前截断）、`premultiplied.xml` 为 14/15（绿色副通道过亮），均不算 GREEN；没有降低断言阈值。
- **PASS — 直接 PlayMode 84/84：** `TestResults/footprint-signal-20260909-play-green.xml`，MainCafe 10 + WallTouch 58 + FootprintLight 2 + FunctionalSurfaceView 14。两套最终 failed/skipped/inconclusive 均 0、exit 0，最终 authoring exit 0。未重跑 full Phase / Player / Android/iOS 真机。

根代理与独立 Technical Art / QA reviewer 已查看 4 组前后截图，无新增整体泛白、边界扩大或模型遮盖等技术阻挡；**这不代替 Owner 视觉偏好验收**。图仍为真实 MainCafe 640×480 临时 ScreenSpaceCamera，不是手机截图；8.6 的旧图原样保留：

- [新版绿色 Preview](../outputs/footprint-signal-20260909/ux-20260909-valid-preview.png)
- [新版红色 Preview](../outputs/footprint-signal-20260909/ux-20260909-invalid-preview.png)
- [新版普通家具 footprint](../outputs/footprint-signal-20260909/furniture-footprint.png)
- [新版墙饰 footprint](../outputs/footprint-signal-20260909/wall-footprint.png)

范围核对：两份 Prefab、Theme、MainCafe、GraphicsSettings、QualitySettings 共 6 项 hash 不变。Unity 测试产生的旧 WallInvalid 材质空白与 `_Color` 浮点尾数变化已恢复，`_BaseColor` 未变，该文件没有保留改动；这不是全部 Assets audit。

人工复测（全部 **Pending**）：查看四种 footprint 的有效/无效红绿光是否够醒目、是否刺眼；在浅色/深色底面检查柔边和底纹；拖动、Confirm、Cancel 并退出装修，确认无残留。Owner 认可后再标记对应 manual PASS。

停在 **Ready for manual review**；仍在 P8 branch，不 commit / push / merge，不关闭 Phase 8 或切换 Phase 8R。

### 8.8 原色模型与墙饰悬浮 Preview（2026-09-09，最新 authority）

Owner 看过相框和长搁板截图后批准本轮小改动：

- 普通家具、Cash Register / Coffee Machine 在有效、无效、恢复有效时都保留模型原材质、颜色、纹理与已有外观属性。仅 footprint 用红绿表示合法性，既有 symbol、原因文字与 Confirm 禁用规则保留；不是把模型涂白。
- Wall Decor / Window Preview 以原贴墙位置为基准，沿目标墙面朝房间方向额外浮出 **20 cm**。此数值是开发试调值；Confirm 回到原来 Base Wall Surface 外 1 mm 的位置，底部高度不跳，Cancel 不改原布局。
- footprint 留在墙面、避开饰条的原投影平面，表示真实 Wall Slots，不跟模型浮出，也不缩成模型轮廓。相框占 1×2、长搁板占 2×1；模型较矮，因此光可能露在上方。这不是把 Preview 垂直抬高，不能只居中 Preview 而让 Confirm 上下跳。

本节只覆盖 8.5 / 8.7 中“模型颜色不改、墙饰悬浮不改”的旧约束；8.7 红绿灯配色、0.45 opacity / 0.12 softness、真实占格和其他 UX 操作继续沿用。没有新 Light/Bloom、模型资产、Scene/Prefab/Theme、Camera 设置、Save 或 domain 改动。

**已完成的自动化：** `TestResults/preview-natural-20260909-play-green.xml`，直接 PlayMode **301/301 PASS**，failed/skipped/inconclusive 均 0、runner exit 0。包含 P6 Scene 169、P7 SurfaceScene 46、WallTouch 58、P8 FunctionalSurfaceView 15、MainCafe 11、FootprintLight 2；本轮 focused cases 已含在其中，不重复相加。

| Case | 已完成的自动核验 | Automated | Owner manual |
|---|---|---|---|
| NP-001 | 普通家具 true→false→true 不覆盖材质/texture/MPB，clone collider/selection 安全；footprint 红绿与不同 symbol 恢复。 | PASS | Pending |
| NP-002 | CR 与 CM 原色、红绿 footprint、回到有效 Slot 和 Preview/domain 隔离。 | PASS | Pending |
| NP-003 | 两面墙/跨墙拖动、1×1 与 1×2、高度/朝向、Confirm 去除 20 cm offset、Cancel 恢复。 | PASS | Pending |
| NP-004 | 五个真实墙饰/Window prefab 的悬浮/材质/mesh/interaction body；MainCafe 相框和搁板真实占格及 cleanup。 | PASS | Pending |

RED 记录：`preview-natural-20260909-red.xml` 有 11 项有效行为失败，另两项 CR/CM 首先碰到测试浮点精度比较，不能算有效 RED；修正后 `preview-natural-20260909-red-verified.xml` 的 CR/CM 两项和 MainCafe 悬浮一项均按预期 FAIL。中间 `focused-green.xml` 为 15/16（一个测试的 Color 往返精度比较），不是 GREEN；随后精确对照原 MPB 读回值，最终 301 全部通过。没有弱化 occupancy、材质或操作约束。

本轮仅三个 Preview View、五个既有测试文件和既有文档有相关修改。两份 Prefab、Theme、MainCafe、GraphicsSettings、QualitySettings 与两份旧 Wall projection 材质共 **8 项 hash 不变**；开始时已 dirty 的 Invalid material 原样保留，没有恢复到 HEAD。全工作区 diff-check 的两处 trailing whitespace 属于这份既有 dirty 材质，不是本轮新增。旧 813/235、15/84 均保留为历史，不冒充此次 full Phase / EditMode / Player / Android/iOS 验证。

真实 Unity 近景图（临时平移/放大 Camera 与 ScreenSpaceCamera，结束恢复，不保存 Scene；640×480，不是手机手感验收）：

- [普通家具原色与 footprint](../outputs/preview-natural-20260909/furniture-footprint.png)
- [相框悬浮 Preview](../outputs/preview-natural-20260909/wall-painting-preview.png)
- [长搁板悬浮 Preview](../outputs/preview-natural-20260909/wall-shelf-preview.png)

人工检查（全部 **Pending**）：在当前 P8 worktree 打开 MainCafe，Play → Decoration Mode。普通家具与 CR/CM 分别试有效→重叠/地面 invalid→有效，确认本体不变色、footprint 仍变红绿且非法不可 Confirm；相框/搁板/Window 在左右墙拖动，观察 20 cm 是否自然、光是否能看懂，Confirm 只贴回、不上下跳；再 Move→Cancel，原位置恢复。最后退出，检查没有旧 ghost/投影残留。8.5 的 UX2-M-006 中模型颜色/墙饰悬浮以本节为准，其他 manual 继续保留。

技术结果不代替 Owner 视觉偏好；M6 incompatible Slot 旧未覆盖子项仍单独 Pending，不自动 Phase closeout、Phase 8R 或 Git 操作。

根代理与独立 Engineering / Technical Art reviewer 已核对三个 View、五个测试文件、最终 XML 和上述三张图；本轮未发现 Critical / Important / Minor。原色、离墙关系与完整模型底部可辨认，通知没有遮住模型；上方投影余量仍是实际占格，不声称它已贴合模型轮廓。交回 **Ready for manual review**，Owner 视觉偏好仍 Pending；没有 commit / push / merge。

## 9. P1–8 UI 素材盘点与专项设计交接（2026-09-09）

Owner 要求完整 review 现有 UI，重新设计截至 Phase 1–8 的 UI 素材，并明确要求保存本清单、创建独立设计 chat。本节是已完成的只读盘点与新设计 brief；不是已批准的新视觉方案，不表示 UI 素材已制作、游戏代码可自动修改或 Phase 已关闭。

### 9.1 当前来源与协作边界

- 当前实装来源：`E:/Unity/Project/AnimalCafe/.worktrees/phase-8-functional-furniture`，branch `codex/phase-8-functional-furniture`。本节及近期 UI 修复尚在该工作区，不能假设新 chat 的默认 main checkout 已包含它们。
- 盘点依据：该工作区的 Assets/UI、MainCafe、UI/Decoration/Feedback runtime、builders、P5–P8 guides/specs，以及已有真实截图。Art 素材清点、UX flow 清点与根代理交叉核对已完成；本轮没有重新启动 Unity、运行测试或做手机视觉验收。
- 新 chat 专门处理 P1–8 UI 素材设计。先读取本节、8.8、Project Design 和现有截图；对 P8 原工作区保持只读，未经单独批准不修改 C#、Prefab、Scene、Shader、ProjectSettings 或正式素材。
- 正式平台为 Android/iOS Touch；Windows 是 Unity 开发环境。Project Profile 中旧 Windows-first 信息以当前 Project Design 为准。
- 从新的视觉方向开始，不自动复用已撤回的 Paper & Timber Figma 方案。用中文、保留 English 技术词；每次只让 Owner 决定一个设计问题。
- 保留 icon 操作偏好。原色模型、墙饰 Preview 外浮20 cm和红绿半透明 footprint 作为当前参照，不借 UI redesign 静默改变 placement/domain。
- 不自动 commit/push/merge、删除旧资产、切换 Phase 或改写旧 manual PASS。新视觉偏好与素材接受需 Owner 查看后确认。

### 9.2 现有素材库存

Assets/UI 排除 .meta 后共 **87 个文件**：P5 40、P6 8、P7 26、P8 13。这是物理文件数，不是独立组件数。

| 素材 | 当前情况 | 重设计处理 |
|---|---|---|
| Prefab | 29 个；P5 19、P6 3、P7 3、P8 4 | 按功能组合重设计，不按旧 Phase 重复制作。 |
| Thumbnail | 25 张；22 张正式选项（含 None），另3张旧墙饰 placeholder | 正式内容可保留，统一构图/背景/比例；placeholder 不属于正式22项。 |
| 其他 PNG | 4 张：Panel、Card、Preview outline、Rotate icon | 尚不是完整 icon library。 |
| Button | Primary/Secondary/Destructive × Default/Pressed/Disabled，共9个样板 prefab | 三种用途、三种状态，不是九种动作。 |
| Panel | Solid、Light Frost、Strong Frost；另有通用 rounded sprite .asset | 新视觉不必照搬旧外观；保留组件功能。 |
| Typography | 一种 Noto Sans SC OTF、三个 TMP SDF 字库 | 统一字体层级，验证中文/英文/数字/符号覆盖。 |
| Material | 7个：3个Panel、Footprint/Pick-up各1、Anchor Debug 2 | 区分2D UI、场景反馈与开发 debug。 |

旧 Prefab 仍有 builder/test/Scene 依赖：P5 Pressed 是 validation sample，P6 是部分 P7 builder 的 clone 来源，P7 仍有引用。不能仅因版本旧就删除。没有发现专用 UI SpriteAtlas、SVG 或 UI animation clip；动画并非因此不存在，当前有 runtime transition。

### 9.3 十个界面/功能区及全部主要按钮

这些区域可以共享一套 UI Kit，并不要求十张不同的 Panel 背景图。

| 功能区 | 信息与状态 | 按钮/入口 |
|---|---|---|
| 1 主画面 HUD | 当前模式、速度；装修期间时间控制锁定 | 装修/退出装修、Pause/Resume、1x、2x。 |
| 2 装修导航 | Furniture/Floor/Wall/Wall Decor；选中/未选中；跨 Tab 先取消未确认 Preview，弹窗期间不可穿透 | 四个Tab；每次进入默认Furniture。详见第 26 节。 |
| 3 Catalogue | 分类行、卡片；Expanded/Compact Preview/Tabs Only/Hidden；session内滚动位置记忆 | 物品卡片、展开/收起、返回编辑、继续添加、独立Pick-up Point入口。 |
| 4 Floor工具区 | Whole Room/Single Grid、已修改N格、操作说明 | 范围切换、Rotate、Undo Last、Apply All、Cancel、Confirm。Whole Room下Rotate/Undo/Apply All当前禁用。 |
| 5 Wall饰面区 | 选一整面墙；Wallpaper/Paint作为Base，Wainscoting/None叠加；同一Preview | 样式卡片、Cancel、Confirm；没有Rotate/Apply All/Store。 |
| 6 浮动Action Bar | 跟随当前物件；新建/已有、有效/无效；icon tooltip | Confirm、Cancel、Rotate、Store，按下表显示。 |
| 7 Preview Notice | 当前对象、尚未确认、有效或具体阻挡原因；Catalogue展开后移入面板 | 通常无需关闭按钮；物件编辑可带“返回编辑”。 |
| 8 布局检查Panel | 未发布时空；已确认布局可营业/有warning/暂不能营业；摘要/详情 | 查看详情/收起详情，长文滚动。 |
| 9 Store Modal | 对象名称、收起影响和阻挡原因；不同对象复用模板 | Store、Cancel。 |
| 10 Exit Modal | 当前未确认修改提示 | Continue、Discard；只有两项，没有Confirm and Exit。 |

| 编辑对象 | 新放置 | 已有物件 |
|---|---|---|
| 普通家具、Cash Register、Coffee Machine | Cancel、Rotate、Confirm | 增加Store。 |
| Wall Decor、Window、Pick-up Point | Cancel、Confirm | 增加Store；没有Rotate。 |

Move 是直接选中拖动，不是额外按钮。Wainscoting 的 None 是目录卡片，不是另一个清除按钮。当前物件 Confirm 后目录保持收起；Cancel/完成Store后展开；Floor/Wall工具仍采用展开的连续编辑流程。

Preview Notice 描述当前未确认物件；Readiness描述已确认的整体布局，两者不可混为一条状态。Confirm 不等于 Save，“可以营业”只表示布局条件满足，不是已存在的开店操作。

### 9.4 共用 UI Kit 制作清单

| 组件 | 必须覆盖 |
|---|---|
| 文字Button | Primary/Secondary/Destructive；正常/按下/禁用。 |
| Icon Button | 同一套底形与不同icon；正常/按下/禁用，不依赖Hover才能理解关键操作。 |
| Tab/范围选择 | 未选中/已选中；跨 Tab 自动取消 Preview，modal 期间不可切换；范围选择保留现有规则。 |
| Catalogue Card | 物件thumbnail+名称、纯材质swatch；已使用勾选与Preview边框须区分；None状态。当前surface卡不显示名称/尺寸/数量，新增信息需另行确认。 |
| Panel/Bottom Sheet | 背景、标题/内容/底部操作区、展开/收起入口。 |
| Modal | 遮罩、标题、正文、两按钮布局；Store与Exit共享骨架。 |
| Feedback | Preview Notice、Readiness摘要/详情、Info/Warning/Error/Success。 |
| Tooltip/Toast | 图标说明和短暂通知。MainCafe action icon有自有tooltip；通用TooltipView/Toast另有foundation实现，不混算独立页面。 |
| 辅助图形 | Scrollbar、分隔线、选中边框、状态标记、展开/收起箭头。 |

还需统一色板、字体/字号层级、间距、圆角、阴影和按压/展开反馈。状态可以由颜色/缩放/描边实现，不要求每个状态一张PNG。

Icon用途：物件操作（Confirm/Cancel/Rotate/Store）；导航（装修入口、四类Tab、Catalogue、展开/收起/返回）；Floor（单格/全屋、Undo/Apply All）；时间（Pause/Resume/加速，1x/2x可保留数字）；反馈（已应用/Warning/Error/Info/None）。同一icon跨区域复用；这份用途清单不是新增动作或已定稿的造型。

正式22个缩略图：Counter四尺寸+CR+CM共6；Floor3；Paint3；Wallpaper2；Wainscoting含None共3；Wall Decor3（Monitor/Shiba Painting/Wood Shelf）；Window2。Pick-up入口不是第23张现有thumbnail。Work Table虽然存在于production content definitions，不在当前可选的6项Furniture catalogue中。

### 9.5 Review 发现与设计时注意事项

1. 图标未成套：除Rotate独立PNG外，不少动作使用✓/×/□字符，Store方框含义不够直观。保留icon操作但统一可辨认造型。
2. 样式来源分散：已有Theme，但部分ActionBar/反馈颜色、字号和布局在runtime里写定。未来实现不能只换Theme；先确认视觉，再逐处迁移，不在本轮改代码。
3. 中英文混用：中文Notice/部分Floor文案与英文Tab/Cancel/Store/Exit并存。统一文案和字体，不把制作图标理解为所有说明都删掉。
4. 动作语义：Store是从当前布局收起，不是出售；Discard仅取消当前未确认Preview后退出，已经Confirm的结果不整批回滚。P7 Guide第74行“恢复进入Decoration Mode前状态”比现代码语义更宽，应在后续批准的文档修订中澄清，不能据此改变runtime行为。
5. Thumbnail规格/构图不统一（64²、256²、1254²）：统一相机角度、物体占图比例、背景、光照、名称区域即可，不必重做家具模型。
6. 信息层级：Catalogue、编辑Notice、Readiness与actions需要清楚主次，避免重复强调；不能为了简洁丢失invalid原因或confirmed/preview区别。
7. 触控：当前UI reference为portrait1080×1920、至少48×48 logical pixels触控区；保留SafeArea和landscape可用。640×480技术截图不能代替真实手机验收。色彩不是唯一反馈，保留symbol/文字；保留Reduced Motion能力。

场景反馈另列：Grid、选中/目标高亮、红绿半透明footprint、白色Pick-up indicator；它们不是普通Panel贴图。Employee/Customer anchor markers目前是开发debug，不自动加进玩家UI。

### 9.6 不包含与新 chat 的起步

本次只设计已实现P1–8所需素材，不自动加入订单、菜单定价、库存、收入统计、员工/顾客详情、招聘/关系、商店购买/货币/IAP、Save/Load、完整Settings或完整Tutorial。它们是未来功能，不是当前UI缺漏。

建议顺序：先确定视觉方向 → Button/Panel/Card基础样式与状态 → 主HUD与装修Panel组合 → 补Modal/反馈/icons/22项thumbnail规范 → Owner逐步看图确认 → 另行批准Unity集成与回归。当前仅授权记录与建立设计chat，未批准具体画风、全面资产生成或游戏集成。

新chat的第一步应简短确认已读本节，只问一个最有用的视觉方向问题；不要重跑整套UI盘点、一次抛出大量问题、自动生成全套素材或擅自恢复旧Figma方案。若其默认checkout来自main，先只读本节顶部的P8绝对路径，不能以main的旧内容替代当前UI证据。

参考：本Guide8.8三张最新Preview图；`outputs/footprint-signal-20260909/ux-20260909-expanded-catalogue.png`、`readiness-details.png`、`floor-scope.png`。以上均相对P8来源工作区，图片代表当前界面参考，不是新设计稿。

## 10. 新 UI 素材接入 — 第 1 步：只导入 Assets（2026-09-11）

### 10.1 本次结果与边界

Owner 已确认新素材制作完成，并批准本步资源导入。本节记录第 1 步完成时：**154 张正式 PNG 已导入 Unity**，Sprite / Border / 透明 / 缩放设置已验证，当时尚未接线；当前 Furniture 接线结果见第 11 节。

- 实施工作区：E:/Unity/Project/AnimalCafe/.worktrees/phase-8-functional-furniture。
- Branch：codex/phase-8-functional-furniture；不在 main 直接修改，没有 commit / push / merge。
- 原始交付包：E:/Unity/Project/AnimalCafe/UI Asset。README、P8R_UI_Handoff_Review.md、PDF、Manifest、Sources、References、Fonts 和 Localization 全部原样保留。
- 保留批准的 A「温暖绘本」方向，不重新设计或重新生成素材。
- 第 1 步不改 Scene、Prefab、现有 UI、footprint、家具材质、布局规则、输入或业务语义。因此仅完成导入时，进入 Play Mode 仍会看到原 UI。
- 此结果不代表 Phase 8 / P8R 已完成，也不替代原有 Pending manual review。

第 9.6 节是制作新素材之前的历史交接；最新资源准备状态以本节为准。英文运行时、TMP 字库、手机排版和场景接线仍待后续步骤。

### 10.2 文件与资源

| 路径（相对本 P8 worktree） | 用途 |
|---|---|
| Assets/UI/P8R/ | 154 PNG 和 Unity 生成的 .meta；保留交付包的分类与文件名。 |
| Assets/Editor/P8R/ImportManifest.json | 从批准 Manifest 提取的 154 项路径、尺寸、Border、SHA-256；附源 Manifest hash。Editor-only，不作为玩家运行时数据。 |
| Assets/Editor/P8R/P8RUiAssetImporter.cs | 手动选择源包并导入；先完整 preflight；已有资源只检查，不静默覆盖。 |
| Assets/Tests/EditMode/P8R/P8RUiAssetImporterTests.cs | 154 个实际 Sprite 检查及 13 个导入、冲突和重复运行检查。 |
| 本 Guide 第 10 节 | 本步执行记录、测试结果和 beginner 检查方法；不新增重复说明文档。 |

导入分组：Panels 3、Buttons 7、Cards_Tabs 8、Icons 93、Feedback 10、Auxiliary 11、Thumbnails 22，共 154 PNG，文件合计约 3.22 MB（不代表运行时显存占用）。

没有导入 198 张九宫格拆片、4 张 Examples，以及 Sources / References 的 35 张 PNG；PDF 和说明文档也没有放进 Assets。352 张可组合素材中包含拆片，采用完整九宫格图时实际只需 154 张。两个用途不同但像素相同的资源保留各自语义路径，没有按 hash 擅自去重。

### 10.3 已落实的 Unity 导入规则

- Sprite (2D and UI)、Single；Alpha from Input、Alpha is Transparency、sRGB；Bilinear、MipMaps off、Read/Write off。
- Pixels Per Unit = 100，沿用现有 CanvasScaler；不改 Canvas reference 1080×1920。PNG 像素不等于手机 logical pixels 或触控目标尺寸。
- Sprite Mesh = Full Rect；中央 pivot；无压缩；Max Size 2048；本步没有启用 Android / iOS / Standalone 压缩覆盖或 Atlas。
- NPOT Scale = None：27×12 / 12×27 的虚线 tile 保留真实尺寸。3 张 tile 使用 Repeat，其余 Clamp。
- 22 张完整九宫格照录 Border。顺序为 left / bottom / right / top，普通为 64 / 64 / 64 / 64；3 张 Pressed Button 为 64 / 64 / 64 / 76。
- 6 张单轴辅助条照录各自 Border，允许某轴两端 Border 之和等于该轴尺寸。
- 原图不改色、不重新编码、不烘焙文字。页面接线时底板使用 Sliced，icon 保持比例；虚线直边和纸纹按交接规则平铺，不能大幅拉伸整张虚线图。

工具先验证整套路径、类型、尺寸、RGBA8、SHA-256、源文件与目标冲突，全部通过后才复制。已有 PNG 内容不同、孤立 meta、未保存 Importer 设置或已保存设置偏离批准值，都报冲突并停止；不会自动覆盖或“修复”。重复导入相同包只检查现有 PNG，不重写设置 / meta / GUID。

若有意改变导入标准，应先审阅新的标准与迁移范围；不要删除冲突文件绕过保护。磁盘故障或进程中断不属于原子事务，遇到未完成导入应保留现场检查，不自动删除已经复制的文件。

### 10.4 本轮自动化与 Review 记录

本表中的 PASS 是 Unity 自动化技术验证，不是 Owner 视觉接受或手机真机验收。

| Case group | 检查内容 | 结果 |
|---|---|---|
| P8R-UI-I-001 | 154 项存在、可加载为 Sprite，实际 PNG hash / 尺寸 / 透明设置 / GUID 正确；无参考图、拆片和示例混入。 | PASS |
| P8R-UI-I-002 | 22 套完整九宫格、Pressed 特殊 Border、6 个单轴辅助条在导入后保留准确 Border。 | PASS |
| P8R-UI-I-003 | PPU 100、Full Rect、NPOT 不缩放、3 个 Repeat tile、其余 Clamp、无压缩及平台无覆盖。 | PASS |
| P8R-UI-I-004 | 相同包连续重跑两次，PNG / meta / GUID 及 fixture 源目录字节不变；额外 Examples 不被导入。 | PASS |
| P8R-UI-I-005 | 缺源图、损坏源图、越界路径、未知类型、重复路径、错误 Border 和尺寸均拒绝；首目标缺失而末源损坏时仍不创建首目标。 | PASS |
| P8R-UI-I-006 | 目标 PNG 被改动时保留其内容并停止，不覆盖。 | PASS |
| P8R-UI-I-007 | 未保存和已保存的 Importer 修改都被保护，不被重跑重置。 | PASS |
| P8R-UI-I-008 | 现有 Phase8FootprintLightTests 15 项直接回归；本步不修改旧 footprint。 | PASS |

证据（本地 TestResults 目录，不是新的规格文档）：

- 导入前基线：p8r-ui-import-baseline.xml，15/15 PASS，exit 0。
- 初次 RED：p8r-ui-import-red.xml，165 项均因尚未导入的资源前置条件而失败，没有编译失败。
- Review 复现：p8r-ui-import-saved-settings-red.xml，1/1 预期失败，证明重跑曾未拒绝已保存 PPU 的修改。
- 最终文件版本：p8r-ui-import-verified.xml，**182/182 PASS = 167 项 UI 导入验证 + 15 项 footprint 回归**；failed / skipped / inconclusive 均 0，exit 0。表中部分 Case group 复用同一组 Sprite 参数测试，不应再次累加。
- 首次 Unity authoring：p8r-ui-import-authoring.log，P8R_UI_IMPORT_OK count=154，exit 0。
- 文件范围审计：p8r-ui-import-audit.json。开始时的 2,005 个 Assets / ProjectSettings / Packages 文件全部原样保留；新增 346 个文件均在 Assets/UI/P8R、Assets/Editor/P8R、Assets/Tests/EditMode/P8R（含 meta）范围内。原始交付包 416 个文件前后 hash 一致，无缺失或新增。

独立代码 Review 中的已保存设置保护、重复 setter 导致 dirty，以及测试临时文件恢复保护均已处理；最终只读复核无未处理 Critical / Important。本步没有运行完整 PlayMode、full EditMode、Player build 或手机实机测试；没有把上述 182 项当成完整 Phase 回归。

### 10.5 Beginner 检查方法

1. Unity Hub 打开本节给出的 P8 worktree，使用 Unity 6000.5.5f1。
2. 在 Project 窗口进入 Assets/UI/P8R，应看到 Panels、Buttons、Cards_Tabs、Icons、Feedback、Auxiliary、Thumbnails 七个分类。
3. 选择 Panels/panel_cream/panel_cream.png，在 Inspector 检查 Texture Type 为 Sprite、Sprite Mode 为 Single、PPU 为 100。
4. 选择 Buttons/button_primary_pressed/button_primary_pressed.png，Sprite Border 应为 L64 / B64 / R64 / T76。
5. 选择 Auxiliary/preview_dash_tile_h.png，尺寸应保持 27×12，Wrap Mode 为 Repeat。
6. 不必重跑导入；如需验证重复操作，可在退出 Play Mode 后选择 AnimalCafe > P8R > Import Approved UI Sprites...，再选原始 UI Asset 目录。相同版本只验证，不重新覆盖已有图。
7. 如需跑自动化，在 Test Runner 的 EditMode 下选择 P8RUiAssetImporterTests。不要把“看到了新 PNG”理解为游戏里的 UI 已换好。

### 10.6 当时的下一步（现已批准，执行状态见第 11 节）

下一步建议先做一条可实际体验的路径：Furniture Catalogue → 物件 Preview → 跟随物件的 icon 按钮 → Confirm / Cancel / Put Away，再逐步复用到其他模式。

根据 Owner 本次要求，后续在适合的环节继续使用 **game-ui-design**（视觉层级、状态、可读性、素材组合）和 **game-ui-ux**（响应式布局、SafeArea、Touch 输入与界面状态）。本步只采用其导入、九宫格与缩放约束，尚未实施新排版；项目仍以 Android / iOS Touch-first 为准，不因通用 skill 引入未批准的 controller 功能。

接线前需要单独确定改动范围：补全英文提示 key 和 TMP ASCII 字库、保留具体错误原因；不改 Confirm / Discard / Readiness 语义；保留 48×48 logical pixels 的重要触控区和场景操作空间。页面组合、手机适配、截图和 Owner manual review 留在后续批准步骤。

## 11. 新 UI 素材接入 — 第 2 步：Furniture 路径（2026-09-11）

### 11.1 已批准的范围

已按 Owner 批准范围完成 Furniture Tab → Preview → Confirm / Cancel / Put Away 的接入；主线程复跑与独立 Engineering / UI 技术复核通过，停在 **Ready for manual review**。这里只标记本步实现与自动化完成，不标记 Owner 接受或 Phase 8 完成。

- 使用第 10 节已导入的 A「温暖绘本」PNG，不重画或覆盖原始交付包。
- 新建 P8R Catalogue、ActionBar、PutAwayModal Prefab 副本和展示目录；原 P6 / P8 Prefab、FurnitureDefinition 和全局 Theme 保留。
- 只将 MainCafe 的相关 UI 实例及引用接到新版本；为旧 Phase8 authoring 增加保护，避免静默换回旧 UI。
- 浮动操作保留 icon-only；英文用于物品名、tooltip、提示与弹窗。内部 action 仍叫 Store，Put Away 只是玩家看见的名称。
- 保留 Confirm / Cancel / Put Away 的原有 transaction、scroll / Touch 边界、设备 Slot 规则及 Readiness 计算。共享 ActionBar 的 Floor / Wall footer 按钮统一使用新底板状态，但原工具、文字、布局与动作不变；这些页面及 Readiness 的全量换肤留后续。
- 不改已经确认的红绿灯式半透明 footprint、家具原材质和墙饰 preview 行为。

字体核对更新：交接包里提到的缺字是旧字体检查；当前实际使用的 Phase8 SDF 已覆盖 95/95 printable ASCII 和交付英文所需字符，因此本步复用现有字体，不重新生成或替换全局字体。

### 11.2 本步文件

| 路径（相对本 P8 worktree） | 用途 |
|---|---|
| Assets/UI/P8R/Prefabs/PF_UI_P8RCatalogue.prefab | 新 Catalogue 壳体、Tab 和卡片；保留原来的目录交互。 |
| Assets/UI/P8R/Prefabs/PF_UI_P8RActionBar.prefab | 跟随 Preview 的 icon 按钮与提示。 |
| Assets/UI/P8R/Prefabs/PF_UI_P8RPutAwayModal.prefab | Put Away 确认弹窗，不改变内部 Store transaction。 |
| Assets/UI/P8R/DC_P8RFurniture.asset | 六项家具 / 设备的展示目录，继续引用原 FurnitureDefinition。 |
| Assets/UI/P8R/P8RAppearance.asset、P8REnglish.json | 新 PNG 外观引用与本路径英文文案。 |
| Assets/Scripts/UI/P8R/P8RAppearance.cs | 在现有 View 中使用的局部外观与文案 helper。 |
| Assets/Editor/P8R/P8RFurnitureUiBuilder.cs | 定向生成 / 接线工具；先检查引用和 Prefab 结构，验证替换后的 Scene；失败时用 Undo rollback 恢复，不保存半套接线。 |
| Assets/Scenes/MainCafe.unity | 本步接入目标；原 UI Prefab 文件仍保留。 |
| Assets/Editor/Phase8/Phase8SceneSetup.cs、Phase8Validator.cs | 识别完整 P8R 接线；部分接线时明确停止，避免旧工具静默覆盖新 UI。 |
| Assets/Tests/EditMode/P8R/、Assets/Tests/PlayMode/EditorSceneLoading/P8RFurnitureFlowTests.cs、P8RActionGeometryTests.cs | 导入 / 接线保护、真实 Scene 流程与布局回归；仍沿用 P6 / P7 / P8 相关旧测试。 |

现有 Catalogue / Tile / Tabs / ActionBar / StoreModal View 加入可选新外观；未配置 P8R 的旧实例继续原行为。DecorationModeController 的本步改动限于本路径文案与呈现，以及弹窗关闭后的一次定位刷新，不改变 Store / placement transaction。

### 11.3 验证与 manual review 边界

本步使用 **game-ui-design** 检查素材组合、正常 / 按下 / 禁用状态、文字层级与可读性；使用 **game-ui-ux** 检查 SafeArea、响应式排版、Touch ownership 和弹窗关闭后的呈现状态。项目保持 Android / iOS Touch-first，没有增加 controller 功能。

以下 PASS 表示相应技术检查已完成，不是 Owner manual 接受。Case group 会复用参数化测试，不能把每行再相加。

| Case group | 检查内容 | 技术结果 |
|---|---|---|
| P8R-UI-F-001 | 154 PNG 导入契约继续通过；3 个新 Prefab、6 项展示目录及准确引用有效；旧字体覆盖英文。 | PASS |
| P8R-UI-F-002 | 正常 / 按下 / 禁用 Sprite 状态正确；底板无旧绿色 tint；浮动操作 icon-only。 | PASS |
| P8R-UI-F-003 | 无效 Confirm 不提交；有效 Confirm 连点也只提交一次。 | PASS |
| P8R-UI-F-004 | 已有家具 Move / Rotate 后 Cancel 恢复确认位置与方向；Put Away Cancel 保留同一逻辑 Preview。 | PASS |
| P8R-UI-F-005 | 空 Counter Put Away 只移除一次；带设备的 Counter 不被删除，保留 Cash Register (1) 等具体 blocker 内容。 | PASS |
| P8R-UI-F-006 | Catalogue 滚动 / Preview 连续性、Add Another、Back to Editing；共享模式切换、设备 / Pick-up 动作差异及现有 readiness 相关回归。 | PASS |
| P8R-UI-F-007 | 四种 synthetic 横竖屏尺寸下 action 至少 48 Canvas logical units、可接收点击且互不重叠；Tab / Pickup / Modal 边界、文字容纳与 SafeArea 正确。 | PASS |
| P8R-UI-F-008 | 真实 MainCafe 的 Modal Cancel 与被阻挡 Put Away 关闭后，在没有新输入的 idle 状态下，action 不盖住任何 Tab 或 Show Catalogue。 | PASS |
| P8R-UI-F-009 | 完整接线重复运行不改 Scene bytes；缺引用 / 部分接线拒绝；替换验证失败 rollback；原 unloaded scene-list 状态保留。 | PASS |
| P8R-UI-F-010 | 本轮开始前保存的 42 个原 UI 文件 SHA256，最终复跑后 42/42 相同。 | PASS |

主线程最终证据（本 worktree 的 TestResults 目录）：

- `p8r-furniture-root-verified-edit.xml`：**187/187 PASS = 167 导入检查 + 20 接线 / 呈现 / 保护检查**。
- `p8r-furniture-root-verified-play.xml`：**135/135 PASS**。范围：P8RFurnitureFlowTests、P8RActionGeometryTests、Phase8MainCafeSceneTests、Phase8DecorationCataloguePlayModeTests、Phase7DecorationUiPlayModeTests、Phase6DecorationUiPlayModeTests。
- 两次最终命令均 exit 0；failed / skipped / inconclusive 全部 0。接线前基线为 167 EditMode / 19 PlayMode，不重复累加。
- 新增关闭后位置断言先在 `p8r-post-close-red.xml` 复现 2 个失败，再修复通过；保留原事务与 blocker 断言，没有靠缩小按钮或测试中主动刷新位置来隐藏问题。

独立 Engineering review 无遗留 Critical / Important / Minor；独立 UI 技术复核确认最后的按钮互盖已解决。此次没有运行 full Phase 1–8 suite、Player build 或 Android / iOS 真机测试；本轮 focused PASS 不替代以前的 Phase 证据或 Owner 待办。

现在可以开始下一节 manual review。卡片识别、图标含义、最终色彩和真实设备的字号 / Touch 舒适度仍需 Owner 检查；synthetic SafeArea / RenderTexture 不等于真机验收。

### 11.4 Beginner 人工检查步骤

1. 在 Unity Hub 打开 `E:/Unity/Project/AnimalCafe/.worktrees/phase-8-functional-furniture`，使用 Unity 6000.5.5f1；不要打开 main 工作区来检查这一步。
2. 打开 `Assets/Scenes/MainCafe.unity`，进入 Play Mode，然后进入装修模式、选择 Furniture。无需重新 Import 或运行旧 Phase8 Build Assets。
3. 查看四种 Counter、Cash Register、Coffee Machine 的新卡片与英文名称；横向 / 纵向拖动目录，确认不会误放家具或同时滚动两个方向。
4. 选择 Counter，将 Preview 移到可放置及冲突位置。检查绿色 / 红色半透明 footprint、具体英文原因，以及无效时不可点的 Confirm；浮动按钮仍应是 icon。
5. 在有效位置 Confirm，应只增加一件。选择已有家具，移动和 Rotate 后点 Cancel，应恢复确认前的位置与方向。
6. 对空 Counter 点 Put Away：先 Cancel，应回到同一件家具的 Preview；再次打开并确认，应只收起这一件。对带设备 / Pick-up 的 Counter，检查阻挡说明中保留具体内容和数量，不应连带删除。
7. Preview 中展开目录，再点 Back to Editing，应回到原 Preview、保留当前问题提示；Confirm 后可用 Add Another 开始下一件。
8. 切换 Floor、Wall、Wall Decor，再回 Furniture。检查原工具仍工作；Cash Register / Coffee Machine 仍只能使用合适 Slot，Pick-up 仍没有 Rotate。
9. 人工分别检查竖屏、横屏和带 SafeArea 的设备：文字能读、按钮好点、弹窗不裁切、目录与浮动操作不会过度遮住场景。Synthetic 测试中的 48 Canvas logical units 不是手机的 48 dp；真实 DPI / Touch 舒适度仍需真机确认。

以上项目的 Owner 接受状态均为 **Pending**；自动化通过不能替代你实际看图、操作后的决定。不要因本步完成而关闭原有 Phase 8 manual 待办。

### 11.5 Unity 截图

截图保存在 `outputs/p8r-furniture-ui-20260911/`：

| 文件 | 状态 |
|---|---|
| [01-catalogue.png](../outputs/p8r-furniture-ui-20260911/01-catalogue.png) | Furniture 目录和新卡片。 |
| [02-invalid-preview.png](../outputs/p8r-furniture-ui-20260911/02-invalid-preview.png) | 无效 Preview、具体原因与禁用 Confirm。 |
| [03-valid-preview.png](../outputs/p8r-furniture-ui-20260911/03-valid-preview.png) | 有效 Preview 与 icon 操作。 |
| [04-confirmed.png](../outputs/p8r-furniture-ui-20260911/04-confirmed.png) | Confirm 后状态。 |
| [05-put-away-modal.png](../outputs/p8r-furniture-ui-20260911/05-put-away-modal.png) | 居中的 Put Away 确认弹窗。 |
| [06-blocked-counter.png](../outputs/p8r-furniture-ui-20260911/06-blocked-counter.png) | 带内容 Counter 的收起阻挡。 |
| [07-return-to-editing.png](../outputs/p8r-furniture-ui-20260911/07-return-to-editing.png) | 展开目录后保留原因与 Back to Editing。 |

这组图来自实际 MainCafe、640×480 Editor GameView。Batch 截图临时用同尺寸 Camera 渲染 UI，随后恢复；因此可检查布局与状态，但不作为原生 Overlay 最终色彩、手机画质或 Owner 审美接受的证据。原 PNG 未为截图改色。1080×1920、1920×1080、720×1600、1600×720 的 injected SafeArea 几何检查另行记录，不冒充这些分辨率的完整游戏截图。

## 12. 新 UI 素材接入 — 第 3 步：全部现有玩家 UI（2026-09-11）

### 12.1 范围与状态

Owner 已批准补齐第 9.3 节全部十个现有界面／功能区，并要求自动化及截图自检后交回人工 review。本节是本轮记录入口；实现、定向修复、最终完整回归、最新截图自检与测试后资产核对均已完成，当前 **Ready for manual review**。这里停止继续开发，等待 Owner 视觉与操作接受；不自动关闭 Phase 8。

沿用第 10 节 A「温暖绘本」资源及第 11 节家具接入，补齐 HUD、Floor / Wall / Wall Decor / Window、Readiness 和 Exit Modal。仅修改 UI presentation 与必要接线；不新增 P9 功能、不改 Confirm / Cancel / Put Away / Discard 的 transaction，不改模型、footprint、Save 或 Readiness 计算。旧 Prefab、字体、全局 Theme 与 Phase7 domain definitions 保留。

本轮继续使用 **game-ui-design** 检查信息层级、素材组合和非纯颜色状态，使用 **game-ui-ux** 检查 SafeArea、Touch、动态布局和界面切换。正式平台仍为 Android / iOS Touch-first；通用 Tooltip / Toast 的 P5 gallery 样板不是 MainCafe 新增功能。

### 12.2 自检 case groups

PASS 只表示相应技术检查已完成，不等于 Owner 接受。每组可能复用同一测试，不能将这些行与套件数量相加。

| Case group | 检查内容 | 技术结果 |
|---|---|---|
| P8R-UI-A-001 | 全部 22 个目录缩略图、分类、四个 Tab、展开／收起／返回编辑及 Pick-up 入口。 | PASS |
| P8R-UI-A-002 | Floor 全屋／单格范围、选中状态、Rotate / Undo / Apply All / Cancel / Confirm 原规则和提示。 | PASS |
| P8R-UI-A-003 | Wall base + wainscoting / None 的双层 applied / preview 标记；材质卡保持 image-only。 | PASS |
| P8R-UI-A-004 | Wall Decor / Window 的具体名称、目录标记、有效／无效 Preview、Confirm / Cancel / Put Away；没有新增 Rotate。 | PASS |
| P8R-UI-A-005 | HUD Pause / Resume / 1x / 2x 正常、选中、锁定状态；退出装修精确恢复进入前速度。 | PASS |
| P8R-UI-A-006 | Readiness ready / warning / blocked、摘要／详情／长文滚动；保留全部原因及位置，不显示 raw IDs；Preview 不覆盖 confirmed report。 | PASS |
| P8R-UI-A-007 | Exit Continue 保留同一个 Preview；Discard Preview 只取消未确认部分；无 Preview 直接退出；Modal pointer ownership 保留至 release。 | PASS |
| P8R-UI-A-008 | 家具、CR / CM / Pick-up、浮动 icon、Tooltip、Preview Notice 与 Put Away 的既有行为回归；关闭弹窗后不遮挡 Tab。 | PASS |
| P8R-UI-A-009 | 横竖屏与 SafeArea 下文字、按钮、Panel、长文容纳和相互遮挡；真实 MainCafe 截图自检。 | PASS（Editor／synthetic 范围，非真机） |
| P8R-UI-A-010 | 定向 authoring 重跑／部分接线保护；旧 UI / domain / 模型 / 项目设置不被意外改写；完整回归结果逐套核对。 | PASS |

接线前本轮基线：`TestResults/p8r-all-ui-baseline-edit.xml` **187/187 PASS**；`p8r-all-ui-baseline-play.xml` **135/135 PASS**。这是修改前基线，不是本节完成证据。

最终验证记录（各套件互有覆盖，不相加）：

- `TestResults/p8r-all-ui-root-full-play-verified.xml`：最终冻结版本的主线程无 filter 完整 **PlayMode 826/826 PASS**，failed / skipped / inconclusive 均为 0，命令 exit 0。26 张本节截图和第 11.5 节 7 张家具截图均由这次运行刷新。
- `TestResults/p8r-all-ui-root-full-edit-verified.xml`：最终冻结版本的主线程无 filter 完整 **EditMode 1949/1949 PASS**，failed / skipped / inconclusive 均为 0，命令 exit 0，耗时约 **30.8 分钟**。
- `TestResults/p8r-ui-settle-focused-green4.xml`：最终定向 **48/48 PASS**，含首次／重复跨帧加载的 clean 状态与 exact bytes、重复接线、late-failure rollback（含 prefab instance override 身份）、旧工具保护及 scene／Selection 恢复。`p8r-ui-settle-cold-green.xml` 的独立冷加载 **1/1 PASS** 没有预热或 ClearDirty；这些 case 也已进入上述最终完整回归。
- 完整 PlayMode 仍记录 **2,293 条 legacy fixture 中文缺字警告**，已逐条对应到 21 个 testcase：旧 Phase6 prefab，或动态创建且未绑定 P8R 的 TMP 测试视图。当前 P8R MainCafe 测试路径未记录缺字警告；这不是整个 Console 零警告，也不表示旧中文 fixture 的字形显示已修复。
- 历史回归：`p8r-complete-post-regression-edit.xml` 曾定向 **32/32 PASS**，但随后无 filter 的 `p8r-all-ui-root-full-edit-final.xml` 仍有 **1934 项中 1840 PASS / 94 FAIL**（约 26.6 分钟）。因此重新修复旧迁移 fixture、legacy authoring 保护、scene/material isolation 和 PlayMode test assembly 的 Editor 引用；新增隔离／保护 case 后，最终完整 **1949/1949 PASS**。保留失败记录，不将历史定向通过冒充当时全量通过。
- 完整回归曾找出 Floor compact 的真实行间重叠；修正后保留原高度限制。旧测试的颜色／中文字断言改为实际 P8R sprite、PNG alpha、icon 与英文文案，保留 transaction、计数、坐标和 Readiness 检查。Mouse fixture 改为 Began 后按真实 UI 排除区域选拖动终点，并等待目录动画完成；四种 footprint 的真实 Mouse 输入、至少 0.9 world-unit 移动、release cleanup 与 Cancel 均保留，Input 规则未改。
- 早期完整 EditMode 因旧资源导入超过 1200 秒而超时，没有 XML，不计作通过。测试遗留的 32 个 asset 已从精确匹配 SHA256 的本轮前快照／既有备份恢复，测试写出的版本另留备份，未用 HEAD 覆盖 Owner 工作。下一轮前另存全部 **2383 个 Assets / ProjectSettings 文件**的逐字节 v2 快照；PlayMode 后 **0 改变、0 新增**。上述完整 EditMode 留下的 **37 个测试副作用文件**也已从 v2 精确恢复并保留副作用备份，恢复后 **2383 文件比对 0 改变、0 新增**；后续修复和回归另建新版快照。
- 最终回归前保存 **v7：2389 个 Assets / ProjectSettings 文件**的已验证快照。最终 PlayMode 后 0 改变／0 新增；最终 EditMode 留下的 **38 个旧资源测试副作用**已先保存到本轮内部 `full-edit-verified-side-effects/` 备份，再从 v7 精确恢复。恢复后 **2389 文件比对 0 改变／0 新增**，没有使用 HEAD 覆盖工作。直接核对当前工作目录：修改前 **1400 个受保护文件**完全一致；全部 **154 张 UI PNG**与修改前逐字节一致。ProjectSettings 无 Git diff，branch／HEAD 未变。
- UI 运行及后续 authoring 修复均已完成独立 Engineering review，无未处理 Critical／Important。最终 PlayMode 刷新后的本节 **26 张**和第 11.5 节 **7 张**截图已全部完成独立 Technical Art review，结果 PASS；主线程另抽查 Floor compact、长名称、无效 Preview、HUD 锁定、退出弹窗、Readiness 详情及 applied／preview 标记，没有发现阻挡性的裁切、重叠或状态矛盾。最终完整回归与资产核对均已通过，交接 **Ready for manual review**；审美接受仍由 Owner 决定。

### 12.3 晚上人工 review 的最短路径

1. Unity Hub 打开 `E:/Unity/Project/AnimalCafe/.worktrees/phase-8-functional-furniture`，使用 Unity 6000.5.5f1；打开 `Assets/Scenes/MainCafe.unity` 后 Play。不用重跑 Import / Build Assets，也不要在 main checkout 找本轮 UI。
2. 先在主画面切换 1x、2x、Pause；分别进入／退出装修，检查恢复原来的速度。观察按钮状态、图标大小和可读性。
3. 依次看 Furniture、Floor、Walls、Wall Decor 四页。家具／设备看图与名称；地面／墙面看材质图，确认没有额外价格／数量／名称。地板试全屋和单格；墙面试 base、护墙板和 None。
4. 各选一件家具、墙饰或 Window，试有效／无效位置，再 Confirm / Cancel。检查原色模型、红绿半透明光、具体提示、禁用 Confirm，以及 icon 是否好认、按钮是否遮住物件或目录。
5. 点 Put Away，先 Cancel 再确认；带设备的 Counter 应保留并说明阻挡内容。带 Preview 退出时，Continue Editing 应回到同一个 Preview；Discard Preview 只丢当前预览，已经 Confirm 的修改应保留。
6. Confirm 一次布局修改后检查 Readiness 摘要，展开详情并滚动；再拖动一个未确认 Preview，顶部报告不应跟着改变。最后看横屏／竖屏的文字裁切、Panel 遮挡与操作舒适度。

Owner 视觉与操作接受、原生 Overlay 最终色彩、Android / iOS 真机 DPI / SafeArea / Touch 均保持 **Pending**。历史 Phase 8 manual 待办继续保留；本轮自动化不代替它们，也不自动 commit / push / merge 或关闭 Phase。

### 12.4 本轮修改的主要 files

| Files | 用途 |
|---|---|
| `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`、`DecorationCatalogueTileView.cs`、`DecorationFloorRangeView.cs` | 补齐四类目录、22 张缩略图、分类／名称、范围选中状态、applied／preview 标记及动态布局。 |
| `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`、`DecorationStoreModalView.cs`、`DecorationExitModalView.cs` | 统一动作／禁用外观、Put Away 和退出确认弹窗；保留原事件与 transaction。 |
| `Assets/Scripts/UI/TimeControlPanel.cs`、`Assets/Scripts/UI/Feedback/ValidationMessageView.cs` | HUD 速度／暂停／装修锁定，以及 Readiness 摘要、具体原因和详情滚动。 |
| `Assets/Scripts/UI/P8R/P8RAppearance.cs`、`Assets/UI/P8R/P8REnglish.json` | 局部 Sprite 状态及英文文案；停止旧 ColorTint tween，避免 PNG 被额外染色。 |
| `Assets/Scripts/Decoration/DecorationModeController.cs` | 从真实 confirmed／preview 状态更新目录标记，提供对应模式的英文提示；不改变摆放和 readiness 规则。 |
| `Assets/Editor/P8R/P8RCompleteUiBuilder.cs`、`P8RFurnitureUiBuilder.cs` | 定向接线、重跑检查、缺引用保护和精确回滚；保存前完成目标 TMP／layout 初始化，包括 ActionBar 源 Prefab，避免下一帧才补写 UI 属性。 |
| `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs`、`Assets/Editor/Phase7/Phase7DecorationSceneSetup.cs`、`Phase7SurfaceAssetBuilder.cs`、`Assets/Editor/Phase8/Phase8FeedbackAssets.cs`、`Phase8SceneSetup.cs` | 让旧 authoring 识别完整 P8R；遇到部分接线明确停止，不静默换回旧 UI。 |
| `Assets/UI/P8R/Prefabs/`、`Assets/Scenes/MainCafe.unity` | 更新 P8R Catalogue／ActionBar／PutAway，新增 P8R Exit Modal，接入 MainCafe 的 HUD／Readiness。旧 Prefab 不删除。 |
| `Assets/Tests/EditMode/P8R/P8RCompleteUiTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/P8RCompleteFlowTests.cs` | 新外观／接线保护、真实场景流程、名称容纳、长文 resize、截图和实际渲染颜色断言。 |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationMainCafeSceneTests.cs`、`Phase6DecorationRealTouchTests.cs`、`Phase7MainCafeSceneTests.cs`、`Phase8MainCafeSceneTests.cs` | 迁移旧呈现断言并修正真实 Mouse fixture 的布局等待；保留 legacy 分支、原交易／坐标／Readiness 及输入清理断言。 |
| `Assets/Editor/Phase6/Phase6DecorationValidator.cs`、`Assets/Tests/EditMode/P8R/P8RLegacyGuardTests.cs` | 在旧工具接受完整 P8R 接线之前，检查原有 scene infrastructure、runtime 和 feedback 结构；损坏时明确拒绝，不跳过原 preflight。 |
| `Assets/Tests/EditMode/LegacyMainCafeFixture.cs`、`LegacyMainCafeFixtureTests.cs`，以及 Phase5–8 migration tests | 为旧迁移测试独立生成 legacy scene；测试后恢复 MainCafe 原始 bytes、scene entry 和 Selection，保护无关未保存内容；保留对应隔离回归。 |
| `Assets/Editor/Phase0SceneSetup.cs`、`Assets/Tests/EditMode/Phase5/Phase5MainCafeMigrationTests.cs` | 提取现有 target scene 配置 core 供测试使用，避免测试通过旧公共入口打开 Single scene 并全局保存；公共旧流程本身没有改变，也不声称已安全化。 |
| `Assets/Tests/PlayMode/Phase8FootprintLightPlayModeTests.cs` | 使用已有 reflection 方式读取测试 material，移除 PlayMode core test assembly 的直接 UnityEditor dependency；footprint 外观和原断言不变。 |
| `Assets/Tests/EditMode/Phase8/Phase8FootprintLightTests.cs` | 只读实际 authored serialized 颜色／参数，避免 Unity 的 Material getter 惰性同步污染共享材质；保留真实 HDR／LDR 渲染、透明度、状态色以及源 dirty／JSON／bytes 不变断言。 |
| 本 Guide 第 12 节、`Docs/Phase7_Beginner_Guide.md` | 保存本轮结果与人工步骤；纠正旧 Guide 中 Discard 会撤销全部装修修改的误导说明。 |

### 12.5 截图索引与证据边界

本轮截图目录：`outputs/p8r-all-ui-20260911/`。家具专用的 7 张图仍保存在第 11.5 节，不删除历史证据。

| 截图 | 用途 |
|---|---|
| [01 HUD 1x](../outputs/p8r-all-ui-20260911/01-hud-1x.png)／[02 2x](../outputs/p8r-all-ui-20260911/02-hud-2x.png)／[03 Paused](../outputs/p8r-all-ui-20260911/03-hud-paused.png)／[04 装修锁定](../outputs/p8r-all-ui-20260911/04-hud-decoration-lock.png) | HUD 状态与原速度恢复流程。 |
| [05 Exit Preview](../outputs/p8r-all-ui-20260911/05-exit-preview-modal.png) | Continue Editing／Discard Preview 双动作弹窗。 |
| [06 Floor 目录](../outputs/p8r-all-ui-20260911/06-floor-catalogue-whole-room.png)／[07 Whole Room Preview](../outputs/p8r-all-ui-20260911/07-floor-preview-apply-all-undo.png)／[08 Single Grid](../outputs/p8r-all-ui-20260911/08-floor-single-grid-preview.png) | 范围、卡片、底部动作与正常／禁用状态。UI 中 Apply 对应内部 Confirm，不是 Save。 |
| [09 Wall base + wainscoting](../outputs/p8r-all-ui-20260911/09-wall-base-and-wains-preview.png)／[10 None](../outputs/p8r-all-ui-20260911/10-wall-neutral-none-preview.png) | 已滚到护墙板区域，绿色 applied 与橙色 preview 可同时表达；None 是中性选项。截图只证明 UI 标记，材质 transaction 另由流程断言验证。 |
| [11 Wall Decor／Window 目录](../outputs/p8r-all-ui-20260911/11-wall-decor-window-catalogue.png)／[12 墙饰 Preview](../outputs/p8r-all-ui-20260911/12-wall-decor-preview.png)／[13 applied + preview](../outputs/p8r-all-ui-20260911/13-wall-decor-applied-and-preview.png)／[14 Window Preview](../outputs/p8r-all-ui-20260911/14-window-preview.png) | 缩略图、名称、标记与 icon 操作。 |
| [15 confirmed Readiness](../outputs/p8r-all-ui-20260911/15-readiness-real-confirmed-blocked.png)／[16 invalid Preview](../outputs/p8r-all-ui-20260911/16-invalid-preview-retains-confirmed-readiness.png) | 报告不随无效 Preview 改变。两图相同是预期；必须结合实际无效移动和相同 report 引用的流程断言，不能仅凭两张静态图证明。 |
| [17 Ready](../outputs/p8r-all-ui-20260911/17-readiness-INJECTED-ready.png)／[18 Warning](../outputs/p8r-all-ui-20260911/18-readiness-INJECTED-warning.png)／[19 Blocked](../outputs/p8r-all-ui-20260911/19-readiness-INJECTED-blocked-collapsed.png)／[20 详情顶部](../outputs/p8r-all-ui-20260911/20-readiness-INJECTED-expanded-scroll.png)／[21 详情底部](../outputs/p8r-all-ui-20260911/21-readiness-INJECTED-scroll-bottom.png) | **INJECTED presentation stress cases**：给同一个 View 注入已构造报告，检查各状态和长列表，不冒充真实布局产生了这些结果。 |
| [22 Wall Monitor Put Away](../outputs/p8r-all-ui-20260911/22-wall-decor-put-away.png)／[23 Tall Glass Window Put Away](../outputs/p8r-all-ui-20260911/23-window-put-away.png) | 具体对象名称、正文和取消／确认按钮。 |
| [24 Window 名称特写](../outputs/p8r-all-ui-20260911/24-window-name-fit.png)／[25 Coffee Machine 名称特写](../outputs/p8r-all-ui-20260911/25-coffee-machine-name-fit.png) | 滚到对应目录底部，完整查看两行名称；捕获当帧同时检查文字容纳及 viewport 内可见性。上方其他卡片被滚出视窗是正常滚动，不是用裁切画面代验目标名称。 |
| [26 Floor 收起状态](../outputs/p8r-all-ui-20260911/26-floor-compact.png) | 底部 action、范围、Show Catalogue 与 Tab 分行排列，不互相遮挡；保持更多场景空间可见。 |

这些图来自实际 MainCafe、640×480 Editor GameView。Batch capture 临时把 Overlay UI 改用同尺寸 ScreenSpaceCamera，并关闭该 Camera 的 post-processing，最后恢复。**它们用于布局／状态自检；世界颜色与 production 不同，不代表原生 Overlay 最终颜色、手机 DPI 或完整真机体验。** 原 PNG 没有为截图改色。横竖屏与 SafeArea 几何测试是独立的 synthetic 检查，不是这组图的实际分辨率。

目录内的 `00-AB-ui-overlay-camera.png` 是被否决的 Camera-stack 技术试验：未渲染出 UI，**不计入通过的截图 gallery**。保留它仅作为诊断记录。

## 13. Owner review 小修：居中与 icon 比例（2026-09-11）

### 13.1 范围与验收

Owner 已批准以下修正。继续使用 game-ui-design 和 game-ui-ux；保留 154 张原 PNG、温暖绘本风格、原色模型、红绿光 footprint 和所有摆放／确认／取消规则。不扩大成第二次 UI redesign。

| Case | 玩家看到／操作到的结果 | 技术状态 |
|---|---|---|
| P8R-UI-P-001 | 展开的 Catalogue 底部 Pick-up Point 相对 Panel 居中；icon + label 作为一组居中，横竖屏及重新展开后不偏移。 | PASS（技术） |
| P8R-UI-P-002 | 家具／墙饰旁 Confirm、Cancel、Rotate、Put Away 的可见外框缩小；保留足够点击范围，动作、禁用状态和指针释放不变。 | PASS（技术） |
| P8R-UI-P-003 | Tab、HUD、Pick-up、目录入口、Floor scope 和状态 icon 更易辨认；考虑 PNG 透明留白，文字不被挤压，已有 applied badge 不盲目放大。 | PASS（技术） |
| P8R-UI-P-004 | 接线重跑、冷加载、回滚及直接相关操作回归通过；旧资源、PNG、模型及项目设置未意外改变。 | PASS（技术） |
| P8R-UI-P-005 | 新版本实际 MainCafe 截图完成独立技术检查；Owner 仍需亲自判断视觉比例与操作舒适度。 | PASS（技术）；Owner Pending |

上一轮自动化只检查了部分范围／文字容纳，没有捕捉到 Pick-up 真正居中和 icon 可见笔画比例；这次补充对应检查。第 12 节历史 PASS 不代表这些问题当时已被覆盖。

### 13.2 人工检查的最短路径

1. 从第 12.3 节同一个 P8 worktree 打开 MainCafe，Play → Decorate；查看 Pick-up 入口和四个 Tab。
2. 选择一件家具、一件墙饰，各试 Confirm／Cancel，再选择已放置物件试 Put Away；确认小外框仍容易点中，灰色禁用 Confirm 不执行。
3. 切换 Floor 全屋／单格，收起并重开 Catalogue；检查 icon 清楚、文字完整、没有重叠或跳位。
4. 退出装修检查 HUD 速度／暂停及 Readiness，再切换横竖 GameView。实际 Android／iOS DPI、SafeArea 与手指操作另由真机验收，不以 Editor 截图替代。

### 13.3 最终技术证据

- `TestResults/p8r-proportion-edit-green.xml`：定向 **56/56 PASS**，含隐藏／无 Canvas 的 Prefab 文本、首次及重复加载的 dirty=false＋exact bytes、重复接线、late rollback，以及可见 Face／实际 hit Image 缺失时拒绝写入。
- `TestResults/p8r-proportion-play-green.xml`：定向 **6/6 PASS**，含真实 MainCafe Pick-up＋图文居中、实际 PNG alpha 可见尺寸、外框／点击区分离与真实射线边缘点击、禁用不执行、Exit 重开与静态帧无额外 TMP 重排。另以实际 Prefab 验证 1080×1920、1920×1080、720×1600、1600×720 的 synthetic Canvas 与 injected SafeArea；不冒充真机测试。
- `TestResults/p8r-proportion-root-full-play-verified.xml`：主线程最终无 filter 完整 **831/831 PASS**，111.77 秒，命令 exit 0；failed／skipped／inconclusive 均为 0。上述定向套件互相重叠，不将数量相加。本次没有重跑 30.8 分钟的完整 EditMode；第 12 节 1949/1949 是旧版历史记录。
- TDD 保留可信失败：最初三项比例检查 **0/3**；新增 Exit 长文案与重复重排检查 **0/2**。同时修复隐藏 Prefab 的 TMP 无效边界、过时 Tab 文字偏移及自算坐标的微小浮点不稳定；不放宽冷加载的逐字节断言。
- 首次完整 PlayMode **830/831**：唯一失败是旧 Phase 7 检查仍把可见底图当作点击区。改为检查实际 root hit Image，保留真实 raycast、安全区和非重叠断言，随后完整 **831/831**。第一次未开启 capture 开关，不计作截图证据；33 张新图均来自后一次通过运行。
- 新浮动 face 为原 hit 边长的 **72%**，只缩小可见外框；当前 640×480 下原有至少 48 screen-pixel hit 范围保留。真实手机物理尺寸／dp 仍需手动检查。小图标使用原 PNG 的 alpha≥16 可见边界补偿，主要目标为 36–40 Canvas logical units 的可见长边；不修改 PNG，不在 runtime 读取 texture。
- 独立 Engineering review 已关闭本轮 findings，无未处理 Critical／Important。最终冻结快照含 **2391 个 Assets／ProjectSettings 文件**；完整 Play 后 **0 改变／0 新增**。**154 张原 UI PNG 逐字节未改**，ProjectSettings 无 diff，P8 branch／HEAD 未变。
- 完整 Play 仍有 **2293 条 legacy 中文缺字警告，来自 21 个旧 fixture case**；当前 P8R／MainCafe／RealTouch 路径 0 个同类 case。它不代表整个 Console 零警告。

### 13.4 本轮主要 files

| Files | 用途 |
|---|---|
| `P8RButtonLayout.cs`、`P8RAppearance.cs` | 局部图文居中、透明留白补偿、隐藏文本边界保护及小外框布局；不建立新全局 Theme。 |
| `DecorationCatalogueView.cs`、`DecorationModeTabsView.cs`、`DecorationActionBarView.cs` | Pick-up 中心锚点、Tab 重排时机，以及可见 Face 与实际 hit region 分离。 |
| `TimeControlPanel.cs`、`ValidationMessageView.cs`、`DecorationStoreModalView.cs`、`DecorationExitModalView.cs` | 最终字号／状态／文案确定后重排；弹窗真正打开时仍完整、居中。 |
| `Assets/Editor/P8R/P8RFurnitureUiBuilder.cs`、`P8RCompleteUiBuilder.cs` | 修正目标控件接线、旧文字偏移与稳定的生成结果。 |
| `Assets/UI/P8R/Prefabs/` 四个 P8R Prefab、`Assets/Scenes/MainCafe.unity` | 通过 Unity Editor API 保存新布局；旧 UI Prefab 与模型保留。 |
| P8R Edit／Play tests、`Phase7MainCafeSceneTests.cs` | 补充上述实际比例／冷加载／输入检查，迁移旧可见图层＝点击区的测试假设。 |
| 本 Guide 第 13 节、Roadmap | 保存本轮结果与人工验收入口；旧失败／旧截图不删除。 |

### 13.5 新截图入口

当时使用 `outputs/p8r-ui-proportion-20260911/`，内含 `all-ui/` 26 张与 `furniture/` 7 张。**后续第 15 节实施期间，这些 PNG 被回归截图开关误覆盖；下面的说明保留为历史记录，但当前链接已不能证明当时的外观。原测试 XML／评审记录保留，截图完整性说明见 15.6。**

| 截图 | 重点 |
|---|---|
| [Pick-up／HUD／Tabs](../outputs/p8r-ui-proportion-20260911/all-ui/04-hud-decoration-lock.png) | Pick-up 相对 Catalogue Panel 居中，图标与文字整体居中，小 icon 放大。 |
| [家具有效 Preview](../outputs/p8r-ui-proportion-20260911/furniture/03-valid-preview.png)／[无效 Preview](../outputs/p8r-ui-proportion-20260911/furniture/02-invalid-preview.png) | 缩小的浮动外框与禁用 Confirm。 |
| [墙饰 Preview](../outputs/p8r-ui-proportion-20260911/all-ui/12-wall-decor-preview.png)／[Window Preview](../outputs/p8r-ui-proportion-20260911/all-ui/14-window-preview.png) | 两键操作与物件的比例。 |
| [Floor 展开](../outputs/p8r-ui-proportion-20260911/all-ui/06-floor-catalogue-whole-room.png)／[Single Grid](../outputs/p8r-ui-proportion-20260911/all-ui/08-floor-single-grid-preview.png)／[Floor 收起](../outputs/p8r-ui-proportion-20260911/all-ui/26-floor-compact.png) | scope icon、文字容纳与分行布局。 |
| [Put Away](../outputs/p8r-ui-proportion-20260911/furniture/05-put-away-modal.png)／[Window Put Away](../outputs/p8r-ui-proportion-20260911/all-ui/23-window-put-away.png)／[Exit](../outputs/p8r-ui-proportion-20260911/all-ui/05-exit-preview-modal.png) | 放大图标、长文案与居中；Exit 保持纯文字按钮。 |
| [Readiness Ready](../outputs/p8r-ui-proportion-20260911/all-ui/17-readiness-INJECTED-ready.png)／[Warning](../outputs/p8r-ui-proportion-20260911/all-ui/18-readiness-INJECTED-warning.png)／[Blocked](../outputs/p8r-ui-proportion-20260911/all-ui/19-readiness-INJECTED-blocked-collapsed.png) | 状态 icon 可见尺寸；这些是明确标注的注入报告展示测试。 |

全部 33 张均是 **实际 MainCafe、640×480** 的新 capture。Batch 临时以 ScreenSpaceCamera 渲染 UI 后恢复；all-ui capture 关闭 Camera post-processing，furniture capture 沿用原方法。因此它们是布局／状态证据，不代表原生 Overlay 最终颜色、完整手机画质或 Android／iOS DPI／Touch 验收。

33 张均已逐张完成独立 Technical Art／QA 检查，Critical 0、Important 0；主线程另直接抽查 7 张关键图。Pick-up 居中、小外框、增大的 icon、长名称与弹窗文案均无阻挡性裁切／重叠。保留一项非阻挡的既有视觉备注：半透明弹窗遮罩下，背景 Preview notice 的末词有时仍在弹窗底边外可见；不影响当前弹窗内容和交互，本次不扩大范围修改，留给 Owner 判断。该次交接为 **Ready for manual review**；随后 Owner 发现跨模式错位，修复记录见第 14 节。Owner 接受及真机项目保持 Pending，不自动关闭 Phase 8。

## 14. 浮动按钮错位与近旧版尺寸修复（2026-09-11）

### 14.1 改动与原因

Owner 截图中的 ×／✓ 偏离底板，是 Floor／Wall 的文字按钮切回家具／墙饰 icon 按钮时，保留了文字布局的左偏移。旧测试只检查显隐与 Sprite，没有检查这个连续切换后的 icon 位置。

- 对照改 UI 前的 commit `4c878bc`：原浮动按钮为 **48×48 Canvas logical units**。现在可见底板为 **56×56**，只增加约 **16.7%**；它不再随透明点击范围一起变大。此决定取代第 13 节的「Face 为 hit 边长 72%」。
- ×／✓／Rotate／Store 按 PNG 实际可见墨迹重新居中，墨迹最长边为 32 logical units。透明点击区域保持原逻辑，按钮之间的点击边界不改变。
- 修改 `DecorationActionBarView.cs`、`P8RButtonLayout.cs`，并更新 `P8RCompleteFlowTests.cs`、`P8RActionGeometryTests.cs`。未改 PNG、模型、footprint、Prefab／Scene、gameplay 或 Save。

### 14.2 已完成的技术测试

| Case | 检查内容 | 结果 |
|---|---|---|
| P8R-UI-A001 | 同一 MainCafe 内 Floor → 新建／已有 Shelf → Furniture，再 Wall → 同样流程；所有可见 icon 回到 Face 中心、墨迹不超出底板 | PASS |
| P8R-UI-A002 | 返回 Floor／Wall 时 icon 隐藏，Cancel／Apply 文字与完整 footer 底板恢复 | PASS |
| P8R-UI-A003 | 1080×1920、1920×1080、720×1600、1600×720 synthetic Canvas；近旧版 Face 尺寸、四键不重叠且保留 Safe Area | PASS |
| P8R-UI-A004 | 点击 Face 外、透明 hit 内边缘，真实 GraphicRaycaster 命中 Rotate；一次点击一次动作，disabled 不执行 | PASS |
| P8R-UI-A005 | UI 绑定、冷启动、重复构建、失败 rollback 和 legacy guard 的相关 EditMode regression | PASS，56/56 |
| P8R-UI-A006 | 八张本轮实际 MainCafe 截图独立检查：新建／已有 Shelf、Furniture、Floor／Wall footer | PASS，8/8；Owner 视觉接受 Pending |

RED：`TestResults/p8r-action-alignment-red.xml` 两项失败，证明测试能抓到原问题：取消 icon 偏左约 12.5 screen pixels，Face 达到约 89.8 logical units。修复后 `p8r-action-alignment-green.xml` **7/7 PASS**（10.45 秒，含上述跨模式与比例测试），`p8r-action-alignment-edit.xml` **56/56 PASS**（9.88 秒）。

最终完整 PlayMode：`TestResults/p8r-action-alignment-full-play.xml` **832/832 PASS**（98.30 秒），failed／skipped／inconclusive 均为 0，CLI exit 0。完整回归关闭截图开关，以保留旧截图；第 14.3 节新图来自前面的 focused capture。测试后 2391 个 Assets／ProjectSettings 文件相对测试前保护快照 **0 改变／0 新增**。仅两个生产布局 source 和两个测试 source 属于本次 Assets 修改，154 张 PNG 及 Scene／Prefab 未改；不重跑历史 30 分钟完整 EditMode。

### 14.3 截图与手动检查

当时的新截图在 `outputs/p8r-action-alignment-20260911/`。**第 15 节实施期间，此目录 8 张 PNG 同样被误覆盖；下面链接不再是本节原始截图，不应与历史 832/832 结果拼接成当时的视觉证据。详见 15.6。**

- [Wall → Shelf：×／✓ 与底板居中](../outputs/p8r-action-alignment-20260911/alignment-Wall-to-shelf.png)
- [已有 Shelf：Store／Cancel／Confirm](../outputs/p8r-action-alignment-20260911/alignment-Wall-existing-shelf.png)
- [Floor → Furniture：Cancel／Rotate／Confirm](../outputs/p8r-action-alignment-20260911/alignment-Floor-to-furniture.png)
- [返回 Wall 文字 footer](../outputs/p8r-action-alignment-20260911/alignment-Wall-footer.png)

本轮八张是 640×480 实际 MainCafe 布局／状态 capture，临时 ScreenSpaceCamera、关闭 post-processing 后恢复。不是原生 Overlay 最终色彩或 Android／iOS DPI／Touch 验收；没有新增 Pick-up 专属 round-trip 测试，它共用此次修复的方法。

Owner 下一步：打开 MainCafe → Play，先预览 Floor 或 Wall 并取消，再选 Wood Shelf 和 Furniture，检查 ×／✓／Rotate 是否居中、大小是否合意；确认／取消及已有物件 Put Away 都操作一次。状态 **Ready for manual review**；Owner manual acceptance 仍 **Pending**，不继续开发或自动关闭 Phase 8。

## 15. 参考图排版：两步合并交接（2026-09-11）

### 15.1 本轮范围与状态

Owner 批准两步一起完成后再 review；本轮现为 **Ready for manual review**，不是 Owner 已接受或 Phase 8 已关闭。参考图只决定 UI 排版，继续使用现有 Warm Storybook 素材、英文文案与场景。game-ui-design／game-ui-ux 用于信息层级、响应式预留区域和真实点击区检查。以下成绩均来自本轮最终代码，不沿用第 14 节的历史 PASS。

- 家具／墙饰浮动按钮保留 56×56 Canvas logical units 可见外框；间距收紧到约 10 logical units，关闭 hover／focus tooltip。透明点击区不互相重叠，指针 press／release ownership 与 disabled 行为保留。
- 左上为独立模式标签 → 横排时间按钮 → confirmed-layout Readiness；右上只有一个图标在上、文字在下的 Decorate／Exit Decoration。初始 Readiness 显示现有正式布局报告，不写死“可以营业”。
- Readiness 用箭头展开完整原因。详情改变时主动重新安排相关 UI，不要求玩家先移动鼠标；预览中的摆放结果不替代正式布局报告。
- 底部 Catalogue 为标题／收起箭头 → 四等宽 icon-above-text Tab → 分组滚动内容。Furniture 独享满宽、固定的 Pickup Point；Floor／Wall 仍使用各自范围和操作区。
- Expanded、CompactPreview、TabsOnly 均保留分类入口。横屏根据 Readiness 实际高度分配目录空间，并检查 Preview 说明不能挡住当前家具和 footprint。

### 15.2 修改文件与用途

| File／文件组 | 本轮用途 |
|---|---|
| `P8RButtonLayout.cs`、`DecorationActionBarView.cs`、`DecorationPointerBoundaryEventHook.cs` | 图标／文字排版、紧凑浮动组、关闭 tooltip；按住期间保持目标位置，保留原输入 ownership。 |
| `TimeControlPanel.cs`、`ValidationMessageView.cs` | 新 HUD 层级、模式标签、Readiness 紧凑行／箭头／详情滚动。 |
| `DecorationModeController.cs` | 仅 presentation 接线、当前报告展示、Readiness 事件绑定与浮动 UI 避让；不改变布局交易和设备规则。 |
| `DecorationCatalogueView.cs`、`DecorationModeTabsView.cs` | 目录内部排版、三种 sheet 状态、等宽分类、固定 footer 和可用内容高度。 |
| `P8RCompleteUiBuilder.cs`、`P8RFurnitureUiBuilder.cs` | 使用 Unity Editor APIs 同步目标 UI；限定刷新范围，保留既有保护与冷加载稳定性。 |
| `Assets/UI/P8R/P8REnglish.json`、目标 P8R Prefab、`Assets/Scenes/MainCafe.unity` | 少量英文显示文字和 UI 引用／几何；不改原始 PNG、模型、footprint 或 gameplay。 |
| `P8RReferenceLayoutTests.cs`、相关 P8R tests、两类 Phase6 MainCafe／RealTouch tests | 实际 MainCafe 对象、真实输入与横竖屏、截图、测试间输入状态隔离，以及重复 authoring 保护回归。 |

### 15.3 本轮技术测试清单

以下 14 项均已完成技术核验；**技术 PASS 不等于 Owner manual acceptance**。真实 Mouse／Touch 测试、布局与 authoring 测试，以及最终截图共同提供证据。

| ID | Test case | 状态 |
|---|---|---|
| P8R-REF-001 | 56 logical 可见按钮、8–12 logical 间距、点击区不重叠；跨模式无 tooltip，真实 raycast 与 disabled suppression 正确。 | 技术 PASS |
| P8R-REF-002 | 左上三层 HUD、横向时间按钮；装修锁定暂停，退出恢复进入前速度。 | 技术 PASS |
| P8R-REF-003 | 右上唯一模式按钮始终图标在上／文字在下，长英文有底部留白，不被通用按钮刷新覆盖。 | 技术 PASS |
| P8R-REF-004 | 初始及 Confirm 后显示真实 confirmed-layout 报告；48 logical disclosure 箭头可展开／收起完整诊断并滚动到末尾。 | 技术 PASS |
| P8R-REF-005 | 不移动鼠标也会在 Readiness 展开／收起后重新避让；active rebind／disable 不遗留旧事件订阅。 | 技术 PASS |
| P8R-REF-006 | 标题、四等宽 Tab、viewport、固定 Pickup 分区有序；Pickup 仅 Furniture 显示且不属于滚动内容。 | 技术 PASS |
| P8R-REF-007 | 四模式 × 三种 sheet 状态中分类始终可触达，Floor／Wall footer 不被 Pickup 或目录盖住。 | 技术 PASS |
| P8R-REF-008 | 分组横／纵拖动、浏览记忆、继续添加和已有物件恢复流程保留；Whole Room 的 Undo 仍按原规则禁用。 | 技术 PASS |
| P8R-REF-009 | 真实 portrait／landscape 和小横屏的 HUD／目录有可用空间；Safe Area 几何检查与真机接受分开记录。 | 技术 PASS |
| P8R-REF-010 | 横屏 Preview notice 不挡住当前家具／footprint；完整原因可读，竖屏不退化。 | 技术 PASS |
| P8R-REF-011 | Exit／Put Away 文案、输入遮挡、取消及恢复原 Preview 正常。 | 技术 PASS |
| P8R-REF-012 | 限定 authoring 保留 Exit 实例／overrides、旧版保护、重复生成与 cold-load byte stability；最终测试后核对 Assets／ProjectSettings。 | 技术 PASS |
| P8R-REF-013 | 真实 Touch 在目录动画期间按下／释放浮动按钮，不因延迟重排跳位或漏点击；UI→世界拖动保留原 ownership，取消／多指／停用不遗留输入。 | 技术 PASS |
| P8R-REF-014 | Reference 场景测试退出时释放该场景的 UI InputAction 运行时缓存，随后真实 Mouse／Touch 测试仍能操作；不改变 serialized bindings 或依赖测试顺序。 | 技术 PASS |

最终证据如下；各测试集有重叠，**不能相加作为独立用例总数**。

| 验证 | 结果 | XML |
|---|---:|---|
| 定向 EditMode：P8R authoring／cold-load／rollback／legacy guard | 57/57 PASS | [Edit](../TestResults/p8r-reference-layout-remediation-edit-final.xml) |
| 扩大 PlayMode 组合：P8R、Phase6 Mouse／Touch、Phase7／8 接线 | 210/210 PASS | [Focused Play](../TestResults/p8r-reference-layout-remediation-focused-play-final.xml) |
| Root 独立无 filter 完整 Editor PlayMode | 849/849 PASS | [Full Play](../TestResults/p8r-reference-layout-root-full-play-final.xml) |
| 最终专用 Reference 截图／布局流程 | 13/13 PASS | [Gallery](../TestResults/p8r-reference-layout-touch-final-gallery.xml) |

以上均为 0 failed／skipped／inconclusive。完整 PlayMode 用时 117.80 秒；对应 logs 保留在 `Logs/`。本轮没有重新运行完整 EditMode 或 standalone／Android／iOS；57 项是定向 EditMode，不将历史完整成绩当作本轮成绩。

首轮完整回归发现的 7 项问题已闭合：两项旧 HUD 断言按新排版迁移；修复目录动画结束后按钮在按下时跳位；两项世界触摸 fixture 保留真实起手／raycast／ownership 检查并使用实际可见区域。新增松手、取消、多指、设备移除、事件重新绑定和测试间 InputAction 缓存清理覆盖。module/device purge 的新测试仅验证显示锁，未改变原有 pointer ownership 策略；fixture 清理不当作 production ownership 验收。

独立 code/spec 与 screenshot review 均无未解决的阻挡级技术 finding。完整测试前后 `Assets/`＋`ProjectSettings/` 的 **2,393 文件** hash 核对为 0 changed／0 added；154 张原始 UI PNG 与开工快照一致。既有 Owner 工作保留，无 commit／push／merge。

### 15.4 本轮截图入口

最终代码已重拍实际 MainCafe：portrait **1080×1920**、landscape **1600×720**，共 **34 张 PNG＋2 份尺寸记录**；不是把横屏图片拉成长图。重拍前的 36 文件已逐 hash 备份。30 张 PNG 与上一轮一致；4 张浮动按钮／modal 图变化，已独立复核，无新增遮挡、裁切或布局退化。下表链接均指向本轮最终图库。

| 场景 | Portrait | Landscape |
|---|---|---|
| 正常 HUD | [01](../outputs/p8r-reference-layout-20260911/portrait/01-normal-hud.png) | [01](../outputs/p8r-reference-layout-20260911/landscape/01-normal-hud.png) |
| Furniture 展开／固定 Pickup | [02](../outputs/p8r-reference-layout-20260911/portrait/02-furniture-expanded-pickup.png) | [02](../outputs/p8r-reference-layout-20260911/landscape/02-furniture-expanded-pickup.png) |
| Furniture 浮动按钮 | [03](../outputs/p8r-reference-layout-20260911/portrait/03-furniture-floating.png) | [03](../outputs/p8r-reference-layout-20260911/landscape/03-furniture-floating.png) |
| Exit Preview modal | [04](../outputs/p8r-reference-layout-20260911/portrait/04-exit-preview-modal.png) | [04](../outputs/p8r-reference-layout-20260911/landscape/04-exit-preview-modal.png) |
| Floor 展开 | [05](../outputs/p8r-reference-layout-20260911/portrait/05-floor-expanded.png) | [05](../outputs/p8r-reference-layout-20260911/landscape/05-floor-expanded.png) |
| Floor compact | [06](../outputs/p8r-reference-layout-20260911/portrait/06-floor-compact.png) | [06](../outputs/p8r-reference-layout-20260911/landscape/06-floor-compact.png) |
| Wall 展开 | [07a](../outputs/p8r-reference-layout-20260911/portrait/07a-wall-expanded.png) | [07a](../outputs/p8r-reference-layout-20260911/landscape/07a-wall-expanded.png) |
| Wall compact | [08](../outputs/p8r-reference-layout-20260911/portrait/08-wall-compact.png) | [08](../outputs/p8r-reference-layout-20260911/landscape/08-wall-compact.png) |
| 新 Shelf | [09](../outputs/p8r-reference-layout-20260911/portrait/09-shelf-new.png) | [09](../outputs/p8r-reference-layout-20260911/landscape/09-shelf-new.png) |
| 已有 Shelf | [10](../outputs/p8r-reference-layout-20260911/portrait/10-shelf-existing.png) | [10](../outputs/p8r-reference-layout-20260911/landscape/10-shelf-existing.png) |
| Put Away modal | [11](../outputs/p8r-reference-layout-20260911/portrait/11-shelf-put-away-modal.png) | [11](../outputs/p8r-reference-layout-20260911/landscape/11-shelf-put-away-modal.png) |
| Modal 取消恢复 | [12](../outputs/p8r-reference-layout-20260911/portrait/12-shelf-modal-recovery.png) | [12](../outputs/p8r-reference-layout-20260911/landscape/12-shelf-modal-recovery.png) |
| TabsOnly | [13](../outputs/p8r-reference-layout-20260911/portrait/13-tabs-only.png) | [13](../outputs/p8r-reference-layout-20260911/landscape/13-tabs-only.png) |
| 真实 confirmed Readiness | [14](../outputs/p8r-reference-layout-20260911/portrait/14-real-confirmed-readiness.png) | [14](../outputs/p8r-reference-layout-20260911/landscape/14-real-confirmed-readiness.png) |
| INJECTED 长诊断展开 | [15](../outputs/p8r-reference-layout-20260911/portrait/15-INJECTED-long-readiness-expanded.png) | [15](../outputs/p8r-reference-layout-20260911/landscape/15-INJECTED-long-readiness-expanded.png) |
| INJECTED 长诊断末尾 | [16](../outputs/p8r-reference-layout-20260911/portrait/16-INJECTED-long-readiness-bottom.png) | [16](../outputs/p8r-reference-layout-20260911/landscape/16-INJECTED-long-readiness-bottom.png) |
| 横屏 Preview 移到左侧／右侧，notice 反侧避让 | 不适用 | [左侧物件](../outputs/p8r-reference-layout-20260911/landscape/17-preview-moved-1.png)／[右侧物件](../outputs/p8r-reference-layout-20260911/landscape/17-preview-moved-8.png) |

**证据边界：**截图临时把 Overlay UI 改为 ScreenSpaceCamera，并关闭 camera post-processing，完成后恢复；只用于布局／状态检查，世界颜色不代表 production 原生 Overlay。`INJECTED` 两项为明确注入的长报告，用于检查滚动，不代表该场景实际有这些失败。Editor 实际分辨率不等于 Android／iOS DPI、触感、Safe Area 或设备性能验收。

### 15.5 Owner 一次性 manual review

最终 Ready 后，从本 P8 worktree 打开 `Assets/Scenes/MainCafe.unity` → Play 即可，不需要运行旧版 Build／Configure 菜单。

1. 看左上模式、时间、Readiness 和右上装修入口；选 2x → Decorate → Exit，确认速度恢复与新排版是否舒服。
2. 查看四类 Tab，以及 Furniture 分组／固定 Pickup；横向拖物品行、纵向拖分类，再收起／重开目录。
3. 家具与 Wood Shelf 各预览一次，检查按钮大小、间距、无 tooltip、红绿 footprint；完成 Confirm／Cancel，已有 Shelf 再打开 Put Away 并取消。
4. 检查 Floor／Wall 的展开与 compact，展开 Readiness 并滚动；切换 portrait／landscape，确认提示不挡操作或当前物件。

Owner 的视觉偏好和实际操作接受仍为 **Pending**；技术 PASS 不自动关闭 Phase 8，也不进入后续 gameplay。不 commit／push／merge。

### 15.6 历史截图完整性说明

本轮一次综合 PlayMode 回归误开通用截图开关，旧 screenshot helper 写回了历史输出路径。这是执行失误，已告知 Owner，并停止该轮 Unity runner。受影响的是 **45 张生成的测试截图＋3 份尺寸记录，共 48 文件**，不是 `Assets/UI/P8R` 的 154 张原始 UI 素材。

| 受影响目录 | 文件数 |
|---|---:|
| `outputs/p8r-ui-proportion-20260911/all-ui/` | 26 PNG＋1 metrics |
| `outputs/p8r-ui-proportion-20260911/furniture/` | 7 PNG＋1 metrics |
| `outputs/p8r-action-alignment-20260911/` | 8 PNG＋1 metrics |
| `outputs/phase7-ui-fix/` | 4 PNG |

在当前工作树的 `outputs`、内部保护目录与 Git tracked files 中尚未找到这些版本的可靠备份；没有用更早的不同版本图片冒充原件。原有测试 XML／logs、评审文字与更早的独立 `p8r-all-ui-20260911`／`p8r-furniture-ui-20260911` 图库保留。历史 Owner acceptance 事实不变，但上述当前 PNG 不能再作为原轮截图证据。

本轮最终视觉证据只使用独立的 `outputs/p8r-reference-layout-20260911/`。Reference 专用截图开关与历史通用开关分离，普通回归全部关闭截图输出。最终完整回归后，48 个受影响历史路径文件没有再次改写；最终图库 36 文件也无漂移。这只证明后续保全有效，**不表示已恢复误覆盖前的原件**。中止／RED／混合截图轮不计入最终成绩。

## 16. HUD 图标与 Surface 卡片精修（2026-09-12）

Owner 批准先完成反馈 1–5，再讨论第 6 项质感方向。本节覆盖新的 UI 展示；不改 placement／readiness／时间恢复规则，不替代 Android／iOS 真机和 Owner manual review。

### 16.1 已落地的变化

- 右上 Decorate／Exit 改为纯 icon：可见底板 **88×88 logical units**，图标实际墨迹最长边 **40**；透明点击区 **96×96**，不是把点击范围一起缩成小图标。没有新增 tooltip。
- 清除旧 `Top Highlight`／`TopHighlight`，并只解绑 Decor 实例的 legacy theme。后者即使 component disabled，旧 Awake 仍会涂回根 Image；现在透明触控区保持 alpha=0，圆角外不再露出白色矩形／横线。
- 时间按钮 **72×64**，隐藏文字。Pause／Resume 为 `Ⅱ`／`▶`，1x 为单三角、2x 为双三角；后续等间距与 2x 比例修正见 16.4。Resume 记住之前的 1x／2x；装修期间仍锁定时间。
- Floor／Wall 无名称行，因此预览底框扩展到卡片上下，外留 **12**，图片外留 **20**；图片比底框再内缩 **8**，保持比例不拉伸。Furniture／Wall Decor 的名称空间保留。
- 边缘检查完成，但**没有宣称全部锯齿消失**。原始 PNG／import meta 未改；原生小尺寸画面仍有极细描边的断续／轻微毛糙，属于第 6 项可进一步改善的视觉细节。本轮没有用全局 AA、渲染倍率或模糊掩盖。

154 张 PNG＋154 份 meta 的 SHA256 与开工快照全部一致。实际导入为 Bilinear、Uncompressed、MipMaps off、NPOT None、PPU100、alphaIsTransparency，无启用的平台 override；未发现误用 Point 或压缩的统一性问题。细线缩小采样与源图手绘轮廓可能共同影响残留毛糙，不能当作所有控件的唯一原因；下一步应针对具体控件比较，再决定是否改源图。

### 16.2 回归与证据

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-POLISH-001 | Decor／Exit 纯 icon，88 底板／40 墨迹；透明边缘实际 GraphicRaycaster 命中 | 技术 PASS |
| P8R-POLISH-002 | 重新载入 MainCafe 后旧 highlight 关闭，根 Image alpha=0，底板 targetGraphic 引用仍保留 | 技术 PASS |
| P8R-POLISH-003 | 1x／2x、Pause／Resume、进入／退出装修后 icon 与速度正确；按钮不重叠 | 技术 PASS |
| P8R-POLISH-004 | 原生 Overlay、1080×1920／720×1280／1600×720 检查；细线毛糙如实保留为质感议题 | 检查完成；Owner 视觉接受 Pending |
| P8R-POLISH-005 | Floor→Wall→Furniture→Floor 重绑定，真实非空缩略图四边在底框内且 preserveAspect；家具名称保留 | 技术 PASS |

| 本轮验证 | 结果 | 证据 |
|---|---:|---|
| 定向 EditMode：authoring／cold-load／rollback／legacy guard | **58/58 PASS** | [Edit XML](../TestResults/p8r-polish-20260912-edit-final.xml) |
| 无 filter 完整 Editor PlayMode | **852/852 PASS** | [Full Play XML](../TestResults/p8r-polish-20260912-full-play-final.xml) |
| 正常 Editor 原生截图＋精修断言 | **3/3 PASS** | [Native XML](../TestResults/p8r-polish-20260912-native-verified.xml) |

全部为 0 failed／skipped／inconclusive；测试集合有重叠，不相加。完整 PlayMode 用时 117.04 秒。未跑完整 EditMode、standalone 或 Android／iOS 真机。

本节唯一验收图库为 `outputs/p8r-ui-polish-20260912/native-verified/`：每种分辨率 9 张，共 **27 张 PNG**，已读取 PNG 自身尺寸确认。原生截图保留 production ScreenSpaceOverlay、camera post-processing 和渲染质量；未走旧的 Camera 转换截图 helper。正常 Editor 使用临时内存 GameView 尺寸并恢复选择，不保存用户 presets。早期 `native-probe`／`native-editor-probe`／`native-final` 为诊断尝试，尤其后者切尺寸未真正生效，**不作为最终多分辨率证据**。

| 查看内容 | 1080p 竖屏 | 720p 竖屏 | 横屏 |
|---|---|---|---|
| 新 HUD／Decor | [01](../outputs/p8r-ui-polish-20260912/native-verified/1080x1920/01-hud-1x.png) | [01](../outputs/p8r-ui-polish-20260912/native-verified/720x1280/01-hud-1x.png) | [01](../outputs/p8r-ui-polish-20260912/native-verified/1600x720/01-hud-1x.png) |
| Floor 预览 fit | [05](../outputs/p8r-ui-polish-20260912/native-verified/1080x1920/05-floor.png) | [05](../outputs/p8r-ui-polish-20260912/native-verified/720x1280/05-floor.png) | [05](../outputs/p8r-ui-polish-20260912/native-verified/1600x720/05-floor.png) |
| Wall 预览 fit | [06](../outputs/p8r-ui-polish-20260912/native-verified/1080x1920/06-wall.png) | [06](../outputs/p8r-ui-polish-20260912/native-verified/720x1280/06-wall.png) | [06](../outputs/p8r-ui-polish-20260912/native-verified/1600x720/06-wall.png) |

同目录 02／03 为 2x／暂停，04 为 Furniture，07 为 Shelf，08 为 Exit modal，09 为退出装修后恢复 2x。

独立 code review 与 27 张原生截图复核均无 blocking：HUD、卡片、Shelf 浮动操作和 Exit modal 未见新增遮挡／越界。720p／横屏的细描边、斜线和虚线仍有局部一像素阶梯；滚动窗口边缘露出半张下一卡片属于可继续讨论的整洁度问题，不代表内容越过固定 footer。3D 房间斜边的采样问题与 Overlay UI 分开，不计为本轮 UI 已修复事项。

对开工时 Assets／ProjectSettings 的 2,393 文件快照核对：仅本轮 **12 个预期路径**变化，无新增 Assets；154 张 UI PNG、其 import meta、全局 ProjectSettings 与其余既有资产未变。变化为 7 个 production C#、3 个测试文件、MainCafe 场景和 P8R Catalogue prefab；本指南另行更新。旧版 prefab、模型、footprint 和 domain assets 未改。没有 commit／push／merge。

### 16.3 Owner review 与下一步

从 P8 worktree 打开 MainCafe → Play：先点 2x→暂停→恢复，再进入／退出装修；看右上 icon、四角无白底和点击是否舒服。随后打开 Floor／Wall 检查框内预览，并在 Game View **1x 显示比例**看边缘，避免把缩放显示造成的像素感混作原素材问题。

当前停在 **Ready for manual review**；Owner 接受仍为 Pending。第 6 项整套质感未应用：建议在保留奶油色／可可色风格的前提下，优先统一细描边与圆角、收敛阴影和选中态、调整字体层级及间距。Owner 后续批准的小范围 A/B 样张见 16.4，样张不代表同意替换整套素材。

### 16.4 时间按钮比例修正与质感 A/B 样张（2026-09-12）

Owner 指出三个时间按钮间距不一、2x 偏小，批准先修正，再看精修风格样张。只改 `TimeControlPanel.cs` 的局部展示、`P8RReferenceLayoutTests.cs` 的两个测试及本指南；没有改通用 icon helper、PNG、prefab、scene 或游戏时间规则。

- **间隔统一为 8 logical units**：原位置 0／88／168 改为 0／80／160；每个按钮与点击区仍为 **72×64**。
- **2x 按可见墨迹补偿**：双三角最长边从 32 调到 44，高度约由 20.2 到 27.8；1x 高度仍为 32。保留宽高比、不拉伸，左右各留约 14。补偿在每次状态重新绘制之后应用，因此暂停、恢复、装修锁定和组件重启不会缩回旧尺寸。
- **范围不扩张**：Decor 按钮、Floor／Wall 卡片、所有 154 张原 UI PNG 与 import 设置保持不变。只是此小修正已实装，B 风格尚未实装。

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-TIME-OPTICAL-001 | 三按钮等距，72×64 点击区不变 | 技术 PASS |
| P8R-TIME-OPTICAL-002 | 读取真实 PNG alpha：2x 与 1x 的可见高度比例、中心与内边距；Normal／Fast、Pause／Resume、装修进出、disable→enable 后不退回旧尺寸，原速度恢复 | 技术 PASS |

先确认新测试 **2/2 RED**：实际间距为 16／8，2x 高度仅为 1x 的 0.632；修正后结果如下。普通回归关闭全部历史截图开关，新截图写入独立目录、不覆盖旧证据。

| 本次验证 | 结果 | 证据 |
|---|---:|---|
| 定向 PlayMode：Reference Layout／Complete Flow／TimeControl | **32/32 PASS**，0 failed／skipped／inconclusive | [Focused XML](../TestResults/p8r-time-optical-20260912-focused-final.xml) |
| 正常 Editor 原生 Overlay 截图＋Polish／TimeOptical | **5/5 PASS**，0 failed／skipped／inconclusive | [Native XML](../TestResults/p8r-time-optical-20260912-native-final.xml) |

测试集合有重叠，不相加。本次没有重跑全量 suite；16.2 的 852 项属于上一轮证据，不能充当本次全量结果。新图库共 27 张原生 PNG，保留生产 Overlay／渲染设置，覆盖 1080×1920、720×1280、1600×720；不等于真机 DPI／触感验收。

| 本次截图 | 1080p 竖屏 | 720p 竖屏 | 横屏 |
|---|---|---|---|
| 2x 选中，新间距与图标大小 | [02](../outputs/p8r-ui-polish-20260912/time-optical-20260912/1080x1920/02-hud-2x.png) | [02](../outputs/p8r-ui-polish-20260912/time-optical-20260912/720x1280/02-hud-2x.png) | [02](../outputs/p8r-ui-polish-20260912/time-optical-20260912/1600x720/02-hud-2x.png) |
| 暂停／Resume 图标 | [03](../outputs/p8r-ui-polish-20260912/time-optical-20260912/1080x1920/03-hud-paused.png) | [03](../outputs/p8r-ui-polish-20260912/time-optical-20260912/720x1280/03-hud-paused.png) | [03](../outputs/p8r-ui-polish-20260912/time-optical-20260912/1600x720/03-hud-paused.png) |

同目录 01 是 1x；04–08 为 Furniture／Floor／Wall／Shelf／Exit modal；09 为退出装修后恢复 2x。独立 code review 无 Critical／Important／Minor finding。测试后核对 2,393 文件基线，仅两个预期 C# 文件变化、无新增 Assets；原 PNG、场景、prefab、ProjectSettings 及既有工作保留。没有 commit／push／merge。

**质感提案：**[A/B 样张](../outputs/p8r-ui-polish-20260912/time-optical-20260912/ui-style-ab-proposal.png) 是 AI 生成的组件风格示意，两边都不是 Unity 截图，不作为尺寸／布局／抗锯齿测试证据。A 概括现版的淡描边、纸纹和多层底线，B 示意规则圆角、较清晰的统一描边、淡纸纹、短单层阴影与更明确的字重。为便于比较，差异略作强调；示意中的实心图标、字重、控件排版不代表已批准更换这些细节。

独立视觉复核已查看全部 27 张图，其中三尺寸 01／02／03／09 用原像素 HUD crop 检查：等距、选中态、2x 居中与内边距正常；04–08 无新增遮挡或越界。720p／横屏仍有约 1px 的细描边、双三角斜边和 selection dash 阶梯，未宣称锯齿全部消除。目录滚动边缘的局部卡片裁切与前一轮一致。A/B 仅通过方向表达检查，Owner 选择仍 Pending。

Owner 下一步：MainCafe → Play，以 Game View 1x 查看时间行，点击 2x→暂停→恢复、进入／退出装修；随后只评价 A/B 的质感方向。当前 **Ready for manual review**，时间按钮视觉接受与 B 风格批准均 **Pending**；没有应用整套视觉更新，也没有关闭 Phase 8。

## 17. B 方案统一精修（2026-09-12）

Owner 后续批准把 B 应用到当前 UI，并要求逐项检查一致性。本节取代 16.4 中“B 尚未批准／尚未实装”的当前状态；16.4 保留为历史记录。本轮只改 presentation，不改游戏规则、原线框 icon、控件尺寸、点击区、排版或真实物品缩略图。

### 17.1 B 的统一规则与修改范围

- 奶油底 `#F9F3E8`、柔桃色选中／主按钮 `#FBD5AF`、可可文字 `#5A463A`，普通描边 `#78675A`。错误、成功、警告、预览保留各自语义色，但使用同一几何规则。
- 单层规则描边：小控件 **1.75 logical units**，panel／notice **2**；圆角分别约 **12／18／16**，短单层下阴影，极淡纸纹。不是直接照搬示意图中的实心 icon 或更重字重。
- 保留字体家族与字号，正文 Regular；标题、选中的 Tab 和主按钮 Bold。Tab 取消选中会恢复 Regular。固定 **Pickup Point** 使用柔桃色主按钮和 Bold；位置、点击区和文案不变。
- 时间按钮仍是 **72×64、间隔 8**，2x 墨迹最长边 44；Decor 仍为纯线框 icon、88 底板／96 点击区。Floor／Wall 缩略图继续 fit 在原 inset frame 内。

新增 `Assets/UI/P8R/RefinedB/` 的 **22 张共享底板**，由 `P8RRefinedBAssets.cs` 用统一圆角／描边公式生成，不覆盖原 PNG。`P8RAppearance.asset` 仍为 **154 个唯一 key**，其中 22 个改指 B 底板；93 个 icon 和原缩略图等保持原引用。

主要文件：`P8RAppearance.cs` 管颜色／字重；`DecorationModeTabsView.cs` 保证真实分类切换时更新字重；`P8RFurnitureUiBuilder.cs` 保证重新创建 Appearance 时没有重复 key，并保留 Pickup 主样式；`P8RCompleteUiBuilder.cs` 增加 **Apply Approved Refined B Style Only**，只更新既有 MainCafe 和四个 P8R prefab 的样式，不重建布局。普通刷新仍有原有用途，不需要为了查看 B 再运行它。

### 17.2 已完成的技术检查

| Test case | 内容 | 状态 |
|---|---|---|
| P8R-B-001 | 奶油底、淡纹理、透明圆角、单连续描边，panel／notice 为 2 logical | 技术 PASS |
| P8R-B-002 | Primary／Secondary／Destructive 的 normal／pressed／disabled 均使用 B；按下不改变轮廓 | 技术 PASS |
| P8R-B-003 | 22 张素材为 384×384、Bilinear、Uncompressed、MipMap off、无平台 override；重复 bake 不改字节、时间戳或 dirty 状态 | 技术 PASS |
| P8R-B-004 | 从零重建 Appearance 恰为 154 唯一 key，22 B 替换，其余原路径不变 | 技术 PASS |
| P8R-B-005 | 样式迁移保留原节点与 RectTransform，重复执行不写文件；真实旧→B 引用改动后注入失败，磁盘／内存恢复且场景干净 | 技术 PASS |
| P8R-B-006 | 真实四分类 Tab 选中加粗、取消恢复；Pickup 主按钮／pressed 引用及原文案正确 | 技术 PASS |
| P8R-B-007 | MainCafe cold-load／逐帧再保存稳定；原 authoring、rollback 和 legacy guard 正常 | 技术 PASS |
| P8R-B-008 | 原生 Overlay 三分辨率、时间恢复、按下态、退出／收起弹窗及取消后返回编辑 | 技术 PASS；Owner 接受 Pending |

本轮先后复现并修复：旧 frame 路径残留、实际 Tab 未更新字重、Appearance 重建产生 176 项重复 key、TMP 颜色缓存冷加载漂移、空 Undo 标脏场景、重复 bake 的 importer setter 标脏。失败轮保留为诊断证据，不计最终 PASS。

| 最终验证 | 结果 | 证据 |
|---|---:|---|
| 全部 P8R EditMode（不是整个项目 EditMode） | **247/247 PASS** | [Edit XML](../TestResults/p8r-refined-b-20260912-edit-verified.xml) |
| 无 filter 完整 Editor PlayMode | **854/854 PASS** | [Full Play XML](../TestResults/p8r-refined-b-20260912-full-play-final.xml) |
| 正常 Editor 原生截图＋Polish／TimeOptical | **5/5 PASS** | [Native XML](../TestResults/p8r-refined-b-20260912-native-final.xml) |

全部 0 failed／skipped／inconclusive；测试有重叠，不相加。完整 PlayMode 用时约 120 秒。未运行全项目 EditMode、standalone build 或 Android／iOS 真机；不把 Editor PASS 当成真机接受。

### 17.3 最终图库与保全

唯一最终图库为 `outputs/p8r-ui-polish-20260912/refined-b-final/`：**1080×1920、720×1280、1600×720，每种 11 张，共 33 张**；PNG 自身尺寸均匹配目录。保留 production Overlay、camera 和画质设置，未用放大渲染或全局 AA 掩盖问题；`refined-b-first` 是问题修正前的诊断图库，不作为最终接受证据。

| 查看内容 | 1080p 竖屏 | 720p 竖屏 | 横屏 |
|---|---|---|---|
| 家具目录／选中 Tab／Pickup | [04](../outputs/p8r-ui-polish-20260912/refined-b-final/1080x1920/04-furniture.png) | [04](../outputs/p8r-ui-polish-20260912/refined-b-final/720x1280/04-furniture.png) | [04](../outputs/p8r-ui-polish-20260912/refined-b-final/1600x720/04-furniture.png) |
| 预览 notice／Shelf 操作 | [07](../outputs/p8r-ui-polish-20260912/refined-b-final/1080x1920/07-shelf.png) | [07](../outputs/p8r-ui-polish-20260912/refined-b-final/720x1280/07-shelf.png) | [07](../outputs/p8r-ui-polish-20260912/refined-b-final/1600x720/07-shelf.png) |
| Pickup 按下态 | [10](../outputs/p8r-ui-polish-20260912/refined-b-final/1080x1920/10-pickup-pressed.png) | [10](../outputs/p8r-ui-polish-20260912/refined-b-final/720x1280/10-pickup-pressed.png) | [10](../outputs/p8r-ui-polish-20260912/refined-b-final/1600x720/10-pickup-pressed.png) |
| Put Away 弹窗 | [11](../outputs/p8r-ui-polish-20260912/refined-b-final/1080x1920/11-put-away-modal.png) | [11](../outputs/p8r-ui-polish-20260912/refined-b-final/720x1280/11-put-away-modal.png) | [11](../outputs/p8r-ui-polish-20260912/refined-b-final/1600x720/11-put-away-modal.png) |

01–03 为 HUD／2x／暂停，05–06 为 Floor／Wall，08 为退出弹窗，09 为退出装修后恢复 2x。

独立代码 review：无 Critical／Important；提出的真实回滚覆盖缺口已补测试并通过。MainCafe＋四 prefab 的 **154 个 RectTransform block 与开工前完全相同**。原 154 张 UI PNG 及 meta、模型、footprint、domain assets 和 ProjectSettings 未变；开工前 2,393 文件只变更本轮 12 个预期路径，新增 22 对素材＋generator／3 个测试类及 meta，共 53 文件。最终回归前后 2,446 个资产／设置文件 **0 漂移、0 新增**。本指南另行更新，没有新增散落的开发说明 MD。

最终独立视觉 review 已查看全部 **33 张**三分辨率截图，结论为 **Ready for Owner manual review**，无 Critical 或阻挡级 Important。实际 Tab 字重、Pickup 主入口与 pressed、notice 圆角／短影，以及两类弹窗均通过；HUD、Decor、缩略图 fit 和 action bar 未见回归。保留两个 Minor：720p／横屏的主 panel 直线段仍约一个 device pixel，视觉上比 concept／内层卡片更轻（增重主要体现在圆角）；细线 icon 和斜线仍有轻微阶梯。这些留给 Owner 的真机／偏好确认，不宣称描边与概念图逐像素相同或锯齿全部消失。

### 17.4 Owner manual review

从 **P8 worktree** 打开 MainCafe → Play，以 Game View **1x** 检查：时间 2x→暂停→恢复；进入装修切四分类，确认只有选中 Tab 加粗；查看 Pickup、Floor／Wall preview fit；操作一件 Wall Decor，再打开／取消 Put Away 和 Exit 弹窗。重点判断 B 的描边清晰度、短阴影、主次层级和整体一致性是否合意。

当前停在 **Ready for manual review**，Owner manual acceptance **Pending**；不关闭 Phase 8、不进入新 phase，未 commit／push／merge。

## 18. 六项 UI 反馈修正（2026-09-12）

Owner 批准针对六张截图做局部调整；本节是当前规则，17 节截图和提示框描述保留为历史记录。延续 B 的颜色、线框 icon、描边和短阴影，不改摆放规则。

### 18.1 改动与已完成的定向检查

| Test case | 当前表现与检查内容 | 状态 |
|---|---|---|
| P8R-U6-001 | 家具／Wall Decor 的 floating Current Preview 说明框隐藏；Confirm／Cancel／Rotate 和 footprint 保留；未结束预览仍不能切分类 | 技术 PASS |
| P8R-U6-002 | 四个分类 Tab 无粗 SelectedUnderline；多次切换后也不恢复，选中底色和 Bold 保留 | 技术 PASS |
| P8R-U6-003 | Normal／Decoration 标签改用小按钮圆角；按实际 glyph bounds 居中，切换文案不累积偏移 | 技术 PASS |
| P8R-U6-004 | 目录不再显示预览／切换限制说明；Floor／Wall 不留说明框占位；家具仍有 Back to Editing | 技术 PASS |
| P8R-U6-005 | Whole Room／Single Grid 在最终字重确定后重排；交替选中时两者可见 icon／文字 gap 均约 8 logical units | 技术 PASS |
| P8R-U6-006 | Paint 卡片使用实际材质颜色的纯色色块；Floor／Wallpaper 仍保留图案；卡片复用后恢复白 tint | 技术 PASS |

隐藏的是两种预览说明，不是顶部 Confirmed Layout 营业状态，也不是 Put Away／Exit 确认弹窗。诊断信息与事务限制仍保留。两个 game UI skills 用于检查可见信息是否必要、icon 墨迹对齐、触控区和多尺寸安全边界；没有缩小点击区或新增 tooltip。

主要文件及用途：

- `DecorationActionBarView.cs`／`DecorationCatalogueView.cs`：隐藏说明、去掉对应占位，保留返回编辑操作。
- `DecorationModeTabsView.cs`／`TimeControlPanel.cs`／`P8RAppearance.cs`：Tab 状态、标签居中、最终字重后的 icon／文字排版。
- `DecorationCatalogueModels.cs`／`DecorationCatalogueTileView.cs`：从 Paint 材质只读颜色，显示纯色色块并正确重置复用卡片。
- `P8RFurnitureUiBuilder.cs`／`P8RCompleteUiBuilder.cs`：今后重建 UI 时保持相同规则；本次无需运行重建菜单。
- 七份现有测试文件更新：新加六个真实场景测试，将旧“说明框可见／横线可见”的视觉断言改成新规则，保留原交互与安全检查。

### 18.2 本轮验证与截图

六个新测试先在原实现上全部 **RED**；初次修正通过 5/6，最后用实际文字边界消除 mode badge 的剩余偏心，并加入 B 底板可见中心的 1 logical unit 补偿。

| 最终验证 | 结果 | 证据 |
|---|---:|---|
| 全部 P8R EditMode（不是整个项目 EditMode） | **247/247 PASS** | [Edit XML](../TestResults/p8r-six-feedback-edit-verified.xml) |
| 无 filter 完整 Editor PlayMode | **860/860 PASS** | [Full Play XML](../TestResults/p8r-six-feedback-full-play-pass.xml) |
| 正常 Editor 原生截图＋六项／Polish／TimeOptical／真实三柜台流程 | **12/12 PASS** | [Native XML](../TestResults/p8r-six-feedback-native-final.xml) |

均 0 failed／skipped／inconclusive，测试集合有重叠，不相加。早一轮 P8R 与 Catalogue EditMode **256/256 PASS** 是额外覆盖，不替代上表最终 P8R 结果。两轮 full-play 失败记录是旧说明可见性断言与隐藏父级查找的诊断证据，不计最终 PASS；对应测试更新后仍保留 invalid Confirm、真实 Touch、六格 footprint、取消不落地、ReadinessVersion 不变等检查。

本轮图库为 `outputs/p8r-ui-polish-20260912/six-feedback-final/`，使用 production Screen Space Overlay，不改相机或画质；三种尺寸各 13 张，共 **39 张原生 PNG**，没有覆盖历史截图。`six-feedback-first` 是最终 1-unit 标签补偿前的诊断图；只用 `six-feedback-final` 进行本轮接受。

| 重点截图 | 720p 竖屏 | 横屏 |
|---|---|---|
| 家具预览：无说明框 | [12](../outputs/p8r-ui-polish-20260912/six-feedback-final/720x1280/12-furniture-preview.png) | [12](../outputs/p8r-ui-polish-20260912/six-feedback-final/1600x720/12-furniture-preview.png) |
| Floor：无说明占位、范围间距 | [05](../outputs/p8r-ui-polish-20260912/six-feedback-final/720x1280/05-floor.png) | [05](../outputs/p8r-ui-polish-20260912/six-feedback-final/1600x720/05-floor.png) |
| Wall：Paint 纯色色块 | [06](../outputs/p8r-ui-polish-20260912/six-feedback-final/720x1280/06-wall.png) | [06](../outputs/p8r-ui-polish-20260912/six-feedback-final/1600x720/06-wall.png) |
| 返回编辑入口保留 | [13](../outputs/p8r-ui-polish-20260912/six-feedback-final/720x1280/13-return-without-explanation.png) | [13](../outputs/p8r-ui-polish-20260912/six-feedback-final/1600x720/13-return-without-explanation.png) |

同目录 1080×1920 也有对应截图；01–03／09 为时间与状态，04／10 为目录，07 为 Shelf，08／11 为两类确认弹窗。Editor 验证不代表 Android／iOS 真机 DPI 和触感验收。

独立代码 review 无 P0–P2 finding。独立视觉 review 已查看三尺寸全部 39 张总览、05／06／12／13 原像素局部；最终又查看三尺寸 Normal／Decoration 的 6 张 HUD 原像素局部，确认没有明显偏心、裁字或圆角不一致。未见新增遮挡、弹窗溢出或返回入口丢失。Single Grid 选中后的字重间距由自动状态切换测试覆盖，不把静态 Whole Room 截图冒充这项证据。

### 18.3 你如何查看

从 **P8 worktree** 打开 `Assets/Scenes/MainCafe.unity` → Play，以 Game View **1x** 检查六项；尝试 Whole Room↔Single Grid、家具预览→Show Catalogue→Back to Editing→Cancel。确认取消仍不应用预览，未完成编辑时不能切走。视觉是否满意仍由 Owner manual review 决定；未 commit／push／merge。

最终保全：相对本轮开工前 2,446 个 Assets／ProjectSettings 文件，只变化 **9 个 UI 实现文件＋7 个测试文件**，没有新增 Assets；MainCafe、P8R prefab、原始 154 PNG／meta、实际材质、模型、footprint 与 ProjectSettings 未变。本指南另行更新。较宽的 EditMode filter 曾触发旧 builder 将三份 Phase6 prefab 重新序列化（默认 null 字段及空白）；已保留副作用副本并精确恢复开工前字节，再运行完整 PlayMode 和最终 P8R EditMode；最后 audit 无这些额外差异。

本轮 **Ready for manual review**，Owner acceptance **Pending**；不关闭 Phase 8、不进入下一 phase。

## 19. B 方案线条轻加粗（2026-09-12）

本节更新 §17 中的线宽；B 的配色、圆角、纸感、阴影、字体、控件尺寸、间距与点击区域保持不变。没有恢复已删除的说明框、Tab 横线或 tooltip。

### 19.1 本次修改

| 范围 | 已实施 |
|---|---|
| 按钮、Tab、卡片外框与 preview outline | 2.25 logical units（9 source pixels） |
| 主 Panel、弹窗、Notice | 2.5 logical units（10 source pixels） |
| 内嵌 Panel 与卡片预览内框 | 2 logical units（8 source pixels），保留较轻层级 |
| 31 种 action icon 的三色版本及 5 个状态图标 | 共 98 个独立派生 PNG；保留原色、256×256 画布及 alpha≥16 的原可见边界 |

Icon 沿已有 alpha-mask 素材流程派生；以约 20% 墨迹覆盖增量作为可重复校准指标，不将其当作每一段笔画都精确增加 20% 的测量。仅在 Editor 离线生成，没有新增 runtime shader、Outline 特效或每帧计算。原 154 张 PNG 完整保留。

主要文件：

- `Assets/Editor/P8R/P8RRefinedBAssets.cs`：22 个共享底板线宽与新旧资源映射。
- `Assets/Editor/P8R/P8RRefinedBGlyphs.cs`：独立图标派生、抗锯齿、可见边界保护及重复生成入口。
- `P8RFurnitureUiBuilder.cs`／`P8RCompleteUiBuilder.cs`：Appearance 重建、静态 Image、按钮状态与隐藏界面共同更新；保持 154 个唯一 key。
- `Assets/UI/P8R/RefinedB/Icons/`：98 个新图标；更新 Appearance、四个 P8R prefab 和 MainCafe 的引用。
- 三个已有 EditMode 测试文件及新增 `P8RRefinedBGlyphTests.cs`：验证线宽、图标颜色／边界／确定性、重建、迁移与失败回退。

`P8RButtonLayout.cs` 无需修改：新素材的可见边界保持原值，已有光学校准继续有效。游戏规则、存档结构、输入逻辑与 footprint 未在本轮修改。

### 19.2 测试记录

| Case | 检查项 | 状态 |
|---|---|---|
| P8R-ST-001 | 主框、按钮与内框按批准层级加粗；普通／按下状态轮廓一致 | 技术 PASS |
| P8R-ST-002 | 98 图标原色、透明背景、256 画布、可见边界保留；三色 alpha 一致 | 技术 PASS |
| P8R-ST-003 | Appearance 154 unique keys；重建、静态图标与 runtime 状态均使用新 pack | 技术 PASS |
| P8R-ST-004 | 仅图标过期也会触发迁移；失败恢复，RectTransform／对象身份保留；重复迁移不写入 | 技术 PASS |
| P8R-ST-005 | 时间、装修切换、家具／墙饰、范围切换、确认／取消与弹窗交互 | 技术 PASS |

TDD：修改前 P8R EditMode **247/247 PASS**；线宽／图标新增断言先得到 **7 个预期 RED**，旧图标映射、重建与回退新增检查另得到 **4 个预期 RED**，随后实施并验证 GREEN。中间 cancel glyph 的固定半径增粗不足，已改为相邻半径插值；保留原测试阈值。

| 最终自动验证 | 结果 | 证据 |
|---|---:|---|
| 全部 P8R EditMode | **250/250 PASS** | [Edit XML](../TestResults/p8r-stroke-edit-green.xml) |
| 无 filter 完整 Editor PlayMode | **860/860 PASS** | [Full Play XML](../TestResults/p8r-stroke-full-play.xml) |
| 正常 Editor 原生截图＋Polish／TimeOptical／SixFeedback／三柜台流程 | **12/12 PASS** | [Native XML](../TestResults/p8r-stroke-native.xml) |

三组均 0 failed／skipped／inconclusive；测试集合有重叠，不相加。既有 Phase7 TMP obsolete 警告不是本轮新增，未借本次换肤修改无关代码。

重复烘焙并应用后核对 **247 个生成／接入文件**，内容与修改时间变化均为 **0**；原始 154 PNG＋154 meta 相对开工前副本变化为 **0**。独立代码 review 未发现 P0–P2 问题。独立素材 QA 已查看全部 31 个 action＋5 个状态 glyph 的 100px／40px 新旧对照，未发现明显新增黏连、变形或色差；93 张三色变体 alpha 一致性异常为 0。

### 19.3 你如何 review

本轮原生图库：`outputs/p8r-ui-polish-20260912/stroke-final/`，三种尺寸 1080×1920、720×1280、1600×720，各 13 张，共 **39 张**。使用 production Screen Space Overlay 与原画质，没有覆盖之前截图。旧版对照在同父目录 `six-feedback-final/` 的同名文件。

- [Floor 目录与范围按钮](../outputs/p8r-ui-polish-20260912/stroke-final/720x1280/05-floor.png)
- [Wall Paint 卡片](../outputs/p8r-ui-polish-20260912/stroke-final/720x1280/06-wall.png)
- [Wall Decor 浮动按钮](../outputs/p8r-ui-polish-20260912/stroke-final/720x1280/07-shelf.png)
- [退出确认弹窗](../outputs/p8r-ui-polish-20260912/stroke-final/720x1280/08-exit-modal.png)

从 P8 worktree 打开 `Assets/Scenes/MainCafe.unity` → Play，在 Game View **1x** 判断线条粗细是否合适；试一下 Pause／1x／2x、家具预览及墙饰确认／取消。字体与按钮不应变大，双三角及房子等图标细节应仍可区分。

需要重复应用同一版素材时，先退出 Play 并保存已有编辑，再用 `Tools > AnimalCafe > P8R > Apply Approved Stronger B Presentation`；日常 review 不需要运行此菜单。

Owner 视觉接受仍为 **Pending**，不代表 Android／iOS 真机 DPI 或触感验收；本轮未 commit／push／merge，也不进入下一 Phase。

最终独立界面视觉 review 已查看全部 39 张总览，以及三尺寸 HUD、目录、floating actions 和两类弹窗的原生像素 crop，并与旧版 HUD 对照：未发现明显偏心、拥挤、裁切或图标黏连。两个 game UI skills 用于保留主／次边框层级、检查小屏清晰度和现有触控布局。

本轮 **Ready for manual review**；请 Owner 判断现在的线条粗细是否合适。

## 20. Icon 缩小显示抗锯齿与取餐标志初稿（2026-09-12）

本节修正 §19 的实际小尺寸显示问题：源 PNG 存在半透明边缘，并不等于 Unity 缩小后也能平滑。当前 256px 图标通常只占二三十个屏幕像素，原先 Bilinear、无 mipmaps 会漏采部分笔画，造成台阶和局部粗细不均。

### 20.1 已实施的修复

- `Assets/Editor/P8R/P8RRefinedBGlyphs.cs`：98 个正在使用的 icon 改用 BoxFilter mipmaps + Trilinear，缩小前先保留各区域的笔画覆盖。明确关闭 alpha-test coverage preservation、fade、border 和 streaming，mip bias 保持 0。
- `Assets/UI/P8R/RefinedB/Icons/*.png.meta`：Unity 重新导入后的设置。**98 张 PNG 字节不变**，B+ 加粗程度、图形、配色和光学边界不变；没有再画一套图标。
- `P8RGlyphMinificationTests.cs`：新增真实 GPU 缩图与源 alpha 面积平均对照；原 `P8RRefinedBGlyphTests.cs` 同步验证所有 98 个图标的导入契约。
- `P8RReferenceLayoutTests.cs`：原生截图检查新增 480×854，补足用户反馈所处的小界面显示条件。

不改按钮尺寸、间距、点击范围、字体、9-slice 底板、场景、prefab、存档或游戏规则。旧的 PNG 工具缩图检查不能替代本轮实际 GPU／Native Overlay 证据。启用 mipmaps 会为 icon 纹理增加约三分之一纹理数据，不增加 runtime 生成或每帧脚本。

### 20.2 本轮测试与证据

| Case | 验证项 | 状态 |
|---|---|---|
| P8R-AA-001 | 32／64px 的真实 GPU alpha 覆盖更接近原 PNG 的面积平均；每个受测图标墨量偏差不超过 3% | PASS |
| P8R-AA-002 | 98 图标导入、三色几何、原色、可见边界、确定性及 Appearance 引用回归 | PASS |
| P8R-AA-003 | 时间按钮、目录、家具／墙饰操作、范围选择与弹窗直接交互回归 | PASS |
| P8R-AA-004 | 1080×1920、720×1280、480×854、1600×720 四尺寸 Native Overlay 截图 | 已完成；Owner 视觉接受 Pending |
| P8R-AA-005 | 重复 bake 检查 98 PNG＋98 meta，内容或修改时间变化 0；不撤销新设置 | PASS |

TDD 先得到两个预期 RED：旧采样使部分图标的墨量分别偏高 5.07% 和 3.44%，不是测试编译或环境错误。保持同一阈值应用修复后通过。36 种独立形状的 GPU alpha 平均绝对误差之和：32px 为 0.007378（无 mipmap 对照 0.793633）；64px 为 0.005066（对照 0.261887）。这是缩图覆盖指标，不是“肉眼锯齿减少百分比”。

- 全部 P8R EditMode：**252/252 PASS**，0 failed／skipped／inconclusive：[结果](../TestResults/p8r-icon-aa-edit-green.xml)。
- 正常 Editor 的原生截图与直接交互回归：**12/12 PASS**，0 failed／skipped／inconclusive：[结果](../TestResults/p8r-icon-aa-native.xml)。
- 修复前复现：[RED](../TestResults/p8r-icon-aa-red.xml)；修复前截图流程 **1/1 PASS**：[结果](../TestResults/p8r-icon-aa-before-native.xml)。

前后图库均为四种尺寸、每尺寸 13 张，共各 **52 张**；保留原相机、后处理与 production Overlay，不用软件缩放图冒充原生截图。目录为 `outputs/p8r-ui-polish-20260912/icon-aa-before/` 与 `icon-aa-final/`。本轮是局部 UI 回归，不将 §19 的完整 PlayMode 历史结果当作本轮重新执行。

独立代码与视觉复核无阻挡项。视觉复核覆盖四尺寸 HUD／目录的原生 1:1 crop，并抽查 shelf 操作和 exit modal；未发现新增粘连或布局变化。480px 的 icon 会更柔和，Wall Decor 内部细节仍然很小；本轮结论是“缩小锯齿减轻、内部空隙保留”，不宣称所有显示尺寸完全无锯齿，也不将抽查等同于逐张人工检查全部 52 张。

### 20.3 如何 review

从 P8 worktree 打开 `Assets/Scenes/MainCafe.unity` → Play；在 Game View **1x** 查看 Pause／1x／2x 及四类目录 icon。查看图时也用 100% 比例；额外缩放的截图不能用于判断原生像素质量。确认边缘更连贯，暂停的两个空隙、双三角、地板四格及墙饰内部细节仍可区分。

- [720px HUD 修复后](../outputs/p8r-ui-polish-20260912/icon-aa-final/720x1280/01-hud-1x.png)
- [720px 家具目录修复后](../outputs/p8r-ui-polish-20260912/icon-aa-final/720x1280/04-furniture.png)
- [480px 小界面修复后](../outputs/p8r-ui-polish-20260912/icon-aa-final/480x854/04-furniture.png)

### 20.4 彩色 Pickup Point 标志——仅设计预览

[查看初稿](../outputs/p8r-icon-aa-20260912/pickup-marker-concept.png)。使用 imagegen 制作：青绿色牌面、奶油色咖啡杯、暖杏色边缘和底部居中小尖角；用杯子表达取餐，用尖角指向柜台中心，不依赖文字或只有颜色的提示。

**尚未导入 Assets，也没有替换倒棱锥**。这张图用于确认造型与色彩，不是实际游戏截图；若 Owner 喜欢，再单独接入、检查实际显示大小、遮挡与中心对齐。当前图标修复等待 manual review，新标志等待设计选择；不代表 Phase 8 验收完成，不进入下一 Phase，不 commit／push／merge。

## 21. 彩色取餐牌接入与四枚操作 Icon 等宽（2026-09-12）

Owner 已批准 §20.4 的彩色初稿并要求应用；因此该节“尚未导入”的状态已由本节取代。本轮仍在 `codex/phase-8-functional-furniture`，不进入下一 Phase，不 commit／push／merge。

### 21.1 本轮修改

- `Assets/UI/P8R/WorldMarkers/pickup_point.png`：直接使用批准的原图，PNG 字节未改。青绿色牌面、奶油色杯子与暖色描边保持原样。
- `Phase8AssetBuilder.cs`、`AnimalCafe.Editor.asmdef` 与取餐点 prefab／material：原倒棱锥显示换成透明 Unlit 牌面；用 UV 排除透明留白、按底部尖角对准 Slot。Prefab GUID、原 mesh local ID、位置锚点和 Footprint 保留，不修改 MainCafe 场景布局。
- `P8RPickUpSignBillboard.cs`：只让牌面朝向游戏 Camera，不转动柜台、Slot 或 Footprint。
- `P8RRefinedBGlyphs.cs` 与 RefinedB 下 12 张派生 PNG：收起、取消、旋转、确认保留原语义和外框，重建等宽圆角笔画。以 32 logical 的可见图形为基准，目标线宽 3.5；七处实际截面测得 3.483–3.525。三种原色、按钮尺寸／间距、点击范围、mipmaps＋Trilinear 抗锯齿不变；其他 86 张派生图标不变。从本节起，这四枚不再使用 §19 的统一墨量增幅规则。
- `DecorationModeController.cs`：移动已放取餐牌的柜台时，操作栏避让边界包括该柜台的牌面，避免“收起”挡住新牌；不扩大按钮。
- `PickUpPointIndicatorView.cs`：同一个已占用 Slot 上出现无效预览时，暂时隐藏被覆盖的旧标记，避免双牌与红绿 Footprint 叠成黄色。取消或移开后恢复；不改变占用、CanConfirm、存档，也不恢复 controller 正在隐藏的原件。

两个 game UI skills 用于维持方案 B 的线条与颜色一致性、保持触控布局，并检查小屏、标记遮挡和状态可读性。

### 21.2 测试与截图

| Case | 验证项 | 当前状态 |
|---|---|---|
| P8R-SIGN-001 | 批准原图、透明材质、无额外阴影、牌面朝向；Slot／Footprint 不随牌面旋转 | PASS |
| P8R-SIGN-002 | 重复更新仍保留 prefab GUID／mesh local ID 与其他受保护资产 | PASS |
| P8R-SIGN-003 | 同 Slot 无效预览只显示一牌；不能 Confirm；取消、换 Slot、Rebuild、退出后恢复正确 | PASS：focused 19/19 |
| P8R-SIGN-004 | 收起／取消／旋转／确认七处截面线宽、三色共用几何、原色、边界与 AA | PASS：focused 10/10 |
| P8R-SIGN-005 | 带取餐牌的柜台移动时，四枚操作按钮都不遮牌；按实际按钮高度留出下沿间隙 | PASS：四尺寸 |
| P8R-SIGN-006 | 四种原生尺寸：正常牌色、有效绿色／无效红色 Footprint、牌面命中分类、取消恢复 | PASS：Native 12/12 |
| P8R-SIGN-007 | P8R＋取餐牌资源 EditMode、完整 PlayMode，以及最后定位调整后的相关回归 | PASS：284/284、864/864、95/95 |

本轮先复现了七个线宽差异 RED、两个未接入取餐牌 RED、四个同 Slot 可见性 RED，以及一个操作栏遮牌 RED。它们与测试准备错误分开记录，没有把编译失败或错误调用当成目标问题的 RED。

首次截图曾捕获 Unity 异步 shader 编译的青色占位方块；截图流程现在在真实 Game View 渲染后等待 shader 就绪，并检查杯子与青绿色牌面的像素。没有改变 production 画质或关闭用户的异步编译设置。等待依据：[Unity ShaderUtil.anythingCompiling](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShaderUtil-anythingCompiling.html)。

本轮实际执行结果（均为 0 failed／skipped／inconclusive）：

- P8R＋取餐牌资源 EditMode：**284/284 PASS**，[结果](../TestResults/p8r-pickup-strokes-edit-verified.xml)。
- 完整 PlayMode：**864/864 PASS**，[结果](../TestResults/p8r-pickup-strokes-full-play.xml)。
- 最后横屏留白调整后，按钮几何、功能家具触控和取餐牌生命周期复测：**95/95 PASS**，[结果](../TestResults/p8r-pickup-strokes-final-targeted-play.xml)。
- 正常 Editor 原生截图与直接交互检查：**12/12 PASS**，[结果](../TestResults/p8r-pickup-strokes-native-ready.xml)。脚本检查牌面屏幕坐标命中并调用同一选择处理函数；不将其描述为真人触屏操作。

最终图库为 `outputs/p8r-ui-polish-20260912/pickup-sign-ready/`：1080×1920、720×1280、480×854、1600×720，各 18 张，共 **72 张**。`pickup-sign-first`／`pickup-sign-final`／`pickup-sign-reviewed` 是排查阶段留档，**不是最终验收图库**；最终只看 `pickup-sign-ready`。

- [彩色取餐牌有效预览](../outputs/p8r-ui-polish-20260912/pickup-sign-ready/720x1280/14-pickup-valid-preview.png)
- [小屏单牌、红色无效预览](../outputs/p8r-ui-polish-20260912/pickup-sign-ready/480x854/15-pickup-invalid-preview.png)
- [柜台移动、四枚等宽按钮与牌面避让](../outputs/p8r-ui-polish-20260912/pickup-sign-ready/720x1280/18-furniture-actions.png)
- [横屏按钮下沿留白](../outputs/p8r-ui-polish-20260912/pickup-sign-ready/1600x720/18-furniture-actions.png)

主代理和独立视觉复核抽查了有效／无效／取消恢复／选中／柜台移动状态的全景与原生 crop；最终四尺寸中未发现牌面遮挡、双牌叠色或操作 icon 黏连等阻断问题。这是关键状态抽查，不声称逐张人工审查全部 72 张。独立代码复核未发现本轮实现的阻断项。

最终文件比对确认：批准原图字节一致；仅指定 12 张 icon PNG 更新，98 个原 importer 设置未变，其余 86 张派生 PNG 未变；MainCafe 场景文件相对本轮开工前不变。没有改存档格式或布局规则。

### 21.3 你如何 manual review

从 P8 worktree 打开 `Assets/Scenes/MainCafe.unity` → Play，Game View 用 **1x**：

1. 进入 Decoration，选 Pick-up Point，在空柜台上预览并 Confirm：应看到一枚彩色咖啡牌，尖角对准取餐位置。
2. 点击牌面重新编辑，再 Cancel：原标记应恢复，不多出一枚，也不消失。
3. 新增第二个取餐点并移到已占位置：只显示当前预览牌，Footprint 为红色、Confirm 不可用；Cancel 后原牌恢复。
4. 选择并移动该柜台：四枚按钮不应挡住牌面；比较收起／取消／旋转／确认的线宽。按钮不应变大、间距不应变化。
5. 调整 Camera 缩放，确认牌面仍朝向屏幕；再检查 480px 小界面下杯子和操作图标是否容易辨认。

Owner 视觉接受保持 **Pending**；自动测试和桌面原生截图不代替 Android／iOS 真机触感与 DPI 验收。

本轮已到 **Ready for manual review**，停在这里。未重新执行 Standalone build，不将旧版 build 结果算作本轮新验证；未 commit／push／merge。

## 22. 滚轮恢复与取餐牌轻浮动（2026-09-12）

本节记录 Owner 批准的滚轮与取餐牌两项改动，均已通过下述自动检查，可 manual review。彩色 Tab 的后续决定：Owner 不采用从概念原图拆图的方式，保留独立生成的 Floor、Walls、Wall Decor，只重新设计 Furniture；本轮独立生成的暖木／奶油色扶手椅已通过透明度检查。四个 Tab 的接入与验证记录见下方第 23 节。

### 22.1 改动与原因

- `Assets/Scripts/Decoration/DecorationCameraDriver.cs`、`DecorationModeController.cs`：滚轮走独立 `ApplyWheelZoom`，与普通模式保持整步缩放；真正双指 pinch 继续按实际位移连续缩放。问题原因是滚轮误入 pinch 的 `/40` 计算；默认输入一格为 1 时，本应变化 0.75，却只变化 0.01875。不修改全局 sensitivity、Scene 或 CameraSettings。
- `Assets/Scripts/UI/P8R/P8RPickUpSignBillboard.cs`：牌面增加 0.112 world unit 高度（原牌高的 20%），再做 ±0.028、周期 2.8 秒的缓慢上下浮动。使用 unscaled time，装修暂停时也能动；新预览共享时间相位，不在每次移动时重新开始。
- `Assets/Scripts/Decoration/PickUpPointIndicatorView.cs`：拖动时的 hover offset 单独交给牌面组件，避免浮动覆盖原来的拖动抬高；旧版测试模型保留原处理方式。
- `DecorationModeController.cs` 的动作栏布局使用完整浮动范围，不逐帧追随牌面。首次创建或旋转柜台后，在计算按钮位置之前同步牌面朝向。实际 Slot、Footprint、柜台位置、摆放规则和存档不变。
- 对应测试：`Phase6DecorationScenePlayModeTests.cs`、`Phase8CatalogueWheelInputPlayModeTests.cs`、新增 `P8RPickUpFloatPlayModeTests.cs`、`P8RReferenceLayoutTests.cs`。

### 22.2 Test cases 与证据

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-ZF-001 | 普通/装修模式的 ±1、±120 滚轮输入均保持一整步；Catalogue 展开时步幅不变 | PASS |
| P8R-ZF-002 | 拖动家具时滚轮不抢输入；pinch 小位移按比例缩放、相同距离不因帧数不同而变化 | PASS |
| P8R-ZF-003 | 抬高与浮动幅度受限，timeScale=0 时仍缓慢浮动 | PASS |
| P8R-ZF-004 | Slot、Footprint 不移动；hover 保留；重复隐藏/显示不累积高度 | PASS |
| P8R-ZF-005 | OnEnable 后再旋转 parent，首帧立即朝向 Camera，不延迟一帧 | PASS |
| P8R-ZF-006 | 真实 MainCafe 的取餐点三按钮、柜台四按钮，各观察 3.1 秒并逐帧重算；按钮稳定且不遮住浮动牌面 | PASS |
| P8R-ZF-007 | 真实取餐点预览重建后仍保留 hover；原取餐点显示/隐藏/恢复和占用状态回归 | PASS |
| P8R-ZF-008 | 四尺寸原生 Overlay 截图、真实牌面颜色、点击分类及周边空白点击回归 | PASS |

TDD：滚轮新断言先得到 **5 个预期 RED**；初次浮动测试得到 **2 个预期 RED**；独立 review 找出的首帧问题另得到 **1 个预期 RED**（实际偏转 90°）。修复后最终结果：

- [EditMode 158/158](../TestResults/p8r-wheel-motion-tabs-edit.xml)：取餐牌、AssetBuilder 与 Footprint 资产回归。
- [相关 PlayMode 337/337](../TestResults/p8r-wheel-motion-final-play.xml)：滚轮、pinch、取餐点、家具交互、现有 P8R UI 与完整浮动周期。
- [正常 Editor 原生检查 13/13](../TestResults/p8r-wheel-motion-native.xml)：1080×1920、720×1280、480×854、1600×720；每种尺寸 18 张，共 72 张原生截图。没有放大后冒充原生像素，也没有改 production 画质。

截图示例：[小屏取餐点编辑](../outputs/p8r-ui-polish-20260912/wheel-float-ready/480x854/17-pickup-selected.png)、[横屏柜台四按钮](../outputs/p8r-ui-polish-20260912/wheel-float-ready/1600x720/18-furniture-actions.png)。静态截图只证明布局；浮动过程的稳定性由上述完整周期测试检查，实际动感仍需 Owner 亲自观看。

独立视觉 review 逐张查看了四尺寸的取餐牌编辑与柜台四按钮截图，共 8 张关键状态：未见阻挡、裁切或按钮大小/间距回归。1600×720 的取餐点操作栏距离较紧但仍与牌面分开；480×854 的杯子较小但仍可辨。没有将这次静态检查称作实际鼠标手感或真机验收。

### 22.3 你如何 manual review

1. 从 P8 worktree 打开 MainCafe 并 Play：在普通和装修模式各滚动一格，缩放幅度应一致；拖动家具时滚轮不应打断操作。
2. 在柜台上预览/确认 Pick-up Point：牌面比之前稍高并缓慢上下浮动；桌面的 Footprint 保持不动。
3. 点击牌面编辑，再选择并旋转所在柜台：操作按钮不随牌面上下晃，也不挡住牌面；Cancel 后原牌正确恢复。
4. 反复进入/退出装修、切换预览、改变 Camera 缩放，确认没有越浮越高、丢失 hover 或首帧横转。

这两项的 Owner 手感/视觉接受仍为 **Pending**。四个彩色 Tab 的最新状态以第 23 节为准。没有执行 Standalone build，没有 commit／push／merge，不进入下一 Phase。

## 23. 四分类彩色 Tab icon（2026-09-12）

本节保留首次接入的历史记录；随后 Owner 批准的放大、居中与 icon-only 排版以第 24 节为准。

Owner 批准继续采用第二套风格，并明确不用从概念原图拆图。Floor、Walls、Wall Decor 保留独立生成版本；Furniture 改成暖木扶手椅、奶油色完整坐垫、少量青绿侧边。四个按钮的大小、间距、文字及 B 方案底板不变。

### 23.1 素材与接入范围

最终 PNG 在 `Assets/UI/P8R/TabIcons/`，均为 1254×1254、透明背景；已逐一比对 SHA-256，项目内文件与生成原件完全相同，没有裁图、抠图或重新采样图片像素。

| 文件 | 图案 | 原始生成文件 ID |
|---|---|---|
| `tab_furniture_color.png` | 暖木／奶油色扶手椅 | `exec-9e6055cc-dcbe-4923-b4b0-2fa348f2d2da` |
| `tab_floor_color.png` | 奶油色／陶土色地板 | `exec-e7d2ad27-6198-42cc-8624-6134729611b7` |
| `tab_wall_color.png` | 青绿墙面与滚筒 | `exec-fa7ffb31-8b11-46f7-be17-8d745127b1c5` |
| `tab_wall_decor_color.png` | 暖木画框与青绿叶子 | `exec-08ca9ef7-107f-4dc3-9784-81dec65e559e` |

Furniture 生成设计要求：front-view warm wooden cafe armchair, solid ivory back and seat cushions, apricot wood frame and legs, small muted teal side inserts, cocoa outlines with rounded joins, restrained two-tone shading, transparent background, no text/backplate/external shadow, no transparent holes inside upholstery. 颜色参考 ivory `#FFF1D5`、wood `#D9986B`、cocoa `#624631`、teal `#62A49D`。新图坐垫中央 alpha 为 254/255，外部角落为 0，不再出现此前的靠背透明洞。

接入只替换 Appearance 中原有的四个 `*_cocoa` 分类引用，不增加或删除 key；原 98 张单色 glyph 保留。Unity Sprite 使用实际可见范围排版，不改 PNG；小图启用 mipmap、Trilinear 和无压缩导入。现有 MainCafe 在切换分类时绑定新图，保持白色 tint；图标长边维持原有 32／40 logical unit，其他图标不变。

主要文件：新增 `Assets/Editor/P8R/P8RColoredTabAssets.cs` 负责原图导入和四个引用接线；`P8RFurnitureUiBuilder.cs`、`P8RRefinedBAssets.cs` 保证以后重建仍优先使用彩色图标；`Assets/Scripts/UI/P8R/P8RButtonLayout.cs` 处理 Sprite 可见范围；`Assets/Scripts/UI/Decoration/DecorationModeTabsView.cs` 负责已有场景首次显示、重新启用及分类切换时复绑。Unity 保存的资产差异只有 `P8RAppearance.asset` 的四个引用与 `PF_UI_P8RCatalogue.prefab` 的四个 Icon 图片／RectTransform；没有修改 Scene、按钮、文字或底板。

### 23.2 Test cases 与证据

首次 RED：新增检查实际得到 **11 个预期失败**，分别捕获导入未实现、旧图引用未替换、分类切换仍显示旧图、可见尺寸不正确，以及重建后的引用回退；无编译错误。证据：[首次 RED](../TestResults/p8r-colored-tabs-red.xml)。

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-CT-001 | 四张原 PNG 字节不改，实际 alpha rect、透明背景和无压缩 mipmap 导入正确 | PASS |
| P8R-CT-002 | 重复导入不重写 PNG／meta；重复应用的 10 个目标文件内容和修改时间不变 | PASS |
| P8R-CT-003 | Appearance 仍为 154 个唯一 key，只替换四个分类；重建及 B migration 不退回单色 | PASS |
| P8R-CT-004 | 真实 MainCafe 首次进入、tabs 关闭再启用、各分类与禁用态恢复彩色图及白色 tint | PASS |
| P8R-CT-005 | Sprite 可见长边保持 32／40，图标居中、文字与按钮排版不变 | PASS |
| P8R-CT-006 | 旧 98 PNG、98 meta、glyph generator 共 197 项 checksum 不变；migration rollback／幂等回归通过 | PASS |
| P8R-CT-007 | 四尺寸原生截图、四分类选中状态及展开／收起布局检查 | PASS |
| P8R-CT-008 | 原滚轮步幅、pinch、取餐牌浮动、家具／取餐点操作回归 | PASS |

最终结果：

- [EditMode 426/426](../TestResults/p8r-colored-tabs-edit.xml)：P8R 素材／排版、Phase 8 asset builder、Footprint。
- [PlayMode 338/338](../TestResults/p8r-colored-tabs-play-final.xml)：真实场景、分类显示／重启、已有交互及第 22 节改动。
- [正常 Editor 原生检查 14/14](../TestResults/p8r-colored-tabs-native.xml)：1080×1920、720×1280、480×854、1600×720，每尺寸 18 张，共 72 张截图；保持 production rendering。
- 重复 `ApplyApproved` 成功，10 个目标文件的 SHA-256 和修改时间全不变；[应用日志](../Logs/p8r-colored-tabs-apply-repeat.log)。

首次 PlayMode 出现的两条尺寸失败也已核实并处理：旧 `P8RCompleteFlowTests.MeasuredInk` 按整张 PNG 换算，忽略 Sprite rect，导致透明留白重复扣除。Walls 被误算成 `40 × 994 / 1254 = 31.70654`。测试 helper 改为按 Sprite rect 原点／尺寸换算；原 256 全图算法等价，未放宽尺寸阈值，也未为通过测试而放大图标。[首次结果 336/338](../TestResults/p8r-colored-tabs-play.xml) 保留作为诊断证据。

视觉自检逐张覆盖四尺寸的 Furniture／Floor／Walls／Wall Decor，共 16 张关键状态。未见错位、文字碰撞、裁切、单色回退、白底方块或明显锯齿。Floor 的原图横向、视觉上较矮，是保留原图比例的结果，没有拉伸成方形。两个 game UI skill 的检查重点落实为：以可见图案居中、保留已批准的 B 布局、实际小尺寸／横竖屏和状态切换复核。

截图示例：[720px 展开目录](../outputs/p8r-ui-polish-20260912/colored-tabs-ready/720x1280/04-furniture.png)、[480px 小屏](../outputs/p8r-ui-polish-20260912/colored-tabs-ready/480x854/04-furniture.png)、[横屏 Wall Decor](../outputs/p8r-ui-polish-20260912/colored-tabs-ready/1600x720/07-shelf.png)。

### 23.3 你如何 manual review

1. 打开 P8 worktree 的 MainCafe 并 Play，进入装修：四个 Tab 应显示扶手椅、地板、墙面滚筒、叶子画框。
2. 依次点击 Furniture → Floor → Walls → Wall Decor，再切回 Furniture；图标保持彩色、不闪回单色，底板仍表达当前选中状态。
3. 展开／收起 Catalogue，改变窗口尺寸，检查图标居中、比例和文字间距；按钮不应变大。
4. 顺便复查第 22 节的滚轮缩放与取餐牌浮动，确认这次换图没有影响它们。

本轮已到 **Ready for manual review**。Owner 视觉接受保持 **Pending**；自动截图不代替实际操作或手机真机验收。未运行新的 Standalone build，未 commit／push／merge，不进入下一 Phase。

## 24. 彩色分类 Tab 放大与 icon-only 排版（2026-09-12）

Owner 确认：去掉四个 Tab 的文字，图标放大并居中；Floor 额外做等比视觉补偿。保留原 PNG、B 方案底色和边框、按钮尺寸／间距／点击范围；不加 tooltip，也不修改其他按钮。目录内的分类和商品标题继续显示。

### 24.1 改了什么

- `Assets/Scripts/UI/P8R/P8RButtonLayout.cs`：仅四枚彩色分类图进入 icon-only 分支。可见长边从原 32／40 放大 1.6 倍，成为 51.2／64 logical unit；Floor 再乘 1.2，成为 61.44／76.8。宽高等比，图案居中，并按按钮宽高限制尺寸，正常支持尺寸下每侧至少保留 8 logical unit 留白。
- `Assets/Editor/P8R/P8RColoredTabAssets.cs`：保存 prefab 时同时检查 Label 的显隐，避免只有文字状态过旧时漏存。
- `Assets/UI/P8R/Prefabs/PF_UI_P8RCatalogue.prefab`：由 Unity 更新四个 Icon 的位置／大小，以及四个 Label 的隐藏状态。对比本轮修改前备份，没有其他 prefab 字段变化。
- 对应 `P8RColoredTabTests.cs`、`P8RReferenceLayoutTests.cs`、`P8RCompleteFlowTests.cs` 更新验证契约；原单色按钮的文字、尺寸和排版逻辑保留。

本轮不重新生成或调色 PNG。原图轮廓会随显示尺寸一起变得更清晰；是否仍需加深素材轮廓，留到 Owner 看过实际效果后另行决定。

### 24.2 Test cases 与证据

先跑 RED：EditMode **7/10，通过 7 项、预期失败 3 项**，分别捕获文字仍显示、反复布局后文字仍显示、窄按钮未保留留白；真实 MainCafe PlayMode **0/1**，预期捕获文字未隐藏。没有编译错误。[EditMode RED](../TestResults/p8r-icon-only-tabs-edit-red.xml)、[PlayMode RED](../TestResults/p8r-icon-only-tabs-play-red.xml)。

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-IO-001 | 四个分类切换、禁用／恢复时保持彩色白 tint；Label 不再显示 | PASS |
| P8R-IO-002 | 80／96 高度与反复 resize，图标保持放大、居中；Floor 额外等比放大 | PASS |
| P8R-IO-003 | 36-unit 窄按钮按双轴夹限，8-unit 留白、不拉伸、不扩大点击范围 | PASS |
| P8R-IO-004 | 原单色图标仍为 32／40、原文字与位置不变；四 PNG 不变 | PASS |
| P8R-IO-005 | 真实 MainCafe 首次进入、重新启用、目录展开／收起不恢复旧文字／小图 | PASS |
| P8R-IO-006 | UI、素材、Footprint、滚轮、pinch、取餐牌与装修操作的相关回归 | PASS |
| P8R-IO-007 | 四尺寸原生截图、四分类选中状态的视觉检查 | PASS |

最终结果：[EditMode 427/427](../TestResults/p8r-icon-only-tabs-edit.xml)、[PlayMode 338/338](../TestResults/p8r-icon-only-tabs-play.xml)、[正常 Editor 原生检查 14/14](../TestResults/p8r-icon-only-tabs-native.xml)，全部通过、无跳过。

原生截图保持 production rendering：1080×1920、720×1280、480×854、1600×720，每尺寸 18 张，共 72 张。本轮 AI 视觉自检逐张检查了四尺寸的四个分类，共 16 张关键状态；其中小竖屏与横屏由独立 review agent 复核。未见分类文字残留、错位、裁切、明显锯齿或 B 底色／间距变化。Floor 仍比其他图标扁，这是原图比例，不是拉伸。两个 game UI skill 的检查重点为：可见图案居中、触控范围不变、小屏辨识度与展开／收起的一致性。

查看：[720px Furniture](../outputs/p8r-ui-polish-20260912/icon-only-tabs-ready/720x1280/04-furniture.png)、[720px Floor](../outputs/p8r-ui-polish-20260912/icon-only-tabs-ready/720x1280/05-floor.png)、[480px 小屏](../outputs/p8r-ui-polish-20260912/icon-only-tabs-ready/480x854/04-furniture.png)、[横屏收起状态](../outputs/p8r-ui-polish-20260912/icon-only-tabs-ready/1600x720/07-shelf.png)。

### 24.3 你如何 manual review

1. 打开 P8 worktree 的 MainCafe，Play → 进入装修。四个 Tab 只显示扶手椅、地板、墙面滚筒、叶子画框，按钮不应变大。
2. 依次点四个 Tab，确认能看清图案和选中底色，分类内容仍正确；不会出现 tooltip。
3. 展开／收起目录、缩放 Game View，检查图案一直居中，不被边框裁掉、不恢复文字。
4. 重点看 Floor 与其他图标是否视觉协调，以及 Furniture 在浅底／选中底色上是否足够突出。此轮未改 PNG 色彩，这部分需要你最终判断。

本轮停在 **Ready for manual review**。Owner 接受仍为 **Pending**；自动测试和截图自检不等于手机真机或 Owner 验收。没有新 Standalone build，没有 commit／push／merge，不进入下一 Phase。

## 25. C 配色与加粗分类图标（2026-09-12）

Owner 批准「C · 橄榄鼠尾草」以及比较图右侧的「再加粗一点」版本，并要求优先处理锯齿。本节替代第 24 节的底色／轮廓状态；第 24 节的 icon-only 排版、尺寸、间距和点击范围继续保留。

### 25.1 素材与接入范围

- 四个分类 Tab：未选中 `#E3E3D3`，选中／按下 `#BFC6A3`；保留 B 的可可边框 `#78675A`、圆角和阴影。Catalogue 仍是暖奶油色 `#F9F3E8`。禁用态保留既有暖灰色，不引入蓝色底板。
- 新底板在 `Assets/UI/P8R/RefinedB/CategoryTabs/`，由 `P8RCategoryTabAssets.cs` 复用 B 的几何与抗锯齿生成。它们绑定到分类组件的三个私有引用，不覆盖共享 `tab_*`；时间、范围选择、取餐点和其他按钮不跟着变色。
- 四张新图在 `Assets/UI/P8R/TabIcons/Outlined/`；上层原四张 PNG 保留，SHA-256 与原生成文件一致。新图仍是椅子、四块地板、墙面滚筒、叶子画框，独立透明 PNG，没有从比较图裁图。
- `P8RColoredTabAssets.cs` 导入并更新四个既有 Appearance 引用和 Catalogue prefab；`P8RFurnitureUiBuilder.cs`、`P8RRefinedBAssets.cs` 保证重建／旧彩色图迁移仍使用新版本。`DecorationModeTabsView.cs` 负责分类专用状态，旧 prefab 缺引用时仍有原底板 fallback。Appearance 维持 154 个唯一 key。
- 没有新增 runtime shader、Outline 特效或每帧描边；未改 Scene、滚轮、取餐牌动画、存档或游戏规则。

新图均为 1254×1254 RGBA；外部 alpha=0，主体填充主要为 254／255。已检查真正轮廓边缘的中间 alpha，不把 alpha=254 的填色当作抗锯齿证据。Unity 采用真实可见范围的 Sprite rect、Box mipmaps、Trilinear、无压缩、alphaIsTransparency；Standalone／Android／iPhone 没有覆盖这些设置。可见长边继续为 51.2／64 logical unit，Floor 为 61.44／76.8，等比居中且不扩大按钮。

| 新文件 | 最终生成文件 ID |
|---|---|
| `Outlined/tab_furniture_color.png` | `exec-8329b804-0cef-4e11-a44b-294659602ab9` |
| `Outlined/tab_floor_color.png` | `exec-0198dd3c-dee9-4706-a176-453e753d339a` |
| `Outlined/tab_wall_color.png` | `exec-3eec9e08-6da3-459f-8716-4b000a0ca60d` |
| `Outlined/tab_wall_decor_color.png` | `exec-f5b8d645-349d-4d58-a0d3-3aca0c61f10c` |

使用内置 ImageGen 的独立编辑流程，未使用 CLI／API fallback。轮廓目标参考 `exec-e4382c22-2a80-4cea-9d5b-f12736413e34` 比较图右侧。生成提示词组：

- 共通要求：Make only the existing warm cocoa-brown outline a little heavier, about 25% wider; preserve the exact icon, proportions, colors and shapes; return a PNG with genuine transparent alpha background; opaque interior surfaces; smooth antialiased edges for small UI display; no halo, shadow, blur, text or UI button.
- Furniture：warm wooden armchair, opaque ivory back and seat, small muted teal side inserts。
- Floor：four 2×2 alternating terracotta／ivory tiles, trapezoid perspective, raised front edge；内部接缝清晰、不挤满。
- Walls：cream wall swatch with central muted teal stripe, tilted ivory roller, bent stem and apricot handle。
- Wall Decor：terracotta mitered frame, opaque cream paper, three muted teal leaves and cocoa stem。
- Floor／Walls 的最后一步提示词为 background-extraction：remove the entire background; genuine transparent RGBA PNG cutout; background alpha=0; keep the object and opaque surfaces; smooth clean antialiased edge, no background pixels or shadow。

提示词中的 25% 只是生成方向，不是逐段描边的测量承诺。早期出现把棋盘格画进背景的候选，已拒绝接入；最终四图逐一核对真实 alpha，并与复制到项目的 PNG 字节完全一致。

### 25.2 Test cases 与证据

TDD 首轮 [RED](../TestResults/p8r-sage-stroke-red.xml) 为 8 项：7 个预期失败、1 个 legacy fallback 通过，捕获专用底板／引用／迁移尚未实现。独立 code review 又发现「专用底板已正确，但旧图修复发生在保存快照之前」的问题；[保存回归 RED](../TestResults/p8r-sage-stroke-save-red.xml) 精确捕获 1 项失败，另 4 项真实新 PNG 导入／边缘检查通过。将快照移到状态更新前后，[针对性 GREEN](../TestResults/p8r-sage-stroke-focused-green.xml) 为 23/23。

| Test case | 检查内容 | 状态 |
|---|---|---|
| P8R-CS-001 | C 三底板沿用 B 的 alpha 几何、边框和阴影；共享素材不变 | PASS |
| P8R-CS-002 | 四分类选中、真实按下／释放、禁用／恢复、重新启用均使用分类专用底板 | PASS |
| P8R-CS-003 | 时间／范围按钮保留共享 B 色板，旧 prefab fallback 正常 | PASS |
| P8R-CS-004 | 四图真实透明边缘、可见 rect、无压缩 mipmap 导入、原图保留 | PASS |
| P8R-CS-005 | 新图／白色 tint／无文字／等比居中；MainCafe 重新载入和重建不回退 | PASS |
| P8R-CS-006 | 已有专用底板但 icon 过旧时，修复真正保存；重复应用不改字节或修改时间 | PASS |
| P8R-CS-007 | UI、Footprint、滚轮／pinch、取餐牌与装修交互回归 | PASS |
| P8R-CS-008 | 四尺寸 production rendering 原生截图及关键分类视觉复核 | PASS |

最终自动结果：

- [P8R EditMode 280/280](../TestResults/p8r-sage-stroke-edit.xml) 与 [相邻 EditMode 157/157](../TestResults/p8r-sage-stroke-adjacent-edit.xml)，合计 437/437。
- [PlayMode 338/338](../TestResults/p8r-sage-stroke-play.xml)。
- [正常 Editor 原生检查 14/14](../TestResults/p8r-sage-stroke-native.xml)。所有结果无失败、无跳过。
- 同一 EditMode run 的 `ApplyAgain_PreservesBytesAndWriteTimesOfAllSixteenAuthoredTargets` 通过：四图及 meta、三个底板及 meta、Appearance 与 Catalogue 共 16 个目标，重复应用不改内容或修改时间。
- 测试结束后，对 [268 条保留 hash 记录](../outputs/p8r-sage-stroke-20260912/pre-apply-hashes-recovered-268.json) 再次比较，变化为 0，包含 MainCafe、原图、新 PNG 与相关保留素材。这只是已记录的子集，不宣称全项目 manifest。

原生截图保持 production rendering：1080×1920、720×1280、480×854、1600×720，每尺寸 18 张，共 72 张。视觉自检逐张覆盖四尺寸的四个分类，共 16 张关键状态；480px 小屏和横屏由独立 review agent 复核，并对重点区域无损放大检查。未发现需要返工的错位、拉伸、文字回退、白底方块、明显锯齿或 halo；Wall 外框未吞掉填色，Floor 下沿连续。480px 下滚筒把手的细节会自然柔化，不代表原画所有细线都能原样保留。

查看：[720px Furniture](../outputs/p8r-ui-polish-20260912/sage-stroke-ready/720x1280/04-furniture.png)、[720px Walls](../outputs/p8r-ui-polish-20260912/sage-stroke-ready/720x1280/06-wall.png)、[480px 小屏](../outputs/p8r-ui-polish-20260912/sage-stroke-ready/480x854/04-furniture.png)、[横屏收起状态](../outputs/p8r-ui-polish-20260912/sage-stroke-ready/1600x720/07-shelf.png)。

两个 game UI skill 的检查重点是小屏辨识度、轮廓视觉一致性、触控范围与状态隔离；因此不扩大按钮或更改整套 UI 色板，也不只靠放大 PNG 判断抗锯齿。自动测试／截图自检不等于手机真机或 Owner 验收。

### 25.3 你如何 manual review

1. 打开 P8 worktree 的 MainCafe，Play → 装修，查看四个 Tab 是否是暖鼠尾草底、可可粗轮廓和纯 icon。
2. 依次点击四个分类，确认选中颜色明显，内容正确；时间、范围选择和取餐点仍保留原配色。
3. 展开／收起 Catalogue，切换小竖屏与横屏；重点看椅子的弧线、Floor 下沿、Walls 外框／滚筒和画框斜角，有没有明显台阶、白边、黑边或发糊。
4. 按停止再 Play 重开一次，确认不会回到旧小图或旧底色。

本轮停在 **Ready for manual review**。Owner 视觉接受仍为 **Pending**；没有新 Standalone build，没有 commit／push／merge，不进入下一 Phase。

## 26. 五项 UI polish：彩色操作 icon、卡片与跨 Tab 取消（2026-09-12）

### 26.1 本轮批准范围

1. 装修入口、退出装修、Pick-up Point 按钮换成已批准的彩色图：鼠尾草／青绿色、暖木色、象牙白和可可轮廓。入口和退出仍只有 icon，Pick-up 保留文字；按钮大小不变。世界里的浮动取餐标志及四个分类 Tab icon 不改。
2. 四个 Tab 下的全部卡片，外层 `card_base` 改为暖燕麦色 `#E8D8BF`。不仅是名称栏；缩略图周围的内层空白 `card_well` 保持原色，缩略图、Paint 色块与材质不改。
3. Preview 用一条完整闭合的圆角虚线路径包住卡片，实线段从 3 增至 4 logical units；统一留白、四角衔接与间隔，带柔化边缘，不叠加原来的连续边框或四边拼接层。
4. Furniture 下 Counter、Cash Register、Coffee Machine 不再显示绿色 Using check；家具可以重复摆放。正在编辑的卡片仍有 Preview 虚线框。Floor、Wall、Wall Decor 的既有 Using check 规则不变。
5. 切到不同 Tab 时，自动取消未确认 Preview，再切换分类。已确认 Layout 与 readiness 不变；已有物件恢复正式位置／绑定，新 Preview 移除。重复点当前 Tab 不取消。Store / Exit 弹窗仍独占输入，旧拖拽和 pointer 不能继续作用到新分类。同分类选新家具、Floor 范围和墙 target 限制不扩大。

### 26.2 素材与实现

- `Assets/UI/P8R/ActionIcons/action_decorate_color.png`、`action_exit_color.png`、`action_pickup_color.png`：三张独立的 1254×1254 透明 PNG。保留原单色素材；生产导入只用 Sprite rect 裁透明留白，不改 PNG 像素。
- `P8RColoredActionAssets.cs`：只替换 Appearance 中已有的三个 cocoa 引用，保留 154 个唯一 key 及 muted／ivory 版本。复用彩色 Tab 的无压缩、Trilinear 与 mipmap 导入设置；`P8RButtonLayout.cs` 按实际可见范围居中，分类 Tab 的放大规则不套给操作按钮。
- `DecorationCatalogueView.cs`：目录打开、重开、重新启用或布局变化时，让已有 Pick-up Icon 重新读取 Appearance；只绑定图标，不重设按钮底色、文字或点击区域。首轮原生截图发现 prefab 内的旧单色引用仍在显示，新增真实 MainCafe consumer 回归先确认失败，再修正并重拍；只通过素材引用单元测试不足以完成视觉验收。
- `P8RRefinedBAssets.cs`：仅卡片外层颜色修改；`P8RRoundedDashGraphic.cs` 与 Catalogue builder：管理一条带 AA 的圆角虚线路径。
- `DecorationModeController.cs`、`DecorationTouchRouter.cs`、`P8REnglish.json`：跨分类 Cancel、指针隔离、Furniture check 与提示文字同步。没有增加 Save 数据字段，不需要存档迁移。
- Project Design、Phase 7 Spec、Phase 8 Spec 已同步这一条获批行为变更；旧章节的测试结果作为历史保留，不再把旧的“不可切 Tab”描述当成现行规则。
- Review 另外补强了两个边界：样式迁移失败后同时恢复内存里的 Image／Selectable／geometry、文件内容和 Scene clean 状态；跨 Tab 后保留旧输入来源直到 release／cancel／空帧被处理，下一次触摸不会被吞。Scene clean 恢复使用 Unity 6000 的 `ClearSceneDirtiness(Scene)` 精确反射并带版本检查；将来升级 Editor 时需重跑 rollback 测试。

本轮图像使用内置 ImageGen 从已批准的概念图提取／重绘，未使用 API／CLI 图像 fallback。最终指令记录如下：

- Exit：`Make the background transparent. Keep only the door and arrow, unchanged.`（前一轮以批准图左上方木门／青绿右箭头为参考，保留造型、配色、轮廓和完整留白。）
- Decorate：`Extract the large paint roller icon in the TOP MIDDLE as one standalone icon on a transparent background. Preserve its approved teal roller, cocoa outlines, metal shank and wooden handle exactly. Remove everything else: board, cream background, text, other icons and the small button copy. Center the complete paint roller in a square with transparent padding. Same design, no additions, clean antialiased edges. Only this one roller, transparent PNG.`
- Pickup：`Extract the large TOP RIGHT coffee cup, saucer and downward teal arrow as one standalone pickup-point icon on a transparent background. Preserve this exact approved ivory cup, coffee, honey saucer, teal down arrow and cocoa outlines, same proportions and shading. Remove the board, text, cream background, other icons and the small button copy. Center the complete cup+saucer+arrow in a square with transparent padding. Same design, no additions, clean antialiased edges. Only this one pickup icon, transparent PNG.`

### 26.3 验证与 manual review

当前本轮状态（2026-09-13）：**Ready for manual review**。实现、自动回归与截图自检完成；Owner 和手机真机验收仍 **Pending**，不等于整个 Phase 或所有设备已验收。

最终验证结果：

| 验证 | 完成结果 | 证据 |
| --- | --- | --- |
| EditMode 相关分组回归（历史，不是全部 EditMode tests） | **683/683 PASS**，0 failed／skipped | [最终 EditMode XML](../TestResults/p8r-five-edit-ready.xml) |
| PlayMode 相关分组回归（历史） | **585/585 PASS**，0 failed／skipped；其中跨 Tab 矩阵 **39/39 PASS** | [最终 PlayMode XML](../TestResults/p8r-five-play-ready.xml) |
| 原生 Editor 截图流程与真实 Scene 图标消费 | **15/15 PASS**，0 failed／skipped | [最终 Native XML](../TestResults/p8r-five-native-final.xml) |
| 四尺寸截图视觉复核 | **80/80 已检查**；1080×1920、720×1280、480×854、1600×720，各 20 张 | [最终截图目录](../outputs/p8r-ui-polish-20260912/five-polish-final/) |
| 重复应用与保留范围 | 三个实际应用入口再次执行：15 个目标文件 **0 字节／时间戳变化**；最终 268 项保护子集 **0 hash 变化** | [验证摘要](../outputs/p8r-five-polish-20260912/verification-summary.json) |

本轮 test case 完成记录：

| Case | 检查范围 | 状态 |
| --- | --- | --- |
| P8R-P5-001 | 三个彩色操作 icon 的透明边、统一导入与 154 个 Appearance key；真实 MainCafe 的进入／重开／重新启用／窄高布局；正常、按压、确认后仍使用新 Pickup 图标，按钮大小与文字不变 | **PASS — 完成** |
| P8R-P5-002 | 四个 Tab 的外层卡片统一为 `#E8D8BF`；内层 `card_well`、缩略图与材质保持原样 | **PASS — 完成** |
| P8R-P5-003 | 4-unit 圆角闭合 Preview 虚线、统一 inset／间距／角部、AA；旧连续边框与四边拼接层停用；不拦截点击 | **PASS — 完成** |
| P8R-P5-004 | Furniture 的 Counter／CR／CM 不显示 Using check，Preview 虚线保留；其他 Tab 的既有 Using check 规则不变 | **PASS — 完成** |
| P8R-P5-005 | 39 项跨 Tab 测试：不同 Tab 自动 Cancel、当前 Tab 不取消、Store／Exit 弹窗独占输入、所有 preview family、承载物与设备绑定恢复；confirmed Layout／readiness 不变；旧拖拽结束／取消／空帧后下一次触摸正常 | **PASS — 完成** |
| P8R-P5-006 | 样式迁移失败可恢复 Scene 内存属性与文件、重跑入口幂等；MainCafe、内层 card well、原图、分类 icon 与世界素材保护子集未改动 | **PASS — 完成** |
| P8R-P5-007 | 原生 Overlay 四尺寸下的 HUD、四个 Tab、卡片／虚线、浮动操作按钮、弹窗与 Pickup 正常／按压／重开／确认状态截图检查 | **PASS — 完成** |

截图 review 入口：

- [720 竖屏：Furniture 与 Preview 虚线](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/13-return-without-explanation.png)、[Floor](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/05-floor.png)、[Walls](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/06-wall.png)、[Wall Decor](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/19-wall-decor-cards.png)。
- [日常 HUD／装修入口](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/01-hud-1x.png)、[Pick-up 正常状态](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/04-furniture.png)、[按压状态](../outputs/p8r-ui-polish-20260912/five-polish-final/720x1280/10-pickup-pressed.png)。
- [480 小竖屏](../outputs/p8r-ui-polish-20260912/five-polish-final/480x854/13-return-without-explanation.png)、[1080 竖屏](../outputs/p8r-ui-polish-20260912/five-polish-final/1080x1920/13-return-without-explanation.png)、[1600 横屏](../outputs/p8r-ui-polish-20260912/five-polish-final/1600x720/13-return-without-explanation.png)。

截图使用真实 GameView 与原有 `ScreenSpaceOverlay`，没有替换为离屏 Camera 或改变生产 Quality 设置；临时 GameView 尺寸在结束后恢复。主检查与独立复核覆盖全部 80 张，并用实际尺寸 PNG 局部检查图标透明边、按压状态和卡片四角，未发现未处理的可见问题。小分辨率仍有正常的像素采样软化，不承诺任何尺寸都完全无锯齿。1600×720 的 Wall 截图中，底部卡片四角位于正常滚动视口之外；完整四角另外在竖屏检查。

首轮 `p8r-five-native.xml` 的 14/14 是历史结构检查：截图复核发现旧 Pickup prefab 引用仍显示单色，随后补真实 Scene 测试、修复并重拍。本节仅以 **native-final 的 15/15 与 five-polish-final** 作为最终证据，不以首轮替代最终结果。保护 hash 清单是 268 项子集，不是全项目文件清单。

两个 game UI skill 在本轮用于检查小屏可读性、统一的卡片状态、触摸边界与弹窗输入隔离；没有借此扩大布局、配色或游戏系统改动范围。

Manual review 清单：

1. 日常 → 装修 → 退出；查看滚筒、门箭头与 Pick-up 按钮的新图是否清楚、居中，按钮和文字尺寸未改变。世界里的浮动取餐标志、四个分类 icon 不变；禁用状态仍采用原 muted 素材，ivory 引用保留。
2. 依次打开 Furniture、Floor、Walls、Wall Decor；外层卡片统一暖燕麦色，缩略图内层的浅色背景没有被染色。
3. 四个 Tab 各选择并展开一张正在 preview 的卡片，在小竖屏和横屏看完整四角、虚线粗细与包裹位置。放置足够柜台，分别确认 Counter、Cash Register、Coffee Machine 后再打开目录，三类均无绿色勾；Floor／Wall／Wall Decor 的既有 Using check 规则不变。
4. 修改 Floor 后不按确认，直接切 Furniture：地板恢复正式样式，顶部已确认 readiness 不变；切回不恢复已取消的 preview。人工抽检普通家具新建／移动、Floor Whole Room／Single Grid、选墙后的 Wall、Wall Decor，以及 CR／CM／Pick-up 的合法／非法 preview；自动化完整矩阵见本节结果。
5. 重复点当前 Tab，Preview 应保留；打开 Store／Exit 弹窗时不能点穿后面的 Tab，Store Cancel／Exit Continue 应返回同一个 Preview，之后切其他 Tab 才取消。按住拖拽途中用另一个手指切 Tab，旧手指抬起不能在新分类误放物件，下一次触摸应立即有效。
6. Confirm 一个更改后再切 Tab，已确认结果不能回滚；取消移动承载设备或取餐点的 Counter 后，台面内容应回到正式位置，readiness 不被本次 Cancel 重新发布。

本轮停在 **Ready for manual review**，不自动判定 Owner 接受；没有新 Standalone build，没有 commit／push／merge，不进入下一 Phase。

## 27. 手机触控尺寸、范围彩色图标与顶部必要提示（2026-09-13）

本轮已获 Owner 批准，**UI 实现、相关自动测试与截图自检已完成；测试副作用恢复核查待授权，整体尚未 Ready for manual review**。不改变 Refined B 配色、素材轮廓方向、Footprint、世界取餐标志、Preview／Confirm／Cancel、跨 Tab 自动取消及存档合同。

### 27.1 实施合同

- Whole Room 与 Single Grid 使用新的彩色 PNG，保持横排 icon＋文字；selected 仍显示彩色，不能因不可重复点击而退回旧单色图。
- 手机普通按钮至少 48 平台 logical units，主要操作 48–56；正文 16–18，卡片名称至少 14。Android dp、iOS pt、Unity Canvas units 与截图 pixels 分开记录，不能互相冒充。
- 保留 CanvasScaler 的 1080×1920 / Match .5；按实际 viewport 转换目标尺寸。Editor 使用明确的手机／平板 profile，不读取电脑屏幕 DPI。无法读取手机 viewport 时标为 Estimated，不当作真机尺寸证据。
- 整排按钮重新留出间距。窄屏／横屏采用换行、紧凑排列及滚动，不缩小到手指难以点击，也不依靠重叠的透明 raycast padding。
- “Select a floor grid to edit.” 等必要指引放在顶部中央、实际 HUD／readiness 下方并位于 Safe Area 内；16–18 字号、最多两行、按内容高度布局。等待选取时持续显示，目标选好后消失；弹窗覆盖时隐藏，仍有需要时恢复，不拦截触控。
- 不恢复已移除的 Current Preview／general error explanation；confirmed-layout readiness 是独立信息，不与操作指引混在一起。纵向目录仅在内容溢出时显示轻量滚动提示。

### 27.2 验证状态

已完成修复前 RED：尺寸 helper 23/23 因缺少功能失败；新范围图标 11/11 因缺少 helper 失败；真实 Scene 排版 3/3 捕获按钮过小、弹窗按钮过矮和提示仍依附底部栏。以上是缺陷证据，**不是最终通过结果**。

初次未筛选的 EditMode 基线运行 600 秒后超时，没有有效完成报告；不得把该次运行计为 PASS。旧素材 builder 意外重写了 23 个 Phase 7 材质文件及 AssetPipelineReadability、Phase7InteriorWalls 两个验证场景。MainCafe 已从 hash 精确匹配的备份恢复；其余恢复核查被权限检查阻止，已单独请求 Owner 授权。无法确认原始版本的文件不覆盖。恢复未核对完毕前，不宣布 Ready for manual review。

阶段证据：[78/78 相关 EditMode](../TestResults/p8r-mobile-edit-final.xml) 通过；[14/14 手机排版针对性测试](../TestResults/p8r-mobile-visual-fixes-final.xml) 通过。UI authoring 重建前后 634 个 P8R UI 文件的集合 hash 一致，范围图标重复接入不改变 5 个目标文件的 bytes 或修改时间，记录见 `outputs/p8r-mobile-ui-20260913/authoring-verification.json`。这是明确范围的检查，不是全项目文件保护证明。

第一轮较广 PlayMode 为 [593/601 PASS](../TestResults/p8r-mobile-play-check1.xml)，8 项失败在本轮继续处理，不计为通过。包含旧的仅竖排测试假设、错误的跨 Canvas HUD 查找，以及反馈移到顶部后需要更新的测试引用；原 Confirm／Undo／正式布局断言保留。Store 已打开后缩窄 Safe Area 的 [RED](../TestResults/p8r-mobile-store-inset-red.xml) 确认 card 左边越界，随后补子安全区尺寸通知。

这 8 项及新边界随后在 [11/11 针对性检查](../TestResults/p8r-mobile-boundary-check2.xml) 通过。短横屏另补真实有效点击面积检查：卡片与 viewport 取交集后至少 48×48 logical，并检查实际 raycast；覆盖 800×360、640×360、569×320 logical 与不对称 Safe Area。等待选 Floor grid 且高度不足时，范围选择改在卡片右侧；极短屏省去重复 Flooring 小标题，切分类／开始实际 Preview 后恢复原 footer。首轮检查发现展开动画写回旧 footer 位置，修正后 [16/16 手机针对性检查](../TestResults/p8r-mobile-short-landscape-check2.xml) 通过。

`mobile-check2` 原生截图检查为 [1/1 PASS](../TestResults/p8r-mobile-native-check2.xml)，四尺寸共 100 张，但逐张视觉 review 发现 Safe Area 变化后的 HUD／readiness／instruction 联动遮挡，**因此不是最终视觉 PASS**。随后修正了小屏卡片名称末尾截断、取餐标志被动作按钮遮挡，以及宽屏 Return 占用整行卡片空间。收起目录的四个 Tab 按实际标题显隐计算位置；新建家具和移动已有家具两条路径均加入居中断言，不修改旧状态 API。

最后截图还发现无 Preview 的单排 Floor 目录填满多余空白，常规手机等待选格时只剩约 4–6 logical 的场景缝。[新增 RED](../TestResults/p8r-mobile-empty-space-red.xml) 复现后，仅将该目录高度限制为 header＋完整 156-logical 分类行＋原 footer／间距，不缩卡片或按钮。[8/8 针对性检查](../TestResults/p8r-mobile-empty-space-green.xml) 通过；有 Preview、返回编辑、多分类及短横屏侧栏规则不变。

最终证据（范围重叠，不将数字相加；所有下列测试 failed／skipped 均为 0）：

- [78/78 EditMode](../TestResults/p8r-mobile-edit-final.xml)：本轮 UI 素材、尺寸换算、图标采样和 authoring 合同。
- [603/603 较广 PlayMode](../TestResults/p8r-mobile-play-final.xml)：相关 P6–P8 场景、Touch／Camera、Surface、设备、Preview 与 UI 回归。运行在最后的 Tab 水平居中和单排 Floor 空白收紧之前，不冒充最后代码的全量重跑。
- [63/63 最后受影响 UI 回归](../TestResults/p8r-mobile-ui-final3.xml)：最后两项纯排版修正后，完整重跑 8 个相关 UI test groups，包含新增场景空间测试。
- [1/1 原生 Game View 截图检查](../TestResults/p8r-mobile-native-final3.xml)：最终 `mobile-final3`，四种 profile 各 26 张，共 **104 张**。使用正常 Editor Game View／ScreenSpaceOverlay，不替换 Camera RenderTexture、不降低正式渲染设置。
- 截图自检采用完整基线 review＋每轮 SHA 差异重新目视，未变化画面沿用已审证据；范围图标另做原像素边缘检查。最终普通竖屏已释放选格场景空间，四 Tab 居中，模拟 SafeArea 下提示／readiness／弹窗无新增重叠。见下方极小屏限制，不等于全设备无问题。

两个 skill 的作用：**game-ui-design** 用于信息层级、彩色 icon 一致性和小尺寸可读性；**game-ui-ux** 用于 logical touch targets、响应式重排、SafeArea、实际 raycast 和弹窗输入所有权。没有因此重画整套 UI 或改变游戏事务。

| Case | 本轮检查内容 | 工程检查状态 |
|---|---|---|
| P8R-MOBILE-01 | 两个彩色范围 icon、选中态仍彩色、PNG 导入与抗锯齿设置、重复 authoring 不改文件 | 完成／PASS |
| P8R-MOBILE-02 | logical viewport 换算、明确 Editor profile、48–56 按钮尺寸与间距 | 完成／PASS；真实 dp／pt 待设备验收 |
| P8R-MOBILE-03 | 必要提示位于实际 HUD／readiness 下方、等待时持续、选取后消失、不拦截输入 | 完成／PASS |
| P8R-MOBILE-04 | 顶部 UI、展开／收起目录、两个家具入口的分类居中与返回编辑 | 完成／PASS |
| P8R-MOBILE-05 | 完整两行卡片名称、范围按钮换行、实际溢出才显示滚动提示 | 完成／PASS |
| P8R-MOBILE-06 | 常规 320／360 logical 手机单排 Floor 留出场景；短横屏卡片裁切后仍有至少 48×48 有效点击区 | 完成／PASS；极小 SafeArea 限制见下文 |
| P8R-MOBILE-07 | 不对称 SafeArea、父级位置变化、已打开 Store 后缩窄安全区、弹窗不点穿 | 完成／PASS（模拟） |
| P8R-MOBILE-08 | 取餐标志与动作栏位置；原 bob／Footprint／Confirm／Cancel／跨 Tab 规则 | 完成／PASS（相关回归） |
| P8R-MOBILE-09 | 四 profile 最终 104 张截图与独立差异复核 | 完成；不是 Owner acceptance |
| P8R-MOBILE-10 | Android／iOS 编译、真机触控、刘海／系统栏、Owner manual acceptance | Pending，未执行 |
| P8R-MOBILE-11 | 初次基线意外重写的 23 材质与 2 验证场景恢复核查 | Pending，等待 Owner 授权 |

Android／iOS 编译、设备触控与真实 dp／pt 验收仍 Pending；本机只安装 Windows Editor module，不安装新 package/module，不制作 Player build。没有 commit／push／merge。

图像通过内置 ImageGen 按批准的 motif 重绘成生产 PNG，并非像素级提取。完整 prompts 与来源记录在 `outputs/p8r-mobile-ui-20260913/icon-prompts.json`；最终素材位于 `Assets/UI/P8R/RangeIcons/`，保留原始 PNG bytes。

### 27.3 实现位置与实际尺寸

- `Assets/UI/P8R/RangeIcons/`：两张 Whole Room／Single Grid 彩色透明 PNG；`P8RColoredRangeAssets.cs` 只更新 Appearance 中两个既有 key。原图、其他颜色状态与 154-key 合同保留。Trilinear、mipmap、无压缩及可见 Sprite rect 用于小尺寸边缘采样，不用运行时描边叠加。
- `P8RMobileMetrics.cs` 与 `Assets/Plugins/iOS/P8RMobileViewport.mm`：平台 logical viewport 换算、Editor 明确 profile、Estimated fallback；不拿电脑 DPI 估算手机。Native 查询缓存，尺寸／配置／焦点变化才重新获取。
- `TimeControlPanel`、`P8RButtonLayout`、`DecorationFloorRangeView`：按钮至少 48 logical，常规文字 16–18，图标与文字横排居中；utility 空间不足时整行重排。
- `DecorationCatalogueView`、`DecorationCatalogueTileView`、`P8RSurfaceFooterLayout`：短竖屏分类与折叠按钮同排，省去重复 Catalogue 标题；宽屏 Return 进入标题行；收起时四 Tab 居中。无 Preview／无 Return 的单排 Floor 目录按内容收紧空白，短横屏必要时将范围按钮放在卡片右侧。卡片 104×124、名称 14 logical，名称区域支持完整两行；缩略图和内底色不变。纵向 overflow 提示只在实际溢出时出现。
- `DecorationActionBarView`、`DecorationModeController`：必要指引独立放在实际 HUD／readiness 下方；由既有事件联动目录空间。浮动操作栏避开 HUD、readiness 和 support／取餐牌稳定范围，不跟随取餐牌 bob 抖动。
- `ValidationMessageView`、两种装修 modal view 与 `P8RModalSafeAreaHost`：Safe Area 移动／缩窄后重新排列；readiness 只在几何真正改变时发送既有事件，父级移动不逐帧重建文字。弹窗全屏遮罩与输入所有权不变。

截图与测试使用以下**显式 Editor profile**；第二列不是从照片或电脑 DPI 猜出的真机数据：

| Render pixels | Editor logical viewport | 检查用途 |
|---|---|---|
| 1080×1920 | 360×640 | 常规手机竖屏 |
| 480×854 | 320×569.33 | 最小竖屏与低像素密度边缘 |
| 1600×720 | 800×360 | 短横屏重排与滚动 |
| 1536×2048 | 768×1024 | 平板宽度上限与安全区联动 |

`INJECTED` 文件名表示在测试 Scene 实例中模拟不对称 Safe Area，并非带刘海设备的实测。真实触控有效面积还受滚动视口裁切影响，不能仅凭 RectTransform 的宽高宣称所有卡片在所有屏幕均有完整 48 logical 可见点击面积。

已知限制：320×569.33 logical 的最小 profile 再叠加较大不对称 SafeArea 时，range 需要两行，展开目录与顶部指引之间仍没有足够选格空间；需点右侧箭头折叠目录再操作场景。该情形保留真实点击尺寸与原有折叠行为，不声称无需折叠。短横屏也需滚动查看完整卡片名称。应在目标手机上判断这项取舍是否接受。

最终截图示例：[普通手机的提示＋范围图标](../outputs/p8r-mobile-ui-20260913/mobile-final3/1080x1920/21-single-grid-instruction.png)、[最小手机](../outputs/p8r-mobile-ui-20260913/mobile-final3/480x854/21-single-grid-instruction.png)、[短横屏 SafeArea／侧置范围按钮](../outputs/p8r-mobile-ui-20260913/mobile-final3/1600x720/22-INJECTED-safe-area.png)、[已打开弹窗再缩窄安全区](../outputs/p8r-mobile-ui-20260913/mobile-final3/1080x1920/26-INJECTED-open-store-resize.png)。完整目录为 `outputs/p8r-mobile-ui-20260913/mobile-final3/`。

### 27.4 Manual review 清单（尚不代表 Owner 已接受）

1. 在 P8 worktree 打开 MainCafe，Play，确认 Normal／Decoration、时间按钮和装修入口大小协调，速度切换与装修暂停规则不变。
2. 打开 Floor，查看 Whole Room／Single Grid 的彩色图与文字；点 Single Grid 后，顶部必要指引清楚可见，选择格子后消失。等待选墙时也应如此。
3. 预览中展开目录、返回编辑、切不同 Tab、重复当前 Tab，确认既有自动取消／保留规则不变；Store／Exit 弹窗不能点穿。
4. 小竖屏检查所有固定按钮能点到、没有互相重叠；观察卡片完整名称、纵向滚动提示与横向相邻卡片露出。短横屏需要额外检查滚动视口实际可操作性。
5. 移动带 Pick-up Point 的 Counter，检查动作按钮不挡彩色标志，标志 bob 不带着按钮抖动；未改变世界牌子本身。
6. 窗口／方向／安全区改变前后检查 HUD、readiness、必要提示、目录和弹窗；弹窗已打开时也要检查。手机真机需另行确认字体、手感、刘海／系统栏以及 Android dp／iOS pt。

当前停在 **UI 实现／相关自检完成，测试副作用恢复核查待授权**。恢复核查完成前不标记整体 Ready for manual review；真机和 Owner acceptance 继续 Pending。没有 commit／push／merge，不推进 Phase。

## 28. 紧凑 UI 与滚轮分区（2026-09-13）

Owner 在查看第 27 节手机尺寸结果后批准本轮收紧：可见控件约为上一版的 70% 宽高（面积约一半），文字只适度缩小；不是把 Canvas、字体、触摸范围全部减半。**实现与限定工程验证完成；严格全状态 45% 限高仍为 PARTIAL。** 第 27、28 节保留历史证据；后续经批准的尺寸与正常提示隐藏规则以第 29 节为准。

- 四个 Tab 普通浏览目录以可用 Safe Area 高度的 45% 为目标上限；内容少时更矮，内容多时滚动浏览。部分带 Preview 的受限尺寸尚未达到严格 45%，见下面的明确例外，不代表 Owner 已接受例外政策。
- 按钮底板和 icon 缩小，保留至少 48 logical units、不相互重叠的点击区域。卡片名称保持至少 14 logical、两行；不重新绘制 PNG 或修改抗锯齿导入设置。
- 鼠标位于已显示的 confirmed-layout readiness 提示卡时，滚轮只属于该卡，不能同时缩放场景；即使滚到末尾或文字无需滚动，也不能漏给 Camera。鼠标回到场景才缩放，弹窗打开时继续阻止场景输入。
- 保留既有目录输入规则：鼠标在目录上滚轮仍缩放 Camera，目录不被滚轮带动；目录用原有拖拽方式滚动。不要把这项和提示卡的独占滚轮混淆。
- Refined B 配色、现有彩色图标、卡片内外底色、Preview 虚线、Furniture 无 Using check、自动取消未确认 Preview、Confirm／Cancel、存档和世界取餐牌均不变。

### 28.1 验证记录

修改前相关 PlayMode 基线 [63/63 PASS](../TestResults/p8r-compact-baseline.xml)。最后生产代码的 [92/92 UI／Input PlayMode](../TestResults/p8r-compact-final2.xml) 全部通过，failed／skipped 均 0、exit 0；覆盖提示卡滚轮隔离、真实48点击面积、宽窄重排、时间icon状态、modal、Preview与回退边界。本轮只选择 UI／input 测试，没有执行未筛选的旧素材 builder。

最终原生截图检查 [1/1 PASS](../TestResults/p8r-compact-native-final.xml)，failed／skipped 均0、正常Editor exit0。`compact-check2` 保存四个常规profile各26张，再加1138×640最短横屏边界一张，共105张。使用真实 Game View／ScreenSpaceOverlay，不替换Camera RenderTexture、不修改画质；测试与截图范围重叠，数字不相加。

关键 RED 证据：[初始输入／尺寸缺陷](../TestResults/p8r-compact-red.xml)（33项中含2个后来修正的测试初始化错误，不能全计为产品缺陷）；[最短横屏详情卡重叠](../TestResults/p8r-compact-boundary6.xml)；[Undo图标节点为空](../TestResults/p8r-compact-icon-red.xml)。首次补充截图因测试 namespace 引用遗漏编译失败，已修正；首轮成功截图又发现空按钮，因此两者均不冒充最终验收。最终结果以上述 final2／native-final 为准。

| Case | 本轮验收点 | 状态 |
|---|---|---|
| P8R-COMPACT-01 | 提示卡滚动／边界／无需滚动、场景缩放、目录原规则、modal 隔离 | 完成／PASS |
| P8R-COMPACT-02 | 四个 Tab 的 Safe Area 45% 限高、溢出滚动、Return／范围／确认取消可操作 | PARTIAL：普通浏览与控件回归PASS，28.2所列Preview限高例外待决定 |
| P8R-COMPACT-03 | 可见控件约 70%、48 logical 点击区、两行可读卡片名称、无重叠 | 完成／PASS（限定profile；短横屏需滚动看完整卡片） |
| P8R-COMPACT-04 | 四尺寸真实 Overlay 截图、模拟 Safe Area、文字与 PNG 边缘自检 | 完成／限定视觉PASS；限高例外与Owner acceptance不计为完成 |

先前 Phase 7 材质／验证场景的恢复核查仍是单独待授权事项，本轮不检查或覆盖该范围；不将 UI 验证等同于全项目 Ready。Android／iOS 真机触控和 Owner acceptance 仍待单独验收。无 commit／push／merge 或 Player build。

两个 UI skill 的实际作用：`game-ui-design` 用于可见层级、图标／字体辨认和disabled表现；`game-ui-ux` 用于分离可见底板与touch root、Safe Area重排、滚轮输入所有权及实际raycast。没有因此另起一套视觉风格。

视觉复核采用首轮105张全量目视＋最终34张SHA差异逐张复核，其余71张bytes相同沿用已审证据。两位独立reviewer检查两种手机、平板和最短横屏边界；主agent检查短横屏并做原像素图标裁切检查。Undo／Apply All空按钮finding已关闭，未发现新增重叠／裁字；最低像素密度的斜线仍有轻微阶梯与柔化，不影响辨认，不称为完全无锯齿或真机验收。严格45%例外继续PARTIAL。

截图入口：[普通手机 Furniture](../outputs/p8r-mobile-ui-20260913/compact-check2/1080x1920/04-furniture.png)、[短横屏 Floor 工具](../outputs/p8r-mobile-ui-20260913/compact-check2/1600x720/05-floor.png)、[最小手机 Floor 限高例外](../outputs/p8r-mobile-ui-20260913/compact-check2/480x854/05-floor.png)、[最短横屏展开详情与 Floor Preview](../outputs/p8r-mobile-ui-20260913/compact-check2/1138x640/27-INJECTED-expanded-readiness-floor-preview.png)。

### 28.2 实际改动与限高例外

- `DecorationModeController.cs`、`MouseCameraInput.cs`：装修／日常两个 Camera 输入路径都隔离 readiness 滚轮，保持原灵敏度与目录行为。
- `TimeControlPanel.cs`、`P8RButtonLayout.cs`、`DecorationActionBarView.cs`：常见底板 34 logical、装修入口 40；独立透明 touch root 至少 48，图标通常 20，时间 1x 为 18、2x 为 26，状态刷新不会把尺寸改回旧值。卡片改为 76×96，标题 16，正文／卡片名称 14，名称保持两行。
- `ValidationMessageView.cs`：展开详情通常最高 112；短横屏最高 64，Safe Area 高度不足 320 logical 的短横屏收至 52。文字完整保存在可滚动内容中。新增真实场景检查曾复现 1138×640、2x、注入 Safe Area 时详情与 Floor Preview 面板重叠 16 pixels；本轮针对该尺寸收紧详情上限，保留 48 点击区。
- `DecorationCatalogueView.cs`、`P8RSurfaceFooterLayout.cs`：按实际 header／footer／卡片尺寸限高，短横屏可将既有范围工具放到侧边。浏览时 Pickup 可进入 header；有 Return 时两者分开，Pickup 回到 footer。没有额外添加 Floor Return。

尚未符合严格 45% 的情形包括：普通最小手机 480×854（320×569.33 logical）的 Floor Preview，截图面板仍约占整屏 63%，叠加窄 Safe Area 后也存在限制；Safe Area 高度不足 360 logical 的部分短横屏 Surface Preview；普通 640×360 横屏的 Furniture Preview 重新打开目录，同时显示 Return 与 Pickup。最后一种至少需要 192 logical（52 header＋64 footer＋76可用内容），超过 162 的限高目标；Wall Decor 没有 Pickup，不套用该计算。

这些情形暂时保留不低于实际控件所需的面板高度，因此仍可能需要折叠目录后操作场景；不是全状态限高完成。是否接受例外，或另行重排 Preview 工具，需要 Owner 看截图后决定。两行 Floor 工具现为 100 logical，不再沿用旧报告的 102。PNG、导入抗锯齿设置与卡片内外底色没有修改。

第一轮原生 105 张截图中发现短横屏 Undo／Apply All 变成空按钮，因此 `compact-check1` 不计为最终视觉 PASS。原因是旧按钮仅有 Label，紧凑 icon-only 布局隐藏文字时没有 Icon 节点。新增真实场景 RED 复现后，只在 runtime 创建／复用各一个非 raycast Icon，使用既有 PNG；宽屏仍回到文字版，并覆盖禁用状态及同实例宽窄往返不重复节点。最后回归与截图结果见 28.1。

### 28.3 本轮 manual 检查

1. Play MainCafe，在 Normal 和 Decoration 下展开 readiness，滚动详情到顶部／底部，确认 Camera 不动；移到场景再滚，确认缩放灵敏度不变。
2. 四个 Tab 依次打开，看缩小后的图标、完整两行卡片名称、内层缩略图底色；拖拽目录浏览后排内容。
3. 家具 Preview 时重新打开目录，点击 Return、Pickup 与四个 Tab，确认不抢点击；跨 Tab 仍取消未确认 Preview。
4. 对照普通竖屏与短横屏截图，判断可见尺寸是否合适；受限 Preview 状态单独评价面板高度取舍。手机触感仍需真机确认。

## 29. 按钮间距、目录尺寸与正常提示隐藏（2026-09-13）

Owner 确认这五项小范围修改；继续使用 Refined B 配色和现有 PNG，不改 Scene、Prefab、字体资产、Save 或 Preview 交易规则。只调整显示与排列，不推进 Phase。

### 29.1 当前显示规则

- 时间按钮：可见底板保持 34 logical，三个 48 logical touch root 相邻排列；可见间隔由 22 收至 14，不重叠点击范围。
- 四个 Tab：恢复四个相邻、等宽的长方形底板，横向填满各自占位，高度 34；保持纯 icon、原配色，Tab 间沿用 4／6 logical 间距。窗口宽度改变后底板跟随占位，不再出现分散的小方块。
- 所有分类卡片：76×96 → 68×84，category heading 16 → 14，caption 14 → 13 logical。只降低名称字号，保留完整两行文字区域；缩略图周围浅色底、卡片底色与 Preview 虚线不变。字体变小一档是本次 Owner 批准的密度取舍，不继续声称名称至少 14；真机可读性仍需人工判断。
- 家具尺寸文字：只在 UI 将已有 ASCII `1 x 2` 完整放到第二行，例如第一行 Counter、第二行 1 x 2；不变更 ItemId、模型名称或 font asset。
- 家具／Wall Decor 浮动操作：底板 34 → 32，48 logical touch root 不变，取消额外 4 logical 排列间隔；可见空隙由 18 收至约 16。时间与浮动操作仅保留 1/64 logical 的亚像素保护间隙，防止 Canvas 换算后的边界微量相交，没有缩小点击区或放宽测试。
- 已确认布局没有任何 error／warning 且可营业时，整条 readiness 卡隐藏，释放布局占位和输入拦截；最新诊断报告仍保留。有 warning（即使可以营业）或 blocking 时仍显示；恢复正常立即隐藏。

### 29.2 本次验证与 manual review

状态：这五项修改的实现、限定回归与关键截图自检完成；等待 Owner 对大小、间距和字号的 manual review，不等于全项目 Ready 或真机验收。

RED：[p8r-spacing-red.xml](../TestResults/p8r-spacing-red.xml)，22 项中 11 项通过、11 项按预期失败，复现旧间距、旧卡片尺寸和健康状态仍可见。

最终限定 PlayMode：[p8r-spacing-green3.xml](../TestResults/p8r-spacing-green3.xml)，**117/117 PASS**，failed／skipped 均 0、Unity CLI exit 0。覆盖四类真实卡片尺寸与完整文字、时间／浮动操作的实际间隔、48 logical 非重叠点击区、Tab 宽窄重排、Preview／跨 Tab 取消、滚轮所有权、modal 与 readiness 的健康→问题→健康切换。健康隐藏不会吞点击或滚轮，重复健康报告不会重复发布布局事件；保留诊断报告。早期 green1／green2 不是最终通过证据。

最终原生截图测试：[p8r-spacing-native1.xml](../TestResults/p8r-spacing-native1.xml)，**2/2 PASS**，failed／skipped 均 0、正常 Editor exit 0。四种 profile 各 26 张，加最短横屏边界 1 张，共 105 张；另保存真实三柜台达到可营业状态的 1 张，共 106 张。真实 Game View／ScreenSpaceOverlay 捕获，不替换 RenderTexture、不修改画质。截图测试与上述回归有覆盖重叠，数量不相加。

本轮逐张目视抽查 **42 张不同的关键画面**，不是声称 106 张全部目视：四种 profile 各检查 HUD、Furniture、Floor、Walls、Shelf、家具 Preview、Return、四操作按钮、墙饰卡片及模拟 Safe Area 共 10 张，另加最短横屏详情边界与健康无提示图。两位独立 reviewer 参与手机／平板和代码差异复核；主 agent 检查横屏、边界、健康状态并复核疑似截图异常。未确认本轮新增的控件互盖、丢失或卡片排字截断。平板两张曾被标记为显示不完整，经重新读取原始 PNG 后 HUD／四 Tab 完整，未因此修改产品代码。

| Case | 本轮验收点 | 状态 |
|---|---|---|
| P8R-SPACING-01 | 三个时间按钮等距收紧，原速度／暂停规则不变 | 完成／PASS |
| P8R-SPACING-02 | 四 Tab 相邻等宽矩形、纯 icon，宽窄／展开收起后仍正确 | 完成／PASS |
| P8R-SPACING-03 | 所有 Tab 的 68×84 卡片、heading 14／caption 13，名称完整、缩略图内底色不变 | 完成／限定自动化 PASS；真机字号待 Owner 判断 |
| P8R-SPACING-04 | 家具／墙饰底板 32、间距约 16，48 logical 点击区非重叠 | 完成／PASS |
| P8R-SPACING-05 | 无 error／warning 且可营业时隐藏；出现问题再显示；恢复正常再次隐藏 | 完成／PASS，含真实三柜台截图 |

显示边界仍需注意：短横屏与最小手机的部分滚动视口只能露出卡片上半，需拖动内容看全文，不是名称自身排版丢字；抽查的墙饰起始滚动位置没有展示 Tall Glass Window，其两行完整性由全部真实卡片的自动化检查覆盖，未冒充截图全文验收。最低像素密度仍可能看到轻微斜线阶梯，本轮没有重绘 PNG 或调整抗锯齿导入设置。

截图入口：[手机目录／卡片](../outputs/p8r-mobile-ui-20260913/spacing-check1/1080x1920/04-furniture.png)、[家具浮动按钮](../outputs/p8r-mobile-ui-20260913/spacing-check1/1080x1920/18-furniture-actions.png)、[健康布局无状态条](../outputs/p8r-spacing-ui-20260913/native1/1080x1920-360x640/healthy-ready-overlay.png)、[最短横屏详情与 Floor 工具](../outputs/p8r-mobile-ui-20260913/spacing-check1/1138x640/27-INJECTED-expanded-readiness-floor-preview.png)。完整目录为 `outputs/p8r-mobile-ui-20260913/spacing-check1/`。

实际生产修改仅 6 个文件：`TimeControlPanel.cs` 调整时间排列；`P8RButtonLayout.cs` 管理 Tab 底板与 action 可见尺寸；`DecorationActionBarView.cs` 收紧浮动操作排列；`DecorationCatalogueView.cs` 调整目录卡片与分类标题尺寸；`DecorationCatalogueTileView.cs` 调整名称字号、两行尺寸标注与横向留白；`ValidationMessageView.cs` 管理正常提示隐藏、输入释放与事件。另补充相关测试并更新既有 Spec／Guide，没有修改 Scene、Prefab、素材或字体资产，没有 commit／push／merge。

Owner manual review：

1. Normal／Decoration 下查看三个时间按钮，确认紧凑且间距一致，1x／2x／恢复速度正常。
2. 四个 Tab 往返切换、收起再展开、改变窗口尺寸：底板仍相邻等宽，名称栏不重新出现。
3. 浏览四类卡片，检查完整的 Shiba Painting、Cash Register 与 Counter 尺寸；内层缩略图背景不变。
4. 家具与墙饰 Preview 中检查 Cancel／Rotate／Confirm／Store 的大小和间距，不与 HUD 或取餐牌互盖。
5. 用三柜台 beginner 示例补齐设备和取餐点：正常时没有绿色状态条；制造问题后重新出现；修复后再次消失，旧位置不吞点击／滚轮。

本次两项 UI skill 用于实际可见间隔、非重叠 touch root、较小文字完整性与上下文提示显隐。第 28 节的受限 Preview 限高取舍和先前恢复核查仍独立待处理；本次不代表真机验收或全项目 Ready。

## 30. 地板工具、连续时间条与卡片图文比例（2026-09-13）

Owner 在第29节结果上确认本轮三项局部调整；此节优先于旧尺寸记录，Refined B、四Tab配色和已批准的交互不变。

- Floor 范围按钮和 footer 文字改为12 logical、底板高32，减少多余横向留白；Undo／Rotate／Apply All 与 Cancel／Apply 紧邻排列，原顺序不变。点击根节点仍至少48 logical，保持非重叠；Wall footer、家具浮动工具和弹窗不套用这个小字号。
- 时间按钮改为一条连续三段纯icon控制条，高34；细分隔线区分 Pause／1x／2x，保留各自48×48点击区、原18／18／26 icon和速度／装修锁定行为。不是将三个点击区缩成三个更难点击的小按钮。
- 所有有名称的卡片外框仍68×84；caption 13→11.5，区域62×41→62×34；浅色thumbnail well 56×30→58×40，thumbnail 48×22→52×34。预览图可用高度增加约55%，仍等比显示、不拉伸模型；名称完整保留两行，尺寸数字与ASCII x不拆开。没有名称的Floor／Wall surface卡片仍为原well 56×72、image 48×64。

字号11.5是Owner明确批准的密度取舍，不声称符合原14号下限。手机真机是否足够清楚仍需Owner判断。没有新PNG或抗锯齿导入修改，也没有修改字体资产、Scene、Prefab、Save或Preview交易规则。

### 30.1 实现位置与验证记录

本轮生产修改6个文件：`P8RSurfaceFooterLayout.cs`负责Floor实际测量；`P8RButtonLayout.cs`增加仅Floor使用的紧凑底板；`DecorationFloorRangeView.cs`与`DecorationActionBarView.cs`在状态刷新后应用尺寸；`TimeControlPanel.cs`管理连续时间条；`DecorationCatalogueTileView.cs`调整有名称卡片的图文比例。相关tests与本Guide／Spec同步维护。

RED：[p8r-proportion-red.xml](../TestResults/p8r-proportion-red.xml)，7项全部准确复现原尺寸／缺少连续时间条，未包含编译失败。后续green1因时间条的一处类型错误未进入测试，不能计为通过。

最终同一版production的130项相关回归已分批通过，不冒充单次130/130：较广的[p8r-proportion-green5.xml](../TestResults/p8r-proportion-green5.xml)为127 PASS／3旧测试合同失败；随后只更新测试，不改production，[p8r-proportion-compatibility6.xml](../TestResults/p8r-proportion-compatibility6.xml)精确复测这3项，3/3 PASS、failed／skipped均0、Unity CLI exit0。三项分别是旧Floor全宽底板、将时间整条sprite误当分段可见区域、旧13号caption下限；仍验证32高Floor底板、分段图标居中及11.5字号，Wall／其他控件合同和48非重叠触控断言未放宽。

回归包含Floor反复选中后的文字和可见间隔、所有真实named卡片的两行glyph／尺寸、无caption surface原尺寸、时间首次初始化／四种状态／同一HUD横竖屏往返的节点复用与真实raycast，以及既有跨Tab取消、滚轮、modal、readiness和正式布局边界。`focused4`已先通过3项时间检查；其余几何浮点比较改为与附近检查一致的0.05 logical微小容差。测试中发现的时间类型错误、负向Units异常、face绘制顺序与初始化问题均已修复；早期green1／green2／focused3不作为通过证据。

最终原生截图测试[p8r-proportion-native1.xml](../TestResults/p8r-proportion-native1.xml)为1/1 PASS，failed／skipped均0、正常Editor exit0。真实Game View／ScreenSpaceOverlay生成113张：四种既有profile各28张，另加1138×640最短横屏1张；新增滚到Cash Register及Windows的画面，不替换RenderTexture、不改变画质。截图测试与回归范围重叠，数量不累计为全项目验证。

已逐张目视检查30张不同关键截图（不是113张全部目视）：两种手机各8张、平板6张、横屏7张、最短横屏1张；两位独立reviewer参与。时间icon与状态完整，连续条没有内部重复圆角；Floor工具紧凑，Apply按实际文字测量；有名称卡片的缩略图区域更大，完整可视卡片未见glyph被截断或图片拉伸。仍保留两项观感／显示边界：选中的Wood Shelf单行文字距Preview虚线较近，但全文可读，交由Owner审美review；小屏／短横屏在scroll viewport边缘会裁切相邻卡片或分类，需滚动查看，不能声称所有内容始终同时完整可见。最低像素密度不保证完全无斜线阶梯，真机字号与触感未验收。

截图入口：[手机Floor工具与时间条](../outputs/p8r-mobile-ui-20260913/proportion-check1/1080x1920/05-floor.png)、[手机家具卡片](../outputs/p8r-mobile-ui-20260913/proportion-check1/1080x1920/04-furniture.png)、[手机收银机](../outputs/p8r-mobile-ui-20260913/proportion-check1/1080x1920/28-equipment-cards.png)、[手机窗户卡片](../outputs/p8r-mobile-ui-20260913/proportion-check1/1080x1920/29-window-cards.png)、[最短横屏边界](../outputs/p8r-mobile-ui-20260913/proportion-check1/1138x640/27-INJECTED-expanded-readiness-floor-preview.png)。实现与限定自检完成，等待Owner review，不是全项目Ready。

### 30.2 Owner review

1. Play MainCafe，试Pause／1x／2x及Decoration锁定，检查时间条是否紧凑且能清楚分辨选中段。
2. Floor预览中切Whole Room／Single Grid，检查范围和底部五项操作的间距、文字与点按；Confirm／Cancel行为不变。
3. 滚动Furniture和Wall Decor，重点查看Counter、Cash Register、Shiba Painting和Tall Glass Window：模型预览更大，文字完整；Floor／Walls纯表面卡片没有被重新缩放。

两个game UI skills分别用于视觉层级／一致性，以及非重叠点击区／响应式与状态刷新检查。严格45%限高的既有例外、Phase7恢复核查、Android／iOS真机与Owner acceptance仍保持原边界，不将本轮测试等同于全项目Ready；无commit／push／merge。

## 31. 更紧凑的浮动按钮与时间选中块滑动（2026-09-13）

Owner 已确认两项调整，以下规格优先于旧记录中的浮动32／20与时间条34高，Refined B素材、颜色和其他UI不变。

- 浮动Store／Cancel／Rotate／Confirm的底板30、icon18，可见底板矩形之间约12.03 logical。2／3／4按钮均按数量向组中心内收；点击根节点仍48×48且位置不动、不重叠，底板完整位于各自点击区内。Floor／Wall footer不套用这个偏移。
- Mode badge和时间条统一为144.03125×32；竖屏上下间距8，横屏保持并排、间距8。模式文字14及时间icon18／18／26不变。
- 橘色背景0.18秒ease-out滑动，icon和分隔线不跟随移动；游戏速度立即生效。快速连点取消旧动画并从当前可见位置转向新目标，暂停时仍使用unscaled时间完成。首次加载／重新启用按实际速度定位；Decoration锁定显示灰色、隐藏橘色，退出后反映恢复的速度；真实尺寸变化结束旧动画并重新对齐。

生产改动只有3个文件：`P8RButtonLayout.cs`负责较小底板与icon共同偏移；`DecorationActionBarView.cs`负责按按钮数量内收，并避免末尾外观刷新覆盖已算好的位置；`TimeControlPanel.cs`负责等尺寸布局、共享选中背景与动画生命周期。另补充／更新6个测试文件、本Guide与既有Spec。没有新PNG、依赖、Scene／Prefab、字体或存档修改，没有更改GameTimeService或Preview交易规则，无commit／push／merge。

### 31.1 验证记录

[RED](../TestResults/p8r-compact-motion-red.xml)5项均按预期失败：原底板32、状态框和时间条宽度不同、缺少共享移动背景；不是编译错误。

首次focused为12 PASS／2测试测量问题：Pause／Resume的PNG透明边距不同，应测可见墨迹中心而不是Image矩形原点；只改logical profile不一定改变真实几何，应固定初始竖屏后实际resize。修正这两处fixture，不改production，独立reviewer复核通过。

最终128项相关回归在同一版production上分批通过，不冒充单次128/128：[regression2](../TestResults/p8r-compact-motion-regression2.xml)为127 PASS／1旧尺寸断言失败；最后将该项浮动底板32／间距16同步为30／12，其余raycast、无Tooltip、禁用及模式循环断言不变。[native3](../TestResults/p8r-compact-motion-native3.xml)精确复测该项并运行原生截图测试，2/2 PASS，failed／skipped均0、正常Editor exit0，漏复测失败项为0。截图测试已包含于上述回归范围，不额外累加为新用例。所有5项新增测试均在regression2通过；覆盖真实N=2/3/4按钮、触控区边缘归属、footer复位、横竖屏、快速改目标、暂停、锁定、重新启用和节点复用。

原生Game View／ScreenSpaceOverlay生成107张截图：四种既有profile各26张，最短横屏边界1张，另加480×854下2张实际滑动中间帧。不修改动画时长或替换Camera渲染来制造效果。Fast中间位置为79.00522 logical（稳定1x在48.015625、2x在96.03125）；Paused中间位置为42.0479（从2x向0移动），均在实际渲染帧抓取；运行测试另确认暂停后能抵达终点。

逐张目视检查14张不同原图，并有独立review参与：1080×1920的01／02／03／04／18；480×854的01／18和两张30-motion中间帧；1600×720与1536×2048各01／18；1138×640的27边界图。未发现新增双重边框、异常圆角、选中色残留、图标错位或浮动按钮裁切。状态框和时间条相等且靠近，四个浮动按钮均匀紧凑；滑动中间帧中只有背景改变位置。没有声称107张全部人工查看，也没有声称低像素密度下所有斜线完全无阶梯。

截图：[手机状态与时间条](../outputs/p8r-mobile-ui-20260913/compact-motion-check1/480x854/01-hud-1x.png)、[四个浮动按钮](../outputs/p8r-mobile-ui-20260913/compact-motion-check1/480x854/18-furniture-actions.png)、[切2x中间帧](../outputs/p8r-mobile-ui-20260913/compact-motion-check1/480x854/30-motion-Fast-mid.png)、[暂停中间帧](../outputs/p8r-mobile-ui-20260913/compact-motion-check1/480x854/30-motion-Paused-mid.png)。

### 31.2 Owner review

1. Play MainCafe，查看状态框与时间条的大小／距离，连续点击1x、2x、Pause，确认滑动手感及速度立即切换。
2. 新家具、已有家具和墙饰分别检查2／3／4个浮动按钮：外观更小更近，图标居中，边缘点按仍属于对应按钮。
3. 进入／退出Decoration并改变窗口方向，确认锁定无橘色、恢复后档位正确，Floor／Wall工具未被内收偏移影响。

两个game UI skills用于保持Refined B一致性、实际可见间距、独立touch root与响应式；game-feel用于短ease-out、可打断改目标和不依赖游戏时间的动效。实现与限定自检完成，等待Owner review；Android／iOS真机触感、既有严格45%限高例外、Phase7恢复核查及全项目Ready仍保持原验收边界。

## 32. 小墙饰 Preview 抓取与操作栏避让（2026-09-13）

Owner 已批准修复 1×1 Wall Monitor 被 Cancel／Confirm 或场景抢走点击的问题。保留第31节的 Refined B 外观、30底板／18 icon／约12.03可见间距，以及完整48×48 logical点击区；不通过缩小点击区或让按钮点击穿透模型来解决。

实际生产修改只有 `Assets/Scripts/Decoration/DecorationModeController.cs`：

- 墙饰 Preview 的整个 renderer bounds 纳入既有操作栏避让，额外预留半个真实按钮高度；复用 ActionBar 的上下优先、侧面备用与固定 UI safe-area 限制。`DecorationActionBarView.cs` 本轮未改。
- UI 仍最先判定；按下（Began）时允许从可见 ghost 的 enabled renderer bounds 抓取当前 Preview，使用保存的显示墙面／墙格，不重新启用 ghost Collider。
- 移动（Current）继续按真实墙体寻找墙格，不持续命中自己的 ghost；空白场景仍由 Camera 处理。没有更改 Confirm／Cancel、跨Tab取消、正式布局、Save、Prefab、Scene、PNG 或时间控件。

新增 `P8RWallDecorActionAvoidanceTests.cs` 与 `P8RWallDecorDragAccessTests.cs` 两组 PlayMode 测试（及对应meta），分别锁定完整按钮触控区避让，以及实际 MainCafe monitor 的 UI／模型／场景分类、真实 router 移动和 Cancel 的正式布局不变。尺寸覆盖1080×1920、480×854与1600×720，两个zoom、左中右三个位置；共18个几何组合，不是18项独立测试。

### 32.1 验证记录

- RED：`p8r-wall-drag-red2.xml` 真实复现144px触控根节点覆盖monitor；另外两项当时存在取样fixture问题。先只修避让后，`p8r-wall-drag-red3.xml` 中几何通过、Began仍准确命中后墙而非当前Preview的slot断言失败；另一个router fixture目标恰落在UI上，随后改用左侧真实邻格。最初`red`编译错误不是产品失败证据。
- 针对性验证：[green1](../TestResults/p8r-wall-drag-green1.xml) **4/4 PASS**，failed／skipped均0，Unity CLI exit0。
- 较广回归：[regression1](../TestResults/p8r-wall-drag-regression1.xml) **88 PASS／2 FAIL，共90项**。包括墙饰跨墙、invalid、existing、Confirm／Cancel、跨Tab，以及真实触控按住、移出释放、模块禁用、设备移除和2／3／4按钮排列。
- 未将两项失败算作通过：`Phase7MainCafeSceneTests.Newly_confirmed_wall_decor_is_immediately_classified_by_the_real_physics_path` 在508行返回Ui；`Responsive_matrix_keeps_compact_actions_visible_raycastable_and_inside_safe_area` 在889行的旧Tab image.raycastTarget断言失败。临时逐字恢复修复前Controller后，[baseline](../TestResults/p8r-wall-drag-baseline.xml)两项以相同断言再次失败，证明不是本轮引入。未顺带改旧测试或扩大修复；已恢复修复版且hash与修复快照一致。
- 恢复后的正常Editor原生验证：[native1](../TestResults/p8r-wall-drag-native1.xml) **4/4 PASS**，failed／skipped均0、Editor exit0；与上述4项重叠，不累加。使用真实Game View／ScreenSpaceOverlay，未替换Camera targetTexture、未超采样，生成三种profile各居中／靠右两张，共6张。

原图：[窄手机居中](../outputs/p8r-wall-drag-fix-20260913/native1/480x854/monitor-center.png)、[窄手机靠边](../outputs/p8r-wall-drag-fix-20260913/native1/480x854/monitor-edge.png)、[横屏靠边](../outputs/p8r-wall-drag-fix-20260913/native1/1600x720/monitor-edge.png)。主agent已逐张目视480×854和1600×720的4张原图；独立review已实际目视1080×1920两张，合计6张均检查：模型露出、两按钮与模型分离，未见新增HUD冲突或屏幕边缘裁切，保留Refined B样式。独立代码review未发现阻塞问题；建议后续补充“invalid display抬手后再次抓取”的专门测试，本轮新增直接覆盖valid/new，旧测试覆盖invalid保留与恢复，不冒充新用例覆盖。

### 32.2 Owner review

1. 重新Play MainCafe，进入Wall Decor，选1×1 Wall Monitor，按原问题的摆放角度拖动模型；应移动墙饰而不是镜头。
2. 放大／缩小后，将模型移到屏幕两侧，检查Cancel／Confirm仍可点按且不遮住模型；点空白场景仍可移动镜头。
3. 分别Cancel与Confirm，再选择已确认墙饰，确认原有取消／重新编辑行为未变。

本轮使用systematic-debugging分离遮挡与抓取原因，TDD逐层复现，game-ui-ux保持完整触控区及UI优先；只完成这一局部修复与限定验证。仍等待Owner按原截图位置确认手感，Android／iOS真机验收、两项既有测试失败、第28节限高例外、Phase7恢复核查及全项目Ready边界不变。

## 33. 全面 review 后的四项修复与收银机取景（2026-09-14）

Owner 已批准处理上一轮 review 的四项输入／验证问题与一项 UI 建议。本节补充新证据，不覆盖上一轮只读 review 的历史结果；保留 Refined B 颜色、图标、卡片和字体。

### 33.1 本轮变化

1. **Normal 原生 Touch**：单指移动、双指连续缩放接入现有镜头限制；UI 起点不移动镜头，第二指点击 UI 不变成 pinch。Touch 活跃时不叠加 Mouse，Editor 滚轮保持原来的整步缩放。模式交接、失焦或暂停会取消旧 Touch，重新抬手后才能开始新手势。
2. **退出弹窗隔离**：打开退出确认时结束旧场景手势和边缘自动移动，但保留未确认 Preview；弹窗后方不再响应旧 pan／pinch。选择 Continue Editing 后，旧手指不会接着拖动，全部抬起后恢复新操作。
3. **缺失触点恢复**：即使设备移除没有发出 Ended／Canceled，旧 primary 消失也会按取消释放，不产生 tap；失去 pinch 的 secondary 时重新建立单指基线，不突然跳动。重新接入的 Touchscreen 可以开始新操作。
4. **EndDrag 与旧 fixture**：动态日志确认，旧测试的拖动终点仍在已变宽的 Catalogue handle 内，松手触发 Click、展开并隐藏原目标，导致后续 EndDrag 无法送到目标。测试改为实际 UI-free 的外部终点，保留 EndDrag、禁止误展开及 ownership 断言。另更新旧 FeedbackToast 路径、被新 UI 覆盖的场景取样点，以及“健康提示隐藏”和“浮动按钮收起后恢复合法位置”的断言；没有恢复常驻绿色 Ready to open，也没有删除核心行为检查。
5. **收银机缩略图**：仅对 `equipment.cash-register.01` 的已批准、未打包 256×256 原图使用 148×166 的显示取景。复用同一 Texture，不重新生成 PNG；模型完整保留适量留白。卡片68×84、预览内框58×40、Image52×34、字号11.5不变。其他物品及不匹配／打包后的图片保留原显示；换绑与销毁会释放临时 Sprite，不销毁源图。

生产文件按用途分为三组：

- `MouseCameraInput.cs`、`CameraInputFrame.cs`、`CafeCameraController.cs`、`DecorationCameraDriver.cs`：共享 Touch／Mouse 镜头输入与连续 pinch。
- `DecorationModeController.cs`、`DecorationTouchRouter.cs`：弹窗中断、旧触点取消与设备恢复。
- `DecorationCatalogueTileView.cs`：仅收银机显示取景及临时 Sprite 生命周期。

新增 `P8RNormalNativeTouchTests.cs`、`P8RInputRecoveryPlayModeTests.cs`、`P8RCashRegisterThumbnailFramingTests.cs`；更新 `Phase6DecorationRealTouchTests.cs`、`P8RReferenceLayoutTests.cs`、`Phase8FunctionalSurfaceInteractionPlayModeTests.cs` 的相关 fixture。没有修改 Scene／Prefab、PNG、import settings、Save、正式布局交易规则或 dependencies；没有 commit／push／merge。

### 33.2 验证记录

第一轮 [输入 RED](../TestResults/p8r-review-fixes-red-20260914.xml) 为21项，1 PASS／20 FAIL：真实复现弹窗后 pinch 仍缩放、移除 Touchscreen 后 Camera owner 残留、Normal 缺少 Touch 路径，以及 EndDrag 失败；不是编译错误。

第二轮 [focused](../TestResults/p8r-review-fixes-green1-thumbnail-red-20260914.xml) 为29项，28 PASS／1 FAIL。输入与旧 fixture 修正通过；唯一失败是尚未实施取景时收银机可见主体宽13.414 logical，小于20的目标，属于 UI 修复的有效 RED。

扩大回归的第一轮 [regression1](../TestResults/p8r-review-fixes-regression1-20260914.xml) 为598项，581 PASS／17 FAIL，不能当成通过记录。后续分开排查了以下测试环境／前提问题，没有为它们改生产 UI：

- `InputTestFixture` 重置前需停止旧场景 Touch source；清理时先结束触点、移除设备，再卸载 UI 和释放 action state。Phase5 route 也需对称释放新增的 Normal Touch source，原 runtime isolation 断言保留。
- 原生截图必须使用正常 Editor 的 `RealGameViewSize`，不是 batch 或只设置 GUIView 逻辑尺寸。尺寸 lease 由 `UnityTearDown` 可靠恢复；失败截图不作为 UI 验收证据。
- HUD 要比较可见32高的时间条，不能把外扩到48的透明触控区当作底板；仍检查等尺寸、8单位可见间距及不重叠。48点击宽的坐标换算曾得到47.9999886，测试只加入0.01 logical单位容差，真实 raycast 和禁用断言不变。
- Modal 遮挡探测有一点落在实际 Cancel 按钮上，原测试自己关闭弹窗；改为按下探测后移至经 raycast 验证的 blocker 再取消，后面的真正 dismiss release／不穿透／新触点恢复检查保留。Deferred flush 测试固定原先的640×480，确保确有待执行位移，仍要求大于1px，不能用合法原地不动冒充位移覆盖。
- 扩大加入的 Phase0 场景加载测试会留下环境 Collider；后续旧 selection fixture 没有隔离 Physics，存在组间干扰风险。7项在独立进程中全部通过，未记录最近命中的具体 Collider。本轮不改生产选中规则或另建通用测试框架，最终按 UI、router、基础镜头分组运行。

[isolated-native1](../TestResults/p8r-review-fixes-isolated-native1-20260914.xml) 为29项、23 PASS／6 FAIL；[native2](../TestResults/p8r-review-fixes-native2-20260914.xml) 为25项、23 PASS／2 FAIL。失败均保留在记录中。native2 中新的 Normal Touch、弹窗／设备恢复、真实第二指 HUD 点击、HUD 几何和 Cash 取景检查已通过；剩余两个旧 fixture 随后按上述前提修正再测。

原生480×854截图：[Cash Register 调整后](../outputs/p8r-cash-thumbnail-framing-20260914/480x854/cash-register-framed-native2.png)。主代理已目视这一张原图：模型及底座完整、边缘无新增明显锯齿，原 Refined B 卡片／字体／颜色未变。截图测试同时验证源 Texture 和 Sprite rect 没变、其他物品不继承取景、临时 Sprite 正确释放。不是所有分辨率逐张目视，也不是手机硬件验收。

最终同版代码的分组回归全部通过，三组用例互不重复，共 **598/598 PASS，失败0、跳过0**，各次 Unity CLI exit0：

- [UI 与装修交互](../TestResults/p8r-review-fixes-final-ui-20260914.xml)：464/464。
- [Touch router 与基础场景 UI 输入](../TestResults/p8r-review-fixes-final-router-20260914.xml)：108/108。
- [基础镜头与 Phase0 回归](../TestResults/p8r-review-fixes-final-camera-20260914.xml)：26/26。

包含本轮所有新增测试，以及首轮失败项的最终版本；真实第二指 HUD 测试已迁移到既有真实 Touch pump fixture，并验证 Down／Up／Click 各一次。输入与截图 fixture 的后续修改只解决测试初始化、清理和测量前提，生产代码在首次598项回归后保持同版。上述原生截图测试与598项重叠，不额外累计；之前的103项 domain review 结果没有算作本轮新验证。

独立代码／测试有效性复审无阻塞，本轮涉及文件的 `git diff --check` 通过。这里只完成获批四项修复与一项取景建议及限定验证，不代表全仓库、所有设备或整个Phase8已验收。

### 33.3 你可以怎样检查

1. 在 MainCafe 的 Normal 与 Decoration 模式分别单指移动、双指缩放；点 UI 时镜头不动。Editor 再检查滚轮，以及滚动错误详情时不会同时缩放场景。
2. 建立未确认 Preview，一根手指保持拖动，第二根点击退出；弹窗后方应停止移动。继续编辑仍保留 Preview，旧手指不会续拖，抬手重按后正常。
3. Furniture 中滚动到 Cash Register：模型应更易辨认，卡片／字体／配色不变。对比 Coffee Machine 与 Counter，确认没有一起放大。

本轮使用 systematic-debugging／TDD 分离真实输入缺陷与过期 fixture；两个 game UI skills 用于保持 Refined B、实际可见主体占比和原点击区／卡片几何。自动注入 Touch 和原生 Editor 截图不是 Android／iOS 真机手感验收；第28节限高例外、Phase7恢复核查、已记录的两项 Phase7MainCafeSceneTests 既有失败及全项目Ready边界不变。

## 34. 四项 UI 一致性收尾（2026-09-14）

Owner 批准四项一起修改；继续使用现有 Refined B，不恢复旧的长 Preview 说明，不重画 PNG，也不修改正式布局或保存交易规则。本节更新第28节的受限高度处理，历史截图和失败记录保留。

- Floor／Wall 的 Apply、Cancel 共用12 logical字号、32可见高度、每侧6留白；Apply按最终显示文字测量，不再按临时Confirm留宽。透明点击根节点仍至少48。
- 等待选择地板格／墙面的正常指引使用既有 `status_info`；Blocked仍用warning，错误保持独立语义。
- 展开面板遵守Safe Area的45%上限。正常尺寸沿用固定工具布局；短宽屏将Return／Pickup放进同一header，极短屏放不下的原有工具随目录滚动。收起、换tab、重新展开和旋转时恢复原父级，不复制按钮。分类名称和卡片／字体规格保留，仅必要时收紧首行留白。
- Floor tab图标的最长边20→23，等比补偿15%；其他三个tab、PNG、导入设置及点击区域不变。

生产文件：`DecorationActionBarView.cs`负责按钮和提示；`P8RSurfaceFooterLayout.cs`统一文案测量；`DecorationCatalogueView.cs`负责高度与工具重排；`P8RButtonLayout.cs`只补偿Floor图案。原生截图另发现嵌套ActionBar重复应用safe area，`SafeAreaContainer.cs`增加可选的父级管理规则；默认行为不变，ActionBar挂入宿主后由外层统一管理边界。没有修改Scene／Prefab。测试新增`P8RConsistencyChromeTests.cs`，扩展Compact／Reference、CompleteFlow、安全区和触控回归。

### 34.1 验证与边界

最终[限定范围回归](../TestResults/p8r-consistency-ui-final-green.xml)470/470通过，零失败、零跳过；[安全区定向测试](../TestResults/p8r-consistency-safe-area-green.xml)9/9、六项关键集成测试6/6通过。不能把本节当作全项目或真机验收；具体RED／GREEN和未通过的早期尝试记录在 `outputs/p8r-consistency-20260914/progress.md`。首组原生截图曾发现Apply右侧裁切，已新增晚改变safe area的回归并修复重复inset，旧图不计最终视觉通过。[替换原生验收](../TestResults/p8r-consistency-native-final.xml)4/4通过，五种尺寸的109张PNG位于 `outputs/p8r-mobile-ui-20260913/consistency-final2`；已目视复核手机Floor／Wall／信息指引及短横屏Apply／Furniture／Pickup。原生、定向和完整回归存在测试重叠，不累加成新的总数。独立review无生产阻塞，最终diff-check通过；按钮测试要求topmost raycast，避免被挡住仍误判可点。

完整回归早期467/470中的三项InputRecovery失败，已用最小顺序组合复现并定位到测试时钟：模拟输入13.20秒落在Unity保留的Editor切换区间12.32522–15.6999511，被当作旧事件丢弃。仅在fixture reset后校准模拟时钟，未修改生产输入、原断言或事件处理顺序，临时诊断已删除；同一顺序组合随后7/7、独立运行3/3通过。没有用放宽断言或跳过测试来掩盖失败。

### 34.2 你可以怎样检查

1. MainCafe进入装修，分别编辑Floor和Wall，比较Apply／Cancel文字与底板；切到Single Grid或尚未选墙面时，指引应是中性信息图标。
2. 用普通手机和短横屏尺寸展开目录，建立未确认Preview后再打开目录；面板应不继续变高，短屏中滚动可以找到所有工具。
3. 收起再展开、切换tab、横竖屏切换：按钮不消失，Return仍回到原Preview；换tab仍按既有规则取消未确认Preview。对比四个tab，Floor更易辨认但没有变形。

本轮不自动commit／push；Phase7既有恢复核查、Android／iOS真机验收和全项目Ready边界不变。game-ui-design／game-ui-ux用于维持B风格、响应式和真实点击范围；TDD与独立review用于区分产品问题与旧测试前提。

## 35. Furniture／Wall Decor 浮动按钮进一步收紧（2026-09-14）

Owner 批准可见间距从约12缩到约9.4 logical单位。只修改 `DecorationActionBarView.cs` 的 P8R floating 分支：透明点击根节点宽48→44，高度仍48；可见底板仍30×30，icon ink仍18，B配色、PNG和导入设置不变。2／3／4按钮分别保持等距、整组居中；实际间距9.390625，四按钮最外侧底板仍完整落在自己的点击区内。Floor／Wall footer、时间控制、目录tabs和legacy UI不变。

对应更新七个既有P8R测试文件的floating预期：FloatingTightSpacing、ActionGeometry、CompactChrome、CompleteFlow、WallDecorActionAvoidance、ReferenceLayout、MobileLayoutIntegration。通用helper默认仍检查48宽，仅明确的floating调用使用44；保留safe area、按钮不重叠、图标大小、禁用规则、顶层raycast、透明留白边缘点击，以及墙饰模型／Pickup标志避让检查。未修改Scene／Prefab或预览交易、拖动逻辑。

### 35.1 验证记录

- [有效RED](../TestResults/p8r-floating-gap94-red.xml)：3项全部在旧宽48与新目标44不符处失败；实施后这3项通过。
- [第一轮集成](../TestResults/p8r-floating-gap94-green1.xml)：24/28通过，4项失败均为旧48宽预期；[扩大回归](../TestResults/p8r-floating-gap94-regression.xml)：248/249通过，剩余小屏floating support也沿用旧48预期。仅更新对应floating断言，未放宽其他控件或删掉行为检查。
- [旧版四尺寸真实触控](../TestResults/p8r-floating-gap94-touch-red.xml)：1/1通过，未修改测试。文件名预留为touch-red，但实际结果为PASS；它加载appearance为空的Phase6 prefab，仍合法使用48×48。
- 最终同版代码[限定回归](../TestResults/p8r-floating-gap94-final-green.xml)：**250/250通过，失败0、跳过0，Unity CLI exit0**。覆盖P8R、Phase7墙饰触控／legacy UI和两项Phase6触控检查，不代表全仓库或真机验收。
- [正常Editor原生截图验证](../TestResults/p8r-floating-gap94-native.xml)：1/1通过。五种尺寸109张PNG位于 `outputs/p8r-mobile-ui-20260913/floating-gap94`；主agent实际检查480×854与1600×720各2／3／4按钮三张，独立review检查1080×1920对应三张，共9张目视，不冒充全部109张逐张验收。间距与尺寸一致，未见本轮新增边缘裁切；PNG未重新绘制。此用例与250项重叠，不额外累加。

独立代码／测试及上述视觉review未发现本轮新增问题。TDD和game UI检查用于保留B风格、独立点击归属及响应式边界；仍需Owner确认实机手感。

### 35.2 你可以怎样检查

重新Play MainCafe，分别新建Wall Decor、新建Furniture、选择已摆放Furniture，对比2／3／4按钮组；应更紧凑，底板和icon不变大。点取消／旋转／确认及按钮留白边缘，再拖动墙饰，确认仍点到预期目标。原生例图：[手机四按钮](../outputs/p8r-mobile-ui-20260913/floating-gap94/480x854/18-furniture-actions.png)、[横屏三按钮](../outputs/p8r-mobile-ui-20260913/floating-gap94/1600x720/12-furniture-preview.png)。

本轮没有commit／push，保留此前未提交修改及既有Material／验证Scene变动；不扩大第34节与既有Phase8验收边界。

## 36. Cash Register 两侧提示（2026-09-14）

Owner 批准在截图方案基础上实现一个可运行版本。沿用现有 B 风格：Employee 使用鼠尾草绿围裙，Customer 使用杏橙色购物袋。**当前版本已按 2026-09-15 的批准稿改为地面小箭头与上方悬浮 icon，见 36.4；36.1–36.3 保留为旧文字标牌版的验证记录，不再代表当前外观。** 只解释设备两侧，不表示可用站位或队列格，也不修改现有朝向、交易、Save 或 pointer ownership。

### 36.1 本次文件与行为

- 新增 `Assets/Scripts/UI/Decoration/CashRegisterSideIndicatorView.cs`：从当前 ghost 的 `CashRegisterSideMarker` 读取侧向，使用已经包含设备与 Counter 旋转的 world pose；标签保持正立，成对寻找模型／按钮外侧的 safe-area 空位。极端空间不足时隐藏提示，不覆盖操作。所有 Graphic 不接收 raycast，整组 CanvasGroup 不可交互；连线使用带透明边缘的 mesh，绘制顺序位于操作按钮后。
- 修改 `Assets/Scripts/Decoration/DecorationModeController.cs`：只接入 Cash Register 当前 preview 的显示与清理。Confirm、Cancel、Store、换 tab、其他设备、退出装修及 disable 后不残留；modal／展开目录时隐藏，返回编辑时恢复。没有修改 Scene、Prefab 或 layout domain 代码。
- 新增 `Assets/Resources/UI/P8R/RoleIcons/employee-apron.png`、`customer-bag.png`，以及专用 `Assets/Editor/P8RCashRoleIconImporter.cs`：只约束这两张新素材的透明、mipmap、Trilinear、无压缩导入，不改变既有图标。
- 新增 `Assets/Tests/PlayMode/EditorSceneLoading/P8RCashRegisterSideIndicatorTests.cs`：检查四向旋转、旋转的 Counter、invalid floor fallback、preview 生命周期、真实 UI raycast、可见性、连线 mesh 与大小屏避让；另有显式启用的原生截图测试。

### 36.2 验证记录与限制

- [初始 RED](../TestResults/p8r-cash-side-red.xml)：4/4 因缺少提示层失败。后续测试暴露先放一个标签占掉另一侧空位，以及短横屏候选不足；已改为成对选位，并增加整组 action row 外侧候选。测试明确要求 alpha 可见，不能用隐藏旧坐标通过。
- 首次原生截图虽然用例通过，目视却发现连接线没有绘制，因此不算最终视觉通过。[连线 RED](../TestResults/p8r-cash-side-leader-red.xml)1/1 精确失败于缺少 CanvasRenderer；补上必需组件，并增加 renderer-owned mesh 的实际顶点与非裁剪断言。测试 API 按本机 Unity 6 的无参数 GetMesh 校正，只读该 mesh，不销毁它。
- 最终同版代码[定向回归](../TestResults/p8r-cash-side-final-regression.xml)：139 PASS、0 FAIL，另1项原生截图测试按 opt-in 忽略，Unity CLI exit0。覆盖本功能以及 Surface View／Interaction／Catalogue、floating spacing 和 input recovery；不是全仓库测试。
- [最终正常 Editor 原生截图](../TestResults/p8r-cash-side-native-final.xml)：1/1 PASS，零跳过。六张 PNG 位于 `outputs/p8r-cash-side-indicators-20260914/native-20260915-035600-3773655`，包含480×854与1600×720的0°、90°、Confirm后隐藏。主 agent 逐张目视检查了六张；图片保持真实 GameView、Overlay 和生产画质，没有重绘或合成。首版目录保留作历史证据。
- 独立只读 review 检查了最终旋转、生命周期、成对避让、Renderer 与绘制层级，未发现阻塞。UI skills 用于维持 B 风格、安全区与不挡操作，TDD 区分了逻辑通过和真实可见；外观偏好与 Android／iOS 真机手感仍由 Owner 验收。

### 36.3 你可以怎样检查

旧版操作记录：重新 Play MainCafe → Decoration → Furniture → Cash Register；放到 Counter 后旋转，观察 Employee／Customer 连线是否始终指向设备相应侧。试拖动设备、打开并取消 Store 弹窗、返回目录再继续；Confirm／Cancel／换 tab 后标签应消失。[旧横屏示例](../outputs/p8r-cash-side-indicators-20260914/native-20260915-035600-3773655/landscape-1600x720-cash-0.png)、[旧竖屏示例](../outputs/p8r-cash-side-indicators-20260914/native-20260915-035600-3773655/portrait-480x854-cash-0.png)。本轮没有 commit／push，不改变此前 Phase8 验收边界。

### 36.4 贴地小箭头首版＋正上方悬浮 icon（2026-09-15；高度规则由 36.5 更新）

Owner 批准先做游戏内可运行版本。修改范围只有显示层、对应测试与本节记录：

- `CashRegisterSideIndicatorView.cs`：替换文字底板／连线，复用原围裙与购物袋 PNG；箭头是带柔边的 runtime mesh，使用现有 FootprintLight 的独立 Material 副本。角色色不表示 placement validity。箭头高度沿用 `GridHighlightView.FootprintHeight`，不浮在 Counter 顶面，也不修改共享材质。
- 箭头小于一格，沿真实设备侧向朝内；固定斜视角下结合相机射线深度判断 Counter 遮挡，仅将真正被挡住的后侧箭头沿原侧向最小外移，前侧保持紧凑。icon 的 x 始终对准箭头中心，约半格高，随镜头缩放并有轻微浮动；空间紧张时只抬高避让真实 Button Image 底板及 safe area。透明点击留白仍保留，icon 无 raycast，arrow 无 Collider。
- `DecorationModeController.cs`：只额外传入既有 footprint Material、gridRoot 和 cell size。Confirm／Cancel／Store／展开目录／切 tab／退出装修／disable 的生命周期规则不变；销毁时释放独立 world root、mesh 与两份 Material。
- `P8RCashRegisterSideIndicatorTests.cs`：以新视觉行为替换旧标签断言，增加贴地、icon 垂直对齐、真实 4–12 zoom、柜台遮挡、横屏近看底栏遮挡与 world renderer 清理检查。无 Scene、Prefab、Save、domain 或既有 icon 资源修改。

验证：初始 [RED](../TestResults/p8r-cash-ground-red.xml) 精确失败于缺少两支 world arrow；原生截图另暴露后侧箭头被 Counter 遮住，专门的 [遮挡 RED](../TestResults/p8r-cash-ground-occlusion-red.xml) 复现后再修正。随后独立截图 review 发现横屏近看时前侧箭头被过度外移到底栏后方，由[底栏遮挡 RED](../TestResults/p8r-cash-ground-catalogue-red2.xml)复现，再加入真实深度判断。最终同版代码[直接回归](../TestResults/p8r-cash-ground-final-regression3.xml)为 **142 PASS、0 FAIL、1 opt-in 截图项忽略**；该截图项已在[正常 Editor 单独通过](../TestResults/p8r-cash-ground-native-final3.xml)，**1 PASS、0 FAIL、0 跳过**，生成 10 张原生 PNG，包含竖屏／横屏的 0°、90°、4／12 zoom 及 Confirm 后隐藏。最终竖屏近看与独立横屏近看视觉复查未见上述遮挡。不是全仓库回归或真机验收。

现在可重新 Play MainCafe → Decoration → Furniture → Cash Register，放上 Counter 后旋转、缩放、取消再试；重点判断两侧 icon 的尺寸、距离和含义是否直观。[竖屏近看](../outputs/p8r-cash-side-indicators-20260914/native-20260915-133028-7864889/portrait-480x854-zoom-4.png)、[横屏近看](../outputs/p8r-cash-side-indicators-20260914/native-20260915-133028-7864889/landscape-1600x720-zoom-4.png)。极远镜头下图标会随场景变小；按钮密集时 icon 可能抬高。其他家具／墙体的遮挡仍需实际布局体验。Technical checks 与 Owner 外观接受分开：**视觉验收待 Owner 确认**。本轮未 commit／push，保留所有此前未提交修改。

### 36.5 提亮箭头、成对等高、整条操作栏避让（2026-09-15；站位规则由 36.6 更新）

Owner 指出箭头偏暗，以及一枚 icon 被单独抬过按钮后与另一枚高度不一致，并确认了本次方案。修改仅涉及两份 runtime C#、对应 PlayMode tests 与本节记录；不改 Scene、Prefab、PNG、共用 Material、Save、方向语义或其他家具按钮的排版规则。

- `CashRegisterSideIndicatorView.cs`：独立箭头 Material 的 `_LightIntensity` 从 1.0 调为 1.3、`_FootprintOpacity` 从 0.52 调为 0.65；颜色、尺寸、贴地位置和柔边不变，不新增 Light 或 Bloom。两枚 icon 使用同一个 arrow-relative 高度和 bob，保留各自箭头的屏幕 x，不能再单独跳到按钮上方。
- View 提供包含完整 bob 上下范围的稳定避让区域；只在布局变化时通知，不随浮动重排按钮。先让操作栏避让；极拥挤时只对两枚 icon 共同作安全范围内的高度调整，完全没有空间时沿用整组隐藏规则。
- `DecorationModeController.cs`：用 CR 独立分支把 icon 区域加入现有 `avoidScreenRect`，先布局标记再安排操作栏，并清理事件订阅。`DecorationActionBarView.cs` 本轮未修改，保留既有整条避让及按住按钮时延迟移动的保护，未借用 pickup sign 的额外高度偏移。
- `P8RCashRegisterSideIndicatorTests.cs`：覆盖四个朝向、default／4／12 zoom 的等高与实际按钮不重叠；校验独立材质提亮而共用材质不变；增加 bob 不推动操作栏、PointerDown 后缩放不移动按压目标及 PointerUp 后恢复布局的真实组件回归。

证据：[RED](../TestResults/p8r-cash-equal-hover-red.xml) 的 2 个测试真实失败，分别测出约 98 对 32 px、58 对 19 px 的不一致相对高度；修正后[首轮定向回归](../TestResults/p8r-cash-equal-hover-green1.xml)为 8 PASS、0 FAIL、1 opt-in 忽略。[最终直接相关回归](../TestResults/p8r-cash-equal-hover-regression1.xml)为 **146 PASS、0 FAIL、1 opt-in 截图项忽略**，涵盖 CR 指示、功能家具 View／Interaction、Catalogue、紧凑按钮、input recovery 与 wall decor 避让。该 opt-in 项随后在[正常 Editor 原生截图测试](../TestResults/p8r-cash-equal-hover-native1.xml)中单独 **1 PASS、0 FAIL、0 跳过**，生成 10 张横竖屏 PNG。不是全仓库回归或手机真机验收。

实际查看：[竖屏默认距离](../outputs/p8r-cash-side-indicators-20260914/native-20260915-150422-0827661/portrait-480x854-cash-0.png)、[竖屏近看](../outputs/p8r-cash-side-indicators-20260914/native-20260915-150422-0827661/portrait-480x854-zoom-4.png)、[横屏近看](../outputs/p8r-cash-side-indicators-20260914/native-20260915-150422-0827661/landscape-1600x720-zoom-4.png)。均来自真实 GameView，无重绘或合成；主代理查看竖屏默认／近看，独立 reviewer 查看横屏默认／近看，未发现本次高度或底栏遮挡问题。操作栏按可用空间优先上下避让，必要时整条移到侧方。极远镜头的小 icon、其他家具／墙体遮挡仍需实际布局体验。

UI design skill 用于保持当前 B 配色、箭头与 icon 的空间对应及操作区域清晰；TDD 与独立 review 用于检查布局、材质隔离和输入保护。Technical checks 与 Owner 感官验收分开，**外观待 Owner 确认**。重新 Play MainCafe → Decoration → Furniture → Cash Register，旋转、缩放并确认／取消，即可检查新版。本轮未 commit／push，保留此前所有未提交修改。

### 36.6 当前版：真实站位固定箭头＋逐角色 invalid 标志（2026-09-15）

Owner 已批准暖红色圆圈＋斜线的样式，并明确要求箭头紧邻 CR 的真实员工／顾客站位，不得被推到整排 Counter 外沿。本节替代 36.4 的 renderer 外轮廓定位与遮挡外移；36.5 的箭头亮度、成对等高、轻微 bob 和整条操作栏避让继续保留。

本轮修改的 files：

- `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`：增加 readonly preview anchor 查询，用临时 `SurfaceMountedInstance` 复用正式 `InteractionAnchorResolver`，不把 preview 加入 confirmed layout，也不重算或发布 confirmed readiness。
- `Assets/Scripts/Layout/LayoutReadinessEvaluator.cs`：抽取共用 `GetAnchorObstruction`，沿用 outside 优先、再检查 Blocked reservation／floor occupant 的原规则。没有增加 flood-fill 或改变营业条件；`AnchorUnreachable` 不属于本次红圈提示范围。
- `Assets/Scripts/Decoration/DecorationModeController.cs`：将真实 anchors 与每个 role 的阻挡状态传给 View。桌面 `SlotOccupied` 仍由原 placement feedback 处理，不能误报成双侧站位受阻；没有 anchor 的无支撑 fallback 双侧显示 invalid。`CanConfirm` 规则不变。
- `Assets/Scripts/UI/Decoration/CashRegisterSideIndicatorView.cs`：箭头使用真实 cell center／Facing，包含 support 与 equipment 旋转；删除按整块 Counter 大小和遮挡外移的算法。红圈作为原 icon 的子层，valid 后移除；始终预留 1.2 倍包围区域以防切换时高度或按钮跳动。
- 新增 `Assets/Scripts/UI/P8R/P8RInvalidRoleGraphic.cs`（及 Unity 自动生成的 `.meta`）：用原生 UI mesh 绘制暖红色 `#B95640` 圆圈斜线，透明内底、96 段圆环与约一屏幕像素 AA fringe。沿用原 apron／bag PNG，不重绘角色；禁止符号不需要额外低分辨率 PNG。所有新增 Graphic 均无 raycast。
- `Assets/Tests/PlayMode/EditorSceneLoading/P8RCashRegisterSideIndicatorTests.cs`：覆盖逐角色状态、四方向、旋转 1x3 Counter 的中间 Slot、blocked reservation、out-of-bounds、无支撑 fallback、占用桌面不误报、confirmed report/version 不变、取消／确认清理，并增加实际阻挡／旋转恢复的原生截图。

验证记录：

- [初始 RED](../TestResults/p8r-cash-invalid-red.xml)：2 个测试真实失败，分别为箭头偏离真实相邻 cell 约 0.47 格、缺少禁止标志。
- [复核问题 RED](../TestResults/p8r-cash-invalid-slot-red.xml)：占用桌面导致两角色误标 invalid；修正后纳入最终回归。
- [Domain 回归](../TestResults/p8r-cash-invalid-domain-regression.xml)：**77 PASS、0 FAIL、0 跳过**，覆盖 readiness、anchor resolver、functional layout 与 preview session。
- [最终直接相关 PlayMode 回归](../TestResults/p8r-cash-invalid-final-regression.xml)：**150 PASS、0 FAIL、1 opt-in 截图项忽略**，覆盖 CR 指示、家具 View／Interaction、Catalogue、输入恢复、紧凑操作栏和 wall decor 避让。
- 该 opt-in 项随后在[正常 Editor 原生截图测试](../TestResults/p8r-cash-invalid-native.xml)中 **1 PASS、0 FAIL、0 跳过**，生成 14 张真实 GameView PNG。主代理检查竖屏受阻／恢复两张，独立 reviewer 检查横屏对应两张；图形边缘平顺、角色可辨认、按钮不重叠，代码与视觉复核未发现 blocker。不是全仓库回归或 Android／iOS 真机验收。

实际效果：[竖屏两侧受阻](../outputs/p8r-cash-side-indicators-20260914/native-20260915-211207-1953285/portrait-480x854-both-blocked.png)、[竖屏旋转恢复](../outputs/p8r-cash-side-indicators-20260914/native-20260915-211207-1953285/portrait-480x854-rotated-recovered.png)、[横屏两侧受阻](../outputs/p8r-cash-side-indicators-20260914/native-20260915-211207-1953285/landscape-1600x720-both-blocked.png)、[横屏旋转恢复](../outputs/p8r-cash-side-indicators-20260914/native-20260915-211207-1953285/landscape-1600x720-rotated-recovered.png)。均为实际游戏截图，无重绘／合成。

注意：真实站位被 Counter 占用时，贴地箭头会被模型遮挡；即使站位有效，远侧箭头也可能因透视被 Counter 遮住。它们不再为了可见而移动到错误位置，上方角色 icon 仍说明对应侧，受阻时有红圈。没有改深度绘制规则或让箭头穿透墙体。

你可以重新 Play MainCafe → Decoration → Furniture → Cash Register，在连排的三个 Counter 中间放置 CR，旋转查看红圈出现／消失；再试取消、确认、切 tab。原来的确认规则不变，红圈是角色站位提示，不代替整店营业检查。保持当前 B 配色、不抢按钮输入；本轮没有修改 Scene／Prefab／Save／现有 PNG／共享 Material，也没有 commit／push。**技术验证通过，外观接受仍待 Owner 实际查看。**

### 36.7 CR 标志抬高至常驻 Pickup 的近似视觉高度（2026-09-15）

Owner 已确认：以未拖动的常驻 Pick Up Point 为高度参考，只提高两个角色 icon 与其 invalid 圈；地面箭头、大小、轻微浮动和摆放规则保持不变。

- `CashRegisterSideIndicatorView.cs`：自然目标高度由 `0.62` 调为 `1.4 × cellSize`，保留共享相对高度、完整红圈／bob 包围区域及 safe-area／按钮避让。此值按当前 Counter Slot `0.72`、Pickup lift `0.112`、billboard 牌面中心 `camera.up × 0.46` 和 MainCafe 相机投影近似对齐；不包含 Pickup preview 的额外拖动抬高。以后若更换相机倾角或柜台高度，需要重新校准。前后两个标志仍随各自地面站位投影，不强行对齐绝对屏幕 Y。
- `P8RCashRegisterSideIndicatorTests.cs`：新增真实 confirmed Pickup renderer 对比，在 zoom `4 / 6 / 12` 检查相对各自地面的视觉高度；通用按钮／safe-area 检查改为量完整 `InvalidOverlay` 外沿，而非较小的 icon Rect。
- 本节记录范围和证据；没有改 Scene、Prefab、Save、PNG、共享 Material，也没有 commit／push。

[RED](../TestResults/p8r-cash-pickup-height-red.xml) 真实失败：zoom 6 的常驻 Pickup 相对地面约 `113.66 px`，旧 CR 约 `59.00 px`。[最终直接相关回归](../TestResults/p8r-cash-pickup-height-final.xml)：**40 PASS、0 FAIL、1 opt-in 截图项忽略**，覆盖高度、四方向、缩放、完整红圈、安全区域、紧凑按钮、输入恢复及功能家具 View。[正常 Editor 原生截图测试](../TestResults/p8r-cash-pickup-height-native.xml) 随后单独 **1 PASS、0 FAIL、0 跳过**，生成 14 张真实 GameView PNG。

新版截图：[竖屏受阻](../outputs/p8r-cash-side-indicators-20260914/native-20260915-223206-3411897/portrait-480x854-both-blocked.png)、[竖屏旋转恢复](../outputs/p8r-cash-side-indicators-20260914/native-20260915-223206-3411897/portrait-480x854-rotated-recovered.png)、[横屏受阻](../outputs/p8r-cash-side-indicators-20260914/native-20260915-223206-3411897/landscape-1600x720-both-blocked.png)。图像未经重绘或合成。重新 Play MainCafe → Decoration → Furniture → Cash Register，旋转与缩放即可复核。技术验证不等同全仓库回归或手机真机验收，最终视觉接受仍由 Owner 判断。

### 36.8 桌面物件拖拽下沉修复（2026-09-15）

Owner 已批准：桌面范围内稳定吸附；拖过 UI 时保留原位置；真正离开柜台才进入无效地面 preview。旧逻辑把任何无 Slot 地址的拖拽帧都清成 `default`，包括 UI 帧；此外只用 Slot 中心 72 px 圆形范围，近看时会漏掉台面边角。失去 Slot 后 ghost 从台面 `0.72 + hover 0.35` 降至地面 `0 + hover 0.35`，形成埋入柜台的效果。

本轮修改 4 个 files：

- `DecorationModeController.cs`：UI 或既无 Slot、也无 FloorPosition 的帧保留原 pose；有明确 FloorPosition 的无 Slot 拖拽仍走原 floor fallback。命中判断优先使用 Slot 台面平面与 support 的 right/forward 半格范围，支持柜台旋转及镜头缩放；多个真实台面按 ray distance 选择，同深度优先当前 Slot。原 72 px 只作为未命中台面的兼容吸附。
- `Phase8FunctionalSurfaceInteractionPlayModeTests.cs`：真实 UI raycast／Router 测试覆盖 CR、Coffee Machine、Pick Up Point 的新增与编辑；桌面角落覆盖 0°／90°，明确断言 CurrentHit 的 Slot，避免仅因保留旧位置而误通过。
- `P8RCashRegisterSideIndicatorTests.cs`：MainCafe 正式 Router／classifier 在 zoom 4／12 下经过真实 rotate 按钮，再回到台面角落；验证 ghost 高度、address、owner 释放及 confirmed readiness/version 不变。原生截图流程复用该输入路径。
- 本节记录修复及证据。没有修改 Router ownership、Confirm 规则、View 高度、Scene、Prefab、Save、PNG 或共享 Material；也未 commit／push。

有效 [RED](../TestResults/p8r-mounted-drag-red3.xml)：3 个测试在旧实现下真实失败，分别为 UI 清空绑定与两个朝向的台面角落漏吸附；之前两轮包含测试 fixture 初始化问题，不作为产品 RED 证据。修复后 [Interaction/View](../TestResults/p8r-mounted-drag-green.xml) **114 PASS**；[最终直接相关回归](../TestResults/p8r-mounted-drag-final.xml) **155 PASS、0 FAIL、1 opt-in 截图项忽略**。该项随后在[正常 Editor 原生截图测试](../TestResults/p8r-mounted-drag-native.xml)中单独 **1 PASS、0 FAIL、0 跳过**，输出 16 张真实 GameView PNG。独立代码与测试复核通过；不是全仓库回归或手机真机验收。

实际画面：[竖屏拖拽后](../outputs/p8r-cash-side-indicators-20260914/native-20260916-010711-4513373/portrait-480x854-drag-ui-and-table-edge.png)、[竖屏放大](../outputs/p8r-cash-side-indicators-20260914/native-20260916-010711-4513373/portrait-480x854-zoom-4.png)、[横屏拖拽后](../outputs/p8r-cash-side-indicators-20260914/native-20260916-010711-4513373/landscape-1600x720-drag-ui-and-table-edge.png)。未重绘或合成。重新 Play MainCafe → Decoration → Furniture，分别拖动 CR／Coffee Machine／Pick Up Point 经过按钮、台面边缘和空地，再试拖回、取消与确认。

保留边界：真正离开 Counter 的地面 invalid preview 仍可见、不能 Confirm；台面外的旧 72 px 吸附容错没有整体重设计。当前正式 Counter 与 grid hierarchy 为单位 scale；以后若允许非单位缩放，需要同步调整台面范围。最终实际操作手感仍待 Owner 验收。

### 36.9 Wall Decor 拖拽目标与遮挡保护（2026-09-15）

Owner 已批准修复诊断中的三处问题。Wall Decor 原有 last-display fallback 不会像桌面物件一样掉到地面，但 UI 帧会清空墙面目标；拖过已有墙饰会被其 Collider 截断；目标丢失又会恢复遮挡物的不透明材质，造成错误 invalid 或看似穿模。

本轮仅修改 4 个 files：

- `DecorationModeController.cs`：`TryHandleSceneDrag` 对 UI 帧直接保留 preview，包括原来已经 invalid 的状态；`ClassifyPrimaryHit` 在 active preview 的 Current 阶段跳过 confirmed wall-decor Collider，继续检测后方真实墙格，正常报告 `Overlap`。Began 仍能选中已有墙饰。`UpdateWallMountedProjection` 根据实际显示墙面保留 occlusion fade，不再使用可能为空的逻辑目标；Cancel 等结束路径照常恢复材质。
- `P8RWallDecorDragAccessTests.cs`：新增 MainCafe 新建／已有墙饰经过真实 Cancel button 后恢复拖拽的测试，以及经过已确认 monitor 后报告 `Overlap`、invalid 经过 UI 不改变原因、拖回空格恢复的测试。使用真实 Camera、Collider、UI raycast、Router 和 Controller；不关闭 UI 来绕过浮动按钮。恢复取点可在同一指定墙格内避开按钮中心。
- `Phase7WallMountedTouchPlayModeTests.cs`：新增真实 blocker／wall／Camera／fade Materials 的目标丢失测试，验证 ghost pose 保留、Confirm 被拒绝、fade 保留及 Cancel 后恢复原材质。
- 本节记录修复与测试证据。没有修改 Scene、Prefab、PNG、模型高度、Save 或共享 Material；不改变手势起点归属和 Confirm 才提交的规则，未 commit／push。

有效 [拖拽 RED](../TestResults/p8r-wall-drag-red2.xml) 为 **3 个真实失败**：新建／已有 preview 经过 UI 后 SurfaceId 被清空，confirmed wall decor 抢先返回 `WallMounted` 而非后方 `WallSlot`。[遮挡 RED](../TestResults/p8r-wall-drag-fade-red.xml) 为 **1 个真实失败**：失去目标后 fade Material 被恢复。先期测试字段编译问题、恢复取点被按钮遮挡的问题已修正，不作为产品 bug 证据。

修复后 [focused GREEN](../TestResults/p8r-wall-drag-green.xml) **7 PASS、0 FAIL**；[最终直接相关回归](../TestResults/p8r-wall-drag-final.xml) **218 PASS、0 FAIL、1 opt-in CR 截图项忽略**，覆盖墙饰／跨墙角／动作按钮避让／输入恢复／桌面物件与 CR indicator。该截图项本轮未运行；本次是 MainCafe 自动交互与场景 fixture 检查，不是新截图、全仓库回归或手机真机验收。

独立代码与测试复核：**0 Critical、0 Important，无交付 blocker**。记录一个非阻塞测试增强项：以后可增加“墙饰外表面与后方墙面映射到不同 Slot”的明确 fixture，以更强地保护跳过整个 Collider hit 的 `continue`；当前实现已正确跳过。

手动复核：重新 Play MainCafe → Decoration → Wall Decor；新放或选中已有墙饰，拖过 Cancel／Confirm 按钮，再回到墙上；拖到另一件墙饰处应提示占用，拖回空墙应恢复；真正离墙仍为 invalid，不能 Confirm；取消后原物件位置及遮挡材质应恢复。最终操作手感仍待 Owner 验收。

### 36.10 营业提示改为短状态和处理清单（2026-09-15）

Owner 已确认：保留当前暖色提示栏的位置、尺寸上限和滚动方式，精简信息，不更改营业判定。

- 收起只显示 `Can't open yet · N issues`；可营业但有 warning 时显示 `Can open · N suggestions`。完全健康时继续隐藏。
- 展开按“加粗物件／角色 + 一句处理动作”排列，例如 `Cash Register · Customer side` / `Clear space for the customer.`。移除玩家正文中的重复原因、`Blocking:` 和坐标；warnings 单独放在 `Suggestions` 下。
- 完全重复的问题去重计数，不合并不同实例、角色、位置或原因；同类多个实例使用编号区分。完整原始记录、坐标及原始原因仍保留在诊断接口，detached IDs 不丢失。
- 有未确认 preview 时，展开详情底部显示 `Updates after confirmation.`。确认、取消、切 tab、退出后同步清除；备注变化不重算 confirmed readiness，也不改变展开状态。仍只有 Confirm 写入布局。

本轮 files：

- `P8RAppearance.cs`、`P8REnglish.json`：独立玩家短文案和完整诊断 formatter，覆盖 12 种 failure。
- `ValidationMessageView.cs`：受控 rich text 标题、短摘要和 preview 备注；generic status / legacy 仍为 plain text。
- `DecorationModeController.cs`：在既有 preview 事件和退出 cleanup 后同步备注，不增加逐帧 gameplay 轮询。
- `CashRegisterSideIndicatorView.cs`：回归发现普通场景卸载可能先销毁 invalid overlay；补充 Unity-null 检查，避免清理时访问已销毁对象，不改变 indicator 的外观或摆放规则。
- 新增 `P8RReadinessCopyTests.cs`；扩展 `P8RReadinessSafeAreaTests.cs`，调整 `P8RCompleteUiTests.cs` 与 `Phase8MainCafeSceneTests.cs` 的旧文案断言。本节记录范围和验收方法。

验证证据：

- [文案 RED](../TestResults/p8r-readiness-copy-red.xml)：旧实现 25 FAIL / 1 PASS；[真实 UI RED](../TestResults/p8r-readiness-ui-red.xml)：缺少 rich text 和 preview 备注，2 FAIL。
- [退出 RED](../TestResults/p8r-readiness-exit-red2.xml)：直接退出 Furniture preview 时备注真实残留；正常 UI Discard 本身无此缺陷。修复在 `session.Exit()` 之后同步 pending。
- [文案／诊断 GREEN](../TestResults/p8r-readiness-copy-green.xml)：**42 PASS、0 FAIL**。[最终相关 UI 回归](../TestResults/p8r-readiness-ui-final.xml)：**75 PASS、0 FAIL、2 opt-in 截图项忽略**，覆盖安全区、展开／收起、滚轮隔离、Floor confirm、Wall cancel、WallDecor cancel／tab switch、Furniture discard／direct exit、MainCafe 重载与 CR 指示。未通过修改 fixture 绕开场景卸载异常。
- Readiness opt-in 随后在[最终原生截图测试](../TestResults/p8r-readiness-native-final.xml)中 **1 PASS、0 FAIL**；本轮不重跑 CR 的 opt-in 图集。使用真实 `RealGameViewSize`，同时验证 Screen、Camera 和输出 PNG 尺寸一致；早期 `033720` 目录存在切屏尺寸错配，不作为验收证据。

最终原生截图：[手机收起](../Artifacts/readiness-checklist-20260916-034124/phone-collapsed.png)、[手机展开](../Artifacts/readiness-checklist-20260916-034124/phone-expanded.png)、[preview 备注](../Artifacts/readiness-checklist-20260916-034124/phone-preview-note.png)、[横屏展开](../Artifacts/readiness-checklist-20260916-034124/landscape-expanded.png)。主代理逐张检查，文字可读、按钮未覆盖正文，详情超出高度时继续滚动；不是合成图或手机真机验收。

独立代码复核在修复退出遗漏后无 Critical / Important。保留一个 Minor：未来文案若加入 `&` 或 `< >`，当前 HTML entity escaping 会被 TMP 原样显示，需要届时补实际 parsed-text 测试并调整转义；当前英文没有这些字符，不影响本轮显示。

你可以重新 Play MainCafe，展开提示栏；查看每项的物件／角色与处理动作，再开始、确认或取消 preview，检查备注出现和消失。还可滚动详情确认镜头不同时缩放。game-ui-design / game-ui-ux 用于保持现有视觉和输入边界；TDD 与独立 review 用于验证呈现、状态清理和诊断保留。本轮未改 Scene／Prefab／Save／现有 PNG，未 commit／push；视觉接受仍待 Owner 实际查看。

### 36.11 Push 前回归修复与重新验证（2026-09-16）

Owner 已批准先修复回归失败，再重新测试并 commit／push 到现有 P8 分支；不合并 main，不改变已接受的紧凑 UI 外观。

- `SafeAreaContainer.cs`：父级持有 Safe Area 的标记仅属于运行时，改为非序列化，避免污染 Scene／Prefab 或被独立 clone 继承。新增 clone 回归先观察到真实失败，再验证修复；保留父级安全区行为。
- `P8RColoredTabAssets.cs`：重复应用已正确的彩色 Tab 时，先检查素材、状态和 raycast 绑定，不因运行时排版重新保存 Prefab。回归同时保护 16 个目标的 bytes 与时间戳；测试恢复先还原全部素材／meta，再统一 import，避免残留错误 importer 状态。
- 旧 UI 测试与已批准的 20 logical action ink、20／23 logical Tab ink、Floor icon-only utility 及 Cash Register 缩略图裁切对齐；保留字体、完整名称、disabled 状态、点击区和确认生命周期检查，不用放大 UI 来迎合旧断言。
- 新 `ProjectAssetEditSafety.cs`：识别 Unity 动态 Font 自动生成的只读 glyph atlas。三个 Editor 工具在把对象转成文件路径前，仅排除属于该 Font 的精确 atlas；主 Font、Font Material、Importer、TMP SDF atlas 和其他真实 dirty 资产仍阻止写入。生产代码不会替用户保存、清理 dirty 或 reimport 源字体。
- 全量回归另发现 `ApplyApproved` 跨导入操作持有的 Appearance native 引用可能失效：写入前重新从固定路径取得当前实例，并再次拒绝 missing／dirty 资源。确定性测试只卸载独立 GUID 的测试 clone；原 Appearance 通过 MoveAsset 暂存并原样移回，验证原 instance、GUID、bytes、meta 均恢复，不卸载原生产资产。

验证记录：

- [字体缓存 RED](../TestResults/p8-prepush-font-cache-guard-red.xml)：**12 PASS、3 预期 FAIL**。三个入口均因动态 glyph atlas 误报 `.otf` 未保存；没有编译错误。
- [修复后混合回归](../TestResults/p8-prepush-resume-focused-green.xml)：**103 PASS、0 FAIL、0 跳过**，包含全部新增 dirty 保护组合、上一轮 UI 修复、Phase 6 字体使用和 Phase 8 builder 的加载顺序回归。
- [首轮完整回归](../TestResults/p8-prepush-retest-full-editmode.xml)：**1736 PASS、1 FAIL、355 跳过**，仅作为诊断记录；唯一失败为上述 stale Appearance。跳过来自 dirty caller Scene 保护（Phase 6 validator 194、migration 160、Phase 8 single-Scene 1），不是 opt-in，必须独立补测。测试额外改写的 8 个资源已留副本并恢复测试前字节，原有修改保留。
- [安全隔离的生命周期 RED](../TestResults/p8-prepush-appearance-owned-red2.xml)：**1 个预期失败**，准确复现相同 `SerializedObject` 空引用，原资产恢复断言通过。先期直接卸载原资产的诊断测试已替换；不支持的 NUnit 并行标记已移除，未增加 dependencies。
- [最终定向回归](../TestResults/p8-prepush-lifetime-focused-green.xml)：**104 PASS、0 FAIL、0 跳过**。此前因场景保护跳过的 355 项随后分别独立补跑：[validator 194 PASS](../TestResults/p8-prepush-final-phase6-validator.xml)、[migration 160 PASS](../TestResults/p8-prepush-final-phase6-migration.xml)、[single-Scene 1 PASS](../TestResults/p8-prepush-final-single-scene.xml)，按完整测试名称对照，没有漏项。
- [首次完整 PlayMode](../TestResults/p8-prepush-final-full-playmode.xml)：**1009 PASS、6 FAIL、2 opt-in 截图项跳过**，仅作为诊断记录。Phase 7 的 5 项涉及旧 UI 断言／测试取点；Phase 5 的 1 项是前一触摸 fixture 未卸载场景造成的输入系统污染。[Round2 独立运行](../TestResults/p8-prepush-phase5-input-diagnostic.xml) **10 PASS**，而[两 fixture 顺序 RED](../TestResults/p8-prepush-phase5-input-sequence-red.xml) **10 PASS、1 个同样失败**，证实该顺序依赖。修复仅清理 RealTouch fixture 自己创建的场景与输入资源，不改生产触摸逻辑。
- `Phase7MainCafeSceneTests.cs` 的兼容修复保留 legacy 分支；新版断言使用已绑定的 category Sprite、真实 root raycast 和独立 Modal Safe Area host。收起栏检查固定的 48 logical 触控高度、8 logical Tab/handle 间距与 24 logical 贴底留白，不用放宽屏幕百分比。墙饰首次确认后的取点仅依据可见 UI 与真实 Collider，保留同帧和准确实例 ID，不关闭 UI、不调用 classifier 筛答案、不手动同步 Physics。
- [关联 PlayMode 回归](../TestResults/p8-prepush-playmode-compat-focused-green.xml)：**36 PASS、0 FAIL、0 跳过**。随后落实 review 的 GraphicRaycaster 精确筛选与卸载后引用清理，再冻结最终 61 个非文档候选文件的 SHA256。
- [最终完整 PlayMode](../TestResults/p8-prepush-final-full-playmode-r2.xml)：**1015 PASS、0 FAIL、2 opt-in 截图项跳过**；两项分别是 CR 角色图集与 Readiness 原生截图，没有把它们算作 PASS。本轮不声称新图像或手机真机验收。
- [最终完整 EditMode](../TestResults/p8-prepush-final-full-editmode.xml)：**1738 PASS、0 FAIL、355 场景保护跳过**。与上述 3 份独立补测按完整用例名称逐项比对，355 项全部已有本轮 PASS；合计覆盖 **2093 个不同用例，无失败、无遗漏**。不是把 skipped 当作通过，也没有移除 dirty Scene 保护。
- 最终测试后，8 个测试生成的资源变化再次留副本并恢复到测试前 SHA256；原有修改完整保留。最终 61 个非文档候选文件的哈希全部匹配冻结版本，加本指南共 **62 个提交文件**，与完整 PlayMode／EditMode 的源码一致。

独立代码／QA 复核无 Critical／Important。保留三个非阻塞的测试增强项：单独注入 Tab raycast 漂移、在 compact utility 测试里直接要求 Icon 节点存在、在真实 dirty 拒绝测试中先验证 clean baseline 并核对报错路径。当前实现的相应条件已检查正确；这些建议不扩展本轮 UI 行为。

提交边界：只收录本次功能、回归修复与本指南；排除原有 23 份 Phase 7 材质序列化差异、`AssetPipelineReadability.unity` 的既有差异，以及测试输出／截图目录。测试前保留文本资源与 metadata 副本，测试生成的额外资源变化应精确恢复，不覆盖原有编辑。没有新的手机真机或视觉验收结论。

### 36.12 PR 前的 Prefab Mode 保护修复（2026-09-16）

最终 review 发现：完整 UI／Refined B 刷新只检查 persistent assets，不能覆盖 Prefab Mode 内尚未保存的编辑对象。Owner 已批准补上保护、回归测试，然后 commit／push 并创建 PR；不合并 main，不改变 Phase 或视觉验收状态。

- `P8RCompleteUiBuilder.cs`：两个 core 入口首先检查当前 PrefabStage。Catalogue、ActionBar、PutAwayModal 或 Exit 正在 Prefab Mode 中时，在打开 Scene、加载依赖和写文件之前拒绝操作，提示具体路径。先自行保存或放弃修改并关闭 Prefab Mode，再执行刷新；工具不替用户保存或清除 dirty。
- `P8RCompleteUiTests.cs`：新增 17 项检查。4 个目标 × 2 个入口分别覆盖 dirty MainCafe 与 clean MainCafe；断言未保存内容、对象身份、dirty／Auto Save、Scene setup、selection，以及 12 个文件的 bytes／时间戳不变。另用独立 GUID 的无关 Prefab clone 检查不误拦截；先加载测试自己的 MainCafe，隔离 Unity 打开 Scene 时切换 Stage 的既有行为。
- 清理只处理测试自行创建的 Stage、Scene 和 clone，恢复 Auto Save 与选择状态；已有调用方 Stage／MainCafe 时跳过并要求隔离补跑。没有修改 Scene／Prefab／PNG 或 runtime UI 规则。

验证证据：

- [安全 RED](../TestResults/p8-prefab-stage-red.xml)：8 个预期失败。旧入口跳过 Prefab Mode 检查，随后被测试的 dirty MainCafe 拦住；这是检查顺序的真实复现，不是实际数据丢失复现，生产资源未改写。
- [首轮定向回归](../TestResults/p8-prefab-stage-focused-green.xml)：57 PASS／1 FAIL，仅作诊断。无关 Prefab 测试未预载 MainCafe，触发 Unity 保存询问；修正测试前置条件，不压制日志或改变生产刷新行为。
- [最终定向回归](../TestResults/p8-prefab-stage-focused-green-r2.xml)：58 PASS／0 FAIL／0 SKIP，新增 17 项全部通过。独立 Editor safety review 无 Critical／Important／Minor。
- [完整 EditMode](../TestResults/p8-prefab-stage-full-editmode.xml)：1755 PASS／0 FAIL／355 场景保护跳过。三组独立补测分别为 [validator 194 PASS](../TestResults/p8-prefab-stage-phase6-validator.xml)、[migration 160 PASS](../TestResults/p8-prefab-stage-phase6-migration.xml)、[single-Scene 1 PASS](../TestResults/p8-prefab-stage-single-scene.xml)。完整用例名称逐项比对，355 项无缺漏、无多项，合计覆盖 2110 个不同用例。
- [完整 PlayMode](../TestResults/p8-prefab-stage-full-playmode.xml)：1015 PASS／0 FAIL／2 opt-in 截图项跳过。未声称新增截图、Player build 或手机真机验收。

两份源码在完整测试期间保持冻结；测试生成的 8 份资源变化留副本并恢复到测试前字节，原有 24 份资源修改保持不变。本次提交仅包含上述两份源码与本指南，不收录本地截图、测试报告或既有资源差异。

## 37. Phase 8 merge 与最终收尾（2026-09-21）

### 37.1 Owner 决定与合并版本

- Owner 确认朋友 review 完成，明确授权合并、将 Phase 8 标记完成、执行所有 post-merge 步骤并清理本地 branch；另行选择了先归档保留旧 worktree 文件，再移除 worktree。
- [PR #7](https://github.com/zhengparker/AnimalCafe/pull/7) 已合并：`codex/phase-8-functional-furniture` → `main`；reviewed head 为 `14a00a85c664e66d14c5864239fb099da5256980`，merge commit 为 `8ee0026247c2c3200bd1bc9ac471b7007e4a4f32`。保留完整提交历史；合并树与 reviewed head 的 tree 均为 `c157d6aaf21747d608d57e13fc44634cdde045af`。
- GitHub 当时没有 formal review / review comments，也没有配置 check runs / commit statuses；没有将空检查列表称为 CI PASS。朋友 review 和整体收尾批准的依据是本轮 Owner 的明确确认。
- 本地 `main` 已 fast-forward 至合并提交。原有 `.gitignore` 和 `AnimalCafe.slnx` 两份修改保持原字节，没有纳入本次完成记录提交。

### 37.2 Merged-main 验证

本次在合并后的主项目 fresh 执行，Toronto 时间为 2026-09-21 20:43:02–21:24:27；使用 Unity `6000.5.5f1`、graphics enabled，未启用两项 opt-in native screenshot。全部进程 exit 0，结果如下：

| 本轮本地报告 | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| [完整 EditMode](../outputs/phase8-postmerge-20260921/editmode.xml) | 1755 | 0 | 355 |
| [独立 validator](../outputs/phase8-postmerge-20260921/editmode-validator.xml) | 194 | 0 | 0 |
| [独立 migration](../outputs/phase8-postmerge-20260921/editmode-migration.xml) | 160 | 0 | 0 |
| [独立 single-Scene](../outputs/phase8-postmerge-20260921/editmode-single-scene.xml) | 1 | 0 | 0 |
| [完整 PlayMode](../outputs/phase8-postmerge-20260921/playmode.xml) | 1015 | 0 | 2 |

独立 QA 已核对 root 和逐用例结果，全部 inconclusive 为 0。完整 EditMode 的 355 个 scene-protection skipped fullname 与独立进程的 355 个 Passed fullname 完全一致，差集为 0；跨运行合计 **2110 个不同 EditMode 用例获得 PASS**，不把原始完整报告写成零 skip。PlayMode 的 2 项 skip 恰为 Cash Register 与 readiness 的 opt-in native screenshot，不计为 PASS。

本轮本地证据目录：`outputs/phase8-postmerge-20260921/`。测试前对 Assets / Packages / ProjectSettings 的 2773 份 tracked 文件保留恢复快照；测试产生的 29 份资源变化已另存到 `test-side-effects/` 并恢复到快照字节，生产文件 diff 为空。原有 `.gitignore` 和 `AnimalCafe.slnx` 两份修改也通过 SHA256 校验保留，不进入完成记录提交。

证据分享注意：原始 Unity log 含本地认证参数，仅保留在本机，不纳入 Git 或直接外发；分享结果使用 XML 和不含敏感参数的摘要。

### 37.3 归档与清理

已按 Owner 授权，将旧 worktree 的所有非缓存资料归档到 `outputs/phase8-archive-20260921/`：**74,981 个文件、6,271,447,731 bytes，源文件与副本 SHA256 全部一致**。包含 24 份未提交的 Phase 7 材质／验证 Scene 修改、截图、XML/log、历史 `.superpowers` 记录和项目文件；仅排除可重建的 `Library/` 与旧 `.git` worktree 指针。清单位于 `outputs/phase8-postmerge-20260921/archive-manifest.csv`，恢复说明见归档内 `ARCHIVE_README.md`。清理前又完成逐文件校验与独立 safety review。

回归通过后，已移除 `.worktrees/phase-8-functional-furniture` 和本地 `codex/phase-8-functional-furniture` branch。Git 首次移除遇到两个超过 260 字符的 Library cache 路径；确认 Git 登记已移除后，对残留非缓存文件再次核对归档 SHA256，以 PowerShell 逐文件及空目录清理，没有递归强制删除或扩大目标。其他两个 Codex worktree 保留。远端 Phase 8 branch 未获删除授权，继续保留；当前开发与试玩使用主项目 `E:\Unity\Project\AnimalCafe`。

### 37.4 完成范围与保留限制

- 完成范围为 Phase 8 functional furniture / anchors / readiness，以及本 PR 已实现的 P8R UI、输入和反馈增强；没有添加 Save / Load、NPC movement、Order / Queue、经营循环或 economy。
- Owner 的整体收尾决定不补造历史逐项 manual PASS。M1–M17 历史 Owner PASS 与 M18 授权技术代测 PASS 分开保留；M6 incompatible Slot 子项仍未独立覆盖。
- Guide 15.6 中历史 45 张截图和 3 份 metrics 的误覆盖限制保留；新归档不表示恢复了已被覆盖的旧版本。
- 本轮不新增 Player build、Android / iOS 真机验收或视觉验收结论；两项 opt-in 截图未执行不能计为 PASS。
- Roadmap 中独立 Phase 8R 的具体范围／gate 仍需 Owner 决定；不因文件名含 P8R 而自动关闭该规划 gate，也不启动 Phase 9。
