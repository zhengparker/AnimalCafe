# AnimalCafe Phase 8 Beginner Guide — Studio Owner Manual Handoff

> 当前状态（2026-09-08）：Phase 8 `In Progress`。M1–M17 为 Studio Owner 人工 PASS；M18 经明确授权，由 Codex 专项技术代测 PASS，独立 QA 复查 PASS，不计为用户亲自执行。M6 的类型不兼容 Slot 子项仍未覆盖；旧 Scene/Input 顺序验证问题仍开放，最新记录见第 6 节。

## 1. 这次要检查什么

Phase 8 只负责三件事：把功能设备放到合适位置、自动算出 Employee / Customer anchors、判断布局是否具备未来营业条件。它不包含 cafe day loop、NPC movement、Order、Queue、NavMesh / pathfinding agents、economy 或 Save；看到“可以营业”只表示布局准备好了，并不表示正式经营已经实现。

你只需要判断玩家实际看到和操作到的内容是否清楚、稳定、符合预期；**不要在本轮自己修改 Scene、Prefab、asset 或 code**。

## 2. 玩家视角：改动前与改动后

- 改动前：Decoration Mode 只能处理普通 furniture，系统不知道哪台设备负责收银、做咖啡或取餐，也不能判断布局是否适合未来营业。
- 改动后：Furniture Tab 多了 Cash Register、Coffee Machine 两行和 Pick-up Point button。玩家仍用熟悉的 Preview、Move、Confirm、Cancel、Store 操作；系统自动放置 Employee / Customer anchors，并在 Confirm 或 Store 后给出 readiness 结果。
- 例子：把一台收银机、一台咖啡机和一个取餐点放到合适 Counter Slots，并让顾客路线和员工路线各自连通，顶部会显示 `布局已准备好，可以营业`。

## 3. 重要文件与关键概念

| 文件 | 用途 |
|---|---|
| `Assets/Scripts/Layout/FunctionalSurfaceLayout.cs` | 保存已确认的桌面设备、Pick-up Points 和 Slot occupancy。 |
| `Assets/Scripts/Layout/InteractionAnchorResolver.cs` | 自动计算 Employee / Customer anchors；玩家不手动设置。 |
| `Assets/Scripts/Layout/GridReachabilityEvaluator.cs` | 分别检查顾客与员工 Grid 连通性，不负责 NPC 移动。 |
| `Assets/Scripts/Layout/LayoutReadinessEvaluator.cs` | 汇总设备、anchors 和两套网络，生成 ready / blocking / warning 结果。 |
| `Assets/Scripts/Decoration/SurfaceMountedPreviewView.cs`、`PickUpPointIndicatorView.cs` | 显示设备 Preview、Pick-up indicator 与随支撑家具移动的表现。 |
| `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` | 完整 automated/manual cases 与 18 项人工结果 ledger。 |
| 本 guide 第 6 节与表中指定的 XML | 本轮修复的最新自动化结果与交接状态；旧 Task 10 report 仅作历史快照。 |

关键概念：`Surface Slot` 是 Counter 上可放功能设备的位置；`anchor` 是未来 Employee 或 Customer 互动时应站的 Grid cell；`Preview` 只是临时状态，只有 Confirm / Store 成功后才改变正式 layout 和 readiness。顾客与员工使用两套独立连通网络；Pick-up 的两个角色 anchor 必要时可以共用一个 cell。

## 4. 开始前

1. 在 Unity Hub 打开项目：`E:\Unity\Project\AnimalCafe\.worktrees\phase-8-functional-furniture`。
2. 使用 Unity `6000.5.5f1`。
3. 打开 `Assets/Scenes/MainCafe.unity` 并进入 Play Mode；在 Game view 右侧点击 `Decoration` button 进入 Decoration Mode。Catalogue 默认打开 `Furniture` tab；如果当前是其他 tab，点击 `Furniture`。
4. P8-M-018 使用 validation Scene：`Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`。
5. 每项完成后，到 `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` 的“Manual Execution Ledger”填写结果。
6. 不要运行 `Build Assets` 或任何 `Configure ...` menu；它们会修改 assets / Scenes，不属于本次 manual review。
7. readiness feedback 显示在 Game view 顶部中央的 `Phase8_ValidationMessage`。它只在 Confirm / Store 成功后发布正式结果；Preview 或 Cancel 不会改写上一条正式 readiness。


判定方法很简单：预期现象全部出现、没有新的 Console error/exception，记 `PASS`；任何步骤无法完成、结果与预期不同、或 Console 有新的 error/exception，记 `FAIL`。`FAIL` 时记录 case ID、操作步骤、Scene 状态、Console message，并附截图或短视频；**停止修复，交回团队处理**。

## 5. 手动验收清单

### A. Catalogue 与设备放置（P8-M-001–005）

| Case | 操作 | PASS 判断 |
|---|---|---|
| P8-M-001 | 进入 Decoration Mode，打开 Furniture Tab；查看三行，鼠标分别放在物品行与 Catalogue 外层转滚轮，再按住拖动横向 / 纵向浏览。 | 滚轮不移动 Catalogue 内容，镜头仍可缩放；拖动浏览正常，三行清楚且无重叠、截断。 |
| P8-M-002 | 选 Cash Register，移到空闲 Counter Slot，观察悬浮并 Rotate 后 `✓`；再 Move → Cancel → Move → Confirm。 | 未确认 ghost 悬浮于桌面，footprint 留在桌面；确认后设备落回 Slot；Cancel 回原位；不需设置 anchor；Console clean。 |
| P8-M-003 | 对 Coffee Machine 重复主要放置、Move、Cancel、Confirm 流程。 | 只使用桌面 Slot；actions 一致；不出现 Customer anchor 设置。 |
| P8-M-004 | 把 CR/CM 移到 Floor，松手再抓 ghost 拖回桌面；也尝试已占用 Slot 和 `✓`。 | 地面 ghost 悬浮可见、footprint 红色；invalid 时 Confirm 不可用；回兼容空闲 Slot 可确认；未确认不改变原设备与 occupancy。 |
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
- P8-M-004 / 016：有未确认 Preview 时点击其他家具或 Tab，不应切换成第二个 Preview，也不应偷偷丢弃当前 Preview；先 `✓` 或 `×` 再切换。
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

#### 尚未解决的旧 Scene / Input 顺序验证问题

`Phase6DecorationRealTouchTests.CompositeOrder_MainCafeDecorationTouchThenPhase5ProductionTouchRestoresRuntimeIsolation` 在接于既有 Phase 8 Scene tests 后运行时，首次 Touch 点击 Decoration HUD 未进入；没有新的生产异常证据。排除新增 M1 fixture 后，旧 26 cases 仍为 `25/26`（`preexisting-scene-order-control.xml`）；原有 Touch fixture 单独运行 `18/18 PASS`（`phase6-real-touch-control.xml`）。这些文件均在上述同一个 outputs 子目录。因此已排除“必须先执行新增 M1 fixture 才失败”，但**完整 root cause 未确认，也未声称旧问题已修复**。本轮不修改旧 Phase 6 fixture；此项保持开放，Phase closeout 前需处理，不混入历史九项已接受 Minor。

中间的混合 228-case runs 与首次 33-case run 不是 GREEN。M1 输入诊断发现初始 Mouse event 未消费；新 helper 改用项目已有的 `device.lastUpdateTime + 0.000001` 安全测试时间，保留单次 wheel、消费确认和生产 Camera 断言，最终 M1 7/7。Unity 的 Editor→PlayMode transition-window 丢弃规则是该时钟调整的依据；未记录实际 transition-window 数值，因此不把这一机制写成已完整证明的根因。

Engineering / QA bounded code review：PASS；Studio Owner 现已确认 M1/M2 复测 PASS，但当前 overall Scene regression **不是 PASS**，不意味着 Phase 8 完成。

M1/M2 修复后，Studio Owner 曾明确反馈“1和2也pass了”，因此关闭这两项此前的 manual FAIL；不是由 automated PASS 推定。当前最新进度以本节顶部 M11–M18 验收记录为准。

### 本轮 review 修复记录（2026-09-04）

下面的“已完成”只指修复与对应 automated regression，不代表 manual PASS。

| 修复 / test case | Automated 状态 |
|---|---|
| 不同时产生普通家具与功能设备 Preview；Tab 不隐式 Cancel | 已完成 / PASS |
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

2026-09-04 当时停在 `Ready for Manual Review`；后续修复与验收结果以本节顶部记录为准。当前仍保留开放的 Scene/Input 验证问题与 M6 未覆盖子项；处理后才请求 Phase 8 完成判断，在此之前不标为 `Completed`，不自动进入 Phase 8R。

九项 accepted/deferred Minor 的完整 register 在 `.superpowers/sdd/2026-09-02-phase-8-functional-furniture-layout-readiness/task-10-report.md`。这些是已知 test / diagnostic / polish limitations，不是隐藏 blocker，也不会自动成为 Phase 8R scope。

## 7. 已知限制与下一步

当前九项 Minor 主要是缺少少数直接 regression、边缘 diagnostic 不够完整，以及一个环境敏感的 performance threshold；完整清单和影响说明见上面的 `task-10-report.md`。它们不是本轮隐藏 blocker，也不会自动变成承诺功能。

本轮验证没有注入真实磁盘 I/O 故障，也不替代窗口 / SafeArea 变化与操作手感的人工检查。自动截图使用临时 ScreenSpaceCamera 渲染，只供摆放和可读性参考，不是原生 Overlay 的逐像素验收。

M18 专项技术代测及独立复查已 PASS，Owner 无需重做；M1–M17 人工 PASS。下一步是确认旧 Scene/Input 顺序验证问题与 M6 未覆盖子项的处理，再完成 Phase closeout 验证。之后才能另行决定 Phase 8 是否完成并进入 `Phase 8R — Review & Polish`；Phase 8R 只打磨 Phase 1–8，不加入新 system / feature，其余 TBD。
