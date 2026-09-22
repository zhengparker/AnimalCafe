# Phase 8 — Functional Furniture & Layout Readiness Design

> 状态：Approved Phase 8 Baseline
>
> 日期：2026-09-02
>
> Project：AnimalCafe
>
> Lifecycle：Approved Design Baseline；Phase 8 **Completed — merged to main**（2026-09-21）。PR #7 合并、Owner 整体收尾决定、post-merge 回归与本地归档清理均已完成，最终结果统一见 `Docs/Phase8_Beginner_Guide.md` 第 37 节。
>
> Source of Truth：`Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md`

## 1. 给初学者的简单说明

Phase 1–7 让玩家可以装修咖啡厅。Phase 8 让游戏理解这些家具的用途，并判断当前布局是否已经具备未来营业所需的摆放条件。

例如，玩家把 Cash Register 放到 Counter 顶面后，不需要手动设置员工或顾客站位。系统会根据设备方向自动计算：

- 员工站在哪里操作；
- 顾客站在哪里点单；
- 这些位置有没有被墙、家具或 locked cell 挡住；
- 当前是否至少存在一组可用的 Cash Register、Coffee Machine 和 Pick-up Point。

Phase 8 的边界很明确：只提供 functional placement、automatic anchors 和 layout-readiness contracts。它不加入 cafe day loop、NPC movement、Order、Queue、NavMesh / pathfinding agents、economy 或 Save；画面显示“可以营业”只表示布局条件已满足，不表示咖啡厅已经会正式营业。

## 2. Goal

让经营系统通过 furniture capabilities、Surface Slot bindings 和自动 Interaction Anchors 使用玩家布局，而不是依赖固定 Scene object names，并通过 `LayoutReadinessReport` 判断咖啡厅是否具备最低营业条件。

## 3. Player-visible Result

Phase 8 完成后，玩家可以在现有 Decoration Mode 中：

- 像操作普通 furniture 一样 Preview、放置、移动、旋转、Confirm、Cancel 或 Store Cash Register 和 Coffee Machine；
- 但 Cash Register 和 Coffee Machine 只能放在兼容家具的空闲 `Surface Slot`，不能放在 Floor；
- 点击 Furniture Tab 内的 Pick-up Point 按钮，创建一个可移动、可 Confirm、Cancel 或 Store 的 Pick-up Point indicator；
- 创建多个 Cash Registers、Coffee Machines 和 Pick-up Points；
- 看见具体的 readiness feedback，例如缺少必要功能、anchor 被挡住或某个设备暂不可用。

## 4. Confirmed Design Decisions

### 4.1 Architecture

采用独立 `LayoutReadinessEvaluator` 方案：

```text
CafeLayout + FunctionalSurfaceLayout
        ↓
InteractionAnchorResolver
        ↓
GridReachabilityEvaluator
        ↓
LayoutReadinessEvaluator
        ↓
LayoutReadinessReport
```

- `CafeLayout` 继续负责 Floor furniture、rotation、occupancy 和 placement。
- `FunctionalSurfaceLayout` 负责 Surface Slot occupancy、mounted equipment 和 Pick-up Points。
- `InteractionAnchorResolver` 只计算 anchors，不负责角色移动。
- `GridReachabilityEvaluator` 只验证 Grid 连通性，不实现 Unity Navigation 或 NPC movement。
- `LayoutReadinessEvaluator` 汇总所有 station results 和整体营业条件。

### 4.2 Multiple Functional Points

系统不得假设每种功能只有一个实例。

- 每个 Cash Register、Coffee Machine 和 Pick-up Point 都有独立稳定 ID。
- 每个功能点按自己的 ID、binding、anchors 和 failure reasons 独立验证。
- `LayoutReadinessReport` 分别记录每种功能的 `TotalCount`、`ValidCount` 和 `InvalidCount`。
- 每种必要功能至少有一个有效实例，并且至少存在一组处于同一可达区域的 Cash Register、Coffee Machine 和 Pick-up Point，整体才允许营业。
- 其他无效功能点只标记为 unavailable 并产生 warning，不拖垮已经存在的有效服务组合。
- Phase 8 不永久绑定 Cash Register → Coffee Machine → Pick-up Point，也不决定角色如何选择或分流；这些属于 Phase 10–14。

## 5. Surface Slot Domain

### 5.1 Stable Address

一个 Surface Slot 使用以下组合定位：

```text
SupportFurnitureInstanceId + SurfaceSlotId
```

`SurfaceSlotAddress` 必须保持 value equality，且不得依赖 Scene object name。

### 5.2 Mounted Equipment

`SurfaceMountedInstance` 保存：

- stable mounted instance ID；
- equipment `DefinitionId`；
- `SurfaceSlotAddress`；
- equipment-local `FurnitureRotation`。

Cash Register 和 Coffee Machine 必须来自允许 `PlacementSurfaceType.FurnitureSurface` 的 definition。一个 Surface Slot 同一时间只能被一个 mounted equipment 或 Pick-up Point 占用。

2026-09-08 Studio Owner manual clarification：未确认 CR / CM ghost 与普通 furniture 使用相同悬浮高度，位于 Surface Slot 或地面上方，footprint 留在对应平面。地面位置仅用于 Preview 显示，footprint 必须红色 invalid 且不可 Confirm；松手后可重新抓起拖回桌面。Confirm 后设备位于正式 Slot，无悬浮；此调整不改变 Surface Slot domain 或 anchors。2026-09-09 补充批准：CR / CM ghost 保留原材质、颜色、纹理，validity 仅改变 footprint 与既有反馈，不染色本体。

### 5.3 Pick-up Point

`PickUpPointInstance` 保存：

- stable Pick-up Point ID；
- `SurfaceSlotAddress`。

Pick-up Point：

- 不是 `FurnitureDefinition`，也不进入独立 Catalogue row；
- 使用一个兼容的 `1 × 1 Surface Slot` footprint；
- 不提供 Rotate；
- Employee delivery anchor 和 Customer pick-up anchor 由系统自动选择；
- 可以存在多个；
- 不得与 Cash Register、Coffee Machine 或另一个 Pick-up Point 重叠。

2026-09-08 Studio Owner approved manual fix：indicator 使用细长、硬边立体倒四方棱锥，宽深约为原来一半，高度基本不变；独立灰白 opaque Lit 材质不随有效性变色，只有 `1 × 1` footprint 使用 valid / invalid 色。Preview 可在地面悬浮显示并松手重新抓起，地面始终 invalid、不可 Confirm；拖回空闲桌面可确认，Cancel 恢复来源。地面位置仅为 presentation，不新增 Floor placement domain。玩家 readiness 文本使用中文角色、站位坐标和原因，原始 instance / support / Slot IDs 只保留在 diagnostics，不拼入可见文本。

### 5.4 Support Furniture Changes

- 移动或旋转支撑家具时，mounted equipment 和 Pick-up Point 保持相同 `SurfaceSlotAddress` 并随支撑家具更新 world position。
- anchors 和 readiness 必须根据当前 layout 重新计算，不得继续使用旧结果。
- 支撑家具上存在 mounted equipment 或 Pick-up Point 时，禁止将该支撑家具 Store；反馈必须列出需要先移除的内容。
- 不采用自动删除、自动换 Slot 或产生 orphaned binding 的隐式行为。

## 6. Interaction Anchors

### 6.1 Cash Register

- 玩家不手动指定 anchors。
- 根据 Cash Register authored sides、equipment rotation、支撑家具 transform 和 Surface Slot 自动计算。
- 必须生成一个 Employee anchor 和一个相反方向的 Customer anchor。
- Queue 起始方向可以作为稳定结果输出，但 Phase 8 不生成 Queue。

### 6.2 Coffee Machine

- 玩家不手动指定 anchor。
- 根据 authored `+Z` Forward、equipment rotation、支撑家具 transform 和 Surface Slot 自动计算一个 Employee anchor。
- 不生成 Customer anchor。

### 6.3 Pick-up Point

- 玩家不旋转、不指定 anchors。
- resolver 先收集 North、East、South、West 四个相邻 Grid cells，再使用固定优先顺序自动选择 Employee delivery anchor 和 Customer pick-up anchor。
- 优先选择两个有效且互相相反的 cells；若没有 opposite pair，则选择两个不同的有效 cells；只有一个有效 cell 时两者共用该 cell。相同条件下按 North → East → South → West 决定结果。
- 两者可以共用同一 Grid cell；若没有有效相邻位置，该 Pick-up Point invalid，不能 Confirm。
- Phase 8 不决定未来 Pick-up Queue 的完整转弯和分流规则。

## 7. Grid Reachability

Phase 8 使用 deterministic Grid search，而不是 Unity NavMesh 或正式 NPC Navigation。

### 7.1 Walkability Inputs

- 已解锁的 Interior Grid cells；
- Floor furniture occupancy；
- Wall / boundary constraints；
- locked cells；
- Entrance / Exit 可达起点；
- 当前自动计算的 Interaction Anchor cells。

### 7.2 Individual Validity

每个功能点至少检查：

- definition / function type 正确；
- 支撑家具存在；
- Surface Slot 存在且兼容；
- Slot 未被重复占用；
- anchors 位于有效范围；
- anchors 未被 furniture、wall 或 locked cell 阻挡；
- anchors 位于需要的可达区域。

### 7.3 Overall Readiness

顾客侧至少需要存在可连通的：

```text
Entrance → valid Cash Register customer anchor → valid Pick-up customer anchor → Entrance / Exit
```

员工侧至少需要存在同一可达区域内的：

```text
valid Cash Register employee anchor
↔ valid Coffee Machine employee anchor
↔ valid Pick-up employee anchor
```

这只证明存在完整可用组合，不决定实际 runtime assignment。

## 8. Readiness Report

`LayoutReadinessReport` 必须：

- 提供 `CanOpenForBusiness`；
- 汇总全部 station results，而不是在第一个错误处停止；
- 区分阻止营业的 `Blocking` failure 和不影响有效组合的 `Warning`；
- 提供稳定 `FailureCode`；
- 提供 player-readable message；
- 在适用时包含 function type、functional instance ID、support furniture ID、Surface Slot ID 和 Grid position；
- 对 missing capability、blocked anchor、unreachable anchor、duplicate occupancy、missing support、missing slot 和 invalid service combination 给出具体原因；
- 不依赖 Scene object name 作为身份或业务规则。

Phase 8 只提供营业判断依据。正式营业系统尚不存在，因此不创建假的营业流程或 Order system。

## 9. Decoration Mode Interaction

### 9.1 Cash Register / Coffee Machine

操作流程与现有 furniture 保持一致：

```text
Catalogue selection
→ Scene Preview
→ Move / Rotate
→ Confirm / Cancel
→ existing item can Move / Rotate / Store
```

唯一核心区别是 placement surface：普通 furniture 使用 Floor；Cash Register 和 Coffee Machine 只使用兼容 Surface Slot。

### 9.2 Pick-up Point

```text
Pick-up Point button
→ create indicator Preview
→ move between compatible Surface Slots
→ Confirm / Cancel
→ existing indicator can Move / Store
```

- 每次点击 Pick-up Point 按钮创建一个新的 Preview；完成后再次点击可以创建第二个实例。
- 新建时 Cancel 取消创建；编辑已有点时 Cancel 返回原位。
- Store 删除已确认的 Pick-up Point。
- invalid footprint / binding 时禁用 Confirm。

### 9.3 Atomic Preview

- 只有 Confirm 修改正式 `FunctionalSurfaceLayout`。
- Cancel 必须恢复完整原状态。
- transaction 失败不得留下部分 occupancy、重复 binding、丢失设备或错误 readiness cache。

### 9.4 Approved editing feedback refinement（2026-09-09）

Owner 批准只实现三项 UX：已确认布局 / 当前 Preview 分层、错误持续可见、编辑锁定说明与 Return to Editing。顶部仅显示已发布 readiness，无 report 时留空；Preview Begin/Move/Rotate/Cancel 不发布。当前提示包含物件、尚未确认及具体原因；合法后替换为 valid 文案。

Catalogue 展开时承接提示并提供 Return，Return 仅恢复 chrome，不 Confirm/Cancel/替换或排队新选择；Modal 保持输入所有权。Floor/Wall 同 Preview 换样式不拦截。Store 失败保留具体原因，Store modal Cancel 不是 Preview Cancel；旧 Collapse 与重复 Store/Dismiss 也保留原因及 diagnostics。只有真实结束 Preview 才清本轮提示；正式 readiness 发布仍沿用原合同。

本轮三项 UX 的历史验证 authority：`Docs/Phase8_Beginner_Guide.md` 8.4。完整 PlayMode 789/789、Phase 8 EditMode + surface sessions 227/227 PASS；Owner manual Pending。当时只更新现有 UI runtime 与 P8 静态字体，不改 footprint/model/Camera/domain/Save。后续 UX2 以 9.5 为准，不沿用这组 PASS，不新增 Phase 或自动推进 closeout。

### 9.5 Approved UX2 refinement 与 footprint light（2026-09-09）

Owner 后续批准选项 2–5 与半透明光感 footprint；选项 1 保留现有 icon，Exit 文案不动。实现与 automated verification 已完成：完整 Editor PlayMode **813/813**（core 707 + Scene/Input 106）、全部 Phase 8 EditMode + SurfaceSession **235/235 PASS**，failed/skipped/inconclusive 均 0、exit 0。Footprint/font focused **8/8** 已包含在 235 内。独立 code 与 7 张截图复核无未处理 Critical/Important；交回 `Ready for manual review`，新增 7 项 Owner manual 全部 **Pending**。最终 XML、截图及 810/813 中间非 GREEN 的三处旧断言迁移见 Beginner Guide 8.5。

本轮没有运行 full EditMode、standalone Player 或 Android/iOS 真机；320 logical pixels 的窄屏检查为 runtime 真实 Prefab fixture。资产 audit 在已批准 footprint/font authoring 后取基线，full regression 前后 1,393 个非 C# Assets + ProjectSettings hash 一致、changed/missing/unexpected 为 0；它不否认本次新增 shader/material、两份 footprint Prefab 与 P8 static font 修改。Scene / ProjectSettings 未改。

- Catalogue memory 仅限当前 Decoration session：按 Tab 保存竖向、按 CategoryId 保存横向，布局更新后 clamp；切换/reset 停止旧 nested drag 与惯性，新 session 清空。无 active Preview 的收起入口显示“继续添加”，有 Preview 保留 Catalogue / Return 限制；Confirm 后不自动展开或连续放置。
- Readiness 使用紧凑 summary 与可展开完整 details；blocking、warnings、角色与位置不丢失。只切换表现，不更新 report/version，不改变原发布时机；Preview 不覆盖 confirmed report，无 report 时留空。
- 只对新 Cash Register / Coffee Machine 的合法空 Slot 做 Camera 排序：优先处于 Camera 裁剪范围、投影在 viewport 内且距屏幕中心最近者；同距和无可见合法候选时保留稳定 fallback，Camera/marker 缺失也可 fallback。不新增遮挡检测或自动 Camera/Confirm；Pick-up Point 的初始排序保持原合同。
- Floor 说明区显示 Whole Room / 逐格范围及 proposed 与 confirmed style/rotation 的真实差异格数。Undo / Apply All 仍只影响本次 Preview；Whole Room 的 Undo 继续禁用，中文 footer 不扩展可执行动作。
- 仅替换普通家具地面、墙饰、台面 CR/CM、Pick-up Point 的 footprint 填充为半透明、边缘柔和的绿/红光感。四路径仍使用原尺寸、旋转、位置及 valid/invalid 判定；保留白色取餐 icon/锥体、模型和入口蓝色区域，不新增 Light/Bloom 或模型换色。

没有扩大 placement domain、Save、Camera 手势、全局 Undo、icon/模型重做或 Phase scope。透明层级、窄屏可读性及最终视觉接受按 UX2 manual ledger 独立验证；技术通过不代替 Owner PASS。

2026-09-09 brightness follow-up：Owner 批准只提亮 footprint 并保留透明度；本版 shader / authoring 实现参数为 `_TintBrightness = 4`、`_TintSaturation = 1.5`，material 仅增加两个 float，数值并非 Owner 亲选。opacity 0.45、softness 0.12、bounds/depth 不变，模型/icon/Theme UI 不动，无 Prefab/Scene 改动。真实 ThemeValid/ThemeInvalid/WallValid 像素测试加旧项共 **10/10**、直接 PlayMode **84/84 PASS**，两套 failed/skipped/inconclusive 均 0、exit 0，authoring exit 0。813/235 仅为提亮前 baseline，本次未重跑 full Phase。独立 code review 无 Critical/Important，WallInvalid 未单测为 Minor；4 组前后截图独立技术复核无阻挡，Owner 偏好 Pending。最新 XML、截图与人工复测见 Beginner Guide 8.6，交回 Ready for manual review，不改变 Phase gate。

2026-09-09 signal-light follow-up（当前 authority，取代上段配色参数）：Owner 批准更接近红绿灯的投影亮度。实现参数为 brightness **6** / saturation **3** / light intensity **1.5**，opacity **0.45** / softness **0.12** 不变；仅 footprint RGB 预乘混色，保持 `C×alpha + B×(1-alpha)`，无 Light/Bloom、Camera/HDR 设置、Scene/Prefab、Theme/model/icon 或操作规则修改。最终 focused EditMode **15/15**、直接 PlayMode **84/84 PASS**，failed/skipped/inconclusive 0、exit 0；新增真实 WallInvalid 与 HDR/LDR 亮底检查，4 组前后图独立技术复核无阻挡。最终显示可截断主色高光，不承诺所有亮底完整保留纹理。最新证据与人工步骤见 Beginner Guide **8.7**；Owner manual Pending，Ready for manual review，不扩大 Phase gate。

### 9.6 Natural Preview follow-up（2026-09-09，最新 Preview authority）

Owner 批准普通家具与 CR/CM ghost 保留原材质/颜色/纹理/已有外观属性，红绿由 footprint 承担，原 symbol、原因和 Confirm validation 不变。Wall Decor / Window ghost 仅 Preview 沿目标墙的 `-forward` 额外悬浮 0.20 m（开发试调值），Confirm 恢复原 Base Wall Surface 1 mm contact；底部高度、实际占格、投影饰条避让平面不变。禁止只把 Preview 垂直居中或缩 footprint 来匹配模型轮廓。

覆盖 9.5 历史 follow-up 中模型颜色/墙饰 Preview 位置不变的约束；signal-light 参数继续沿用。三个 View 的局部变更，无 Scene/Prefab/Theme/Camera 配置、Save/domain 或正式放置位置迁移。直接 PlayMode **301/301 PASS**（失败/跳过/不确定均 0，exit 0），含真实 prefab 与 MainCafe 相框/搁板、跨墙/Confirm/Cancel 和原色状态切换；不是 full Phase/Player/mobile 验证。Guide **8.8** 保存新 XML、截图与四项新增 manual（全部 Pending），旧验证数字不改写。

## 10. UI Design

### 10.0 Approved mobile UI follow-up（2026-09-13）

最新批准的小墙饰抓取修复：Wall Decor Preview 的完整renderer bounds进入既有floating action避让，保留48×48 logical点击区、30底板／18 icon／约12.03间距与Refined B样式。UI先判定；Began允许从可见ghost的enabled renderer bounds抓取当前显示墙格，Current仍投射真实墙格，不能黏在ghost上。保留空白场景Camera ownership，不启用ghost Collider、不改Confirm／Cancel或正式布局。实际生产修改仅DecorationModeController；证据、两项修复前同样失败的旧测试与manual review边界见Beginner Guide第32节。

最新批准的紧凑操作与时间动效 follow-up（优先于下文旧尺寸）：浮动工具底板30、icon18，2／3／4按钮均通过外观向组中心平移，将可见间距收至约12.03 logical；独立48×48 touch root及其位置保留，不重叠，外观仍完整位于各自触控范围内。模式状态框与时间条统一144.03125×32；竖屏上下排列、可见间距8，横屏保持并排策略且间距8。时间条沿用18／18／26的icon，橘色背景以0.18秒ease-out移动，只有背景移动，实际速度立即生效；快速连点从当前位置重新定向，不排队；使用unscaled时间，Pause不冻结动画。首次加载／重启用按实际速度定位，Decoration锁定隐藏橘色并显示原灰色，退出后反映恢复速度；真实尺寸变化结束旧动画并重新对齐。只改局部presentation，不改PNG、依赖、配色、字体、Scene／Prefab、时间service或Preview规则。验证与截图记录见Beginner Guide第31节。

上一轮比例 follow-up：Floor 范围与 footer 文字为12 logical、底板高32，收紧横向留白；保留至少48 logical且非重叠的touch root，不改Wall footer。时间改为连续三段纯icon条，高34，细分隔线；pause／1x／2x仍是三个独立48 logical目标，保留18／18／26的icon、原速度与装修锁定语义及Refined B状态配色。所有有名称的卡片外框保持68×84，caption从13改11.5、区域62×34；浅色thumbnail well为58×40、thumbnail为52×34，完整两行名称与ASCII尺寸标注保留。没有名称的Floor／Wall surface卡片布局不变。小字号是Owner明确批准的密度取舍，真机可读性仍待人工验收；此轮不改PNG、Scene、Prefab、存档或Preview规则。验证与截图记录见Beginner Guide第30节。

上一轮间距／尺寸修订（同日，部分尺寸已由上段更新）：时间按钮保持34底板／48 touch root，移除额外root间隙，可见间隔14；四Tab恢复相邻等宽长方形底板，高34，纯icon和原配色不变；全目录卡片68×84，heading14、caption13 logical并保留两行与缩略图内底色，ASCII尺寸标注固定放第二行，数字与x不拆分；浮动操作底板32／touch root48、不保留可见额外间隙（仅1/64 logical浮点保护），可见间隔约16。仅当`CanOpenForBusiness && Failures.Count == 0`时隐藏整条readiness及其输入／布局占位，保留最新诊断报告；warning和blocking仍显示。字号13是Owner批准的小一档密度取舍，不按旧14下限验收。测试与人工检查记录见Beginner Guide第29节；不更改存档、素材或Preview交易规则。

Owner 已批准在现有 Refined B UI 上调整手机触控排版：普通 targets 至少 48 平台 logical units，主要操作 48–56；正文 16–18、卡片名称至少 14。保持原 CanvasScaler，通过 Android view/density、iOS view points 或明确标记的 Editor profile／Estimated fallback 换算 Canvas units；不把截图 pixels 或 Unity units 当作真实 dp／pt。窄屏、横屏与 Safe Area 通过换行、紧凑排列和滚动适配，不能缩小 target 或叠加相邻点击区。

Whole Room／Single Grid 使用两张彩色范围 icon，保留横排 icon＋文字及选中态颜色。必要目标选择指引位于顶部中央、实际 HUD/readiness 下方，不再依附底部动作栏；最多两行且按内容测高，等待选取时持续显示、选取后隐藏，弹窗遮挡时隐藏且按当前状态恢复，完全不拦截输入。已删除的 Current Preview／general error explanation 不恢复，confirmed-layout readiness 保持独立。目录只有实际纵向溢出时显示轻量滚动提示。

响应式细节已落实：短手机将分类与折叠按钮同排；宽屏 Return 占标题位，不额外吃一行卡片；收起目录时四 Tab 按真实标题显隐居中。无 Preview／无 Return 的单排 Floor 目录仅保留完整内容所需高度，释放场景选格空间；短横屏空间不足时将两颗范围按钮放在卡片右侧。卡片有效触控面积按 viewport 裁切后的交集及实际 raycast 验证，不只看 RectTransform 尺寸。极小手机叠加较大 SafeArea 后，仍可能需要先折叠目录再选格，此限制进入真机 manual review。

不修改 Refined B 配色、world pickup sign/animation、Footprint、正式布局数据、Preview Confirm/Cancel、跨 Tab 自动取消或 Modal 输入所有权。本轮工程与验证记录见 `Docs/Phase8_Beginner_Guide.md` 第 27 节：78 项相关 EditMode、603 项较广 PlayMode、最后排版变更后的 63 项 UI 重跑通过，最终 104 张截图完成自检；各范围重叠，不累计为全项目验证。UI 工作已完成，但初次未筛选基线意外改写的 Phase7 材质与两个验证场景仍待 Owner 授权恢复核查，整体尚未 Ready for manual review。不自动关闭 Phase 8；Android/iOS 编译、真机触控及 Owner acceptance 仍 Pending。

2026-09-13 紧凑化 follow-up 已获 Owner 批准，实现与限定工程／视觉验证完成：四个 Tab 普通浏览目录以实际 Safe Area 高度的 45% 为上限，内容不足时收紧、溢出时滚动；按钮底板／icon／卡片的可见尺寸约缩至上一版 70%，正文只适度减小，卡片仍至少 14 logical 两行。48 logical、非重叠的真实点击区域及原有 PNG／Refined B 视觉合同保留，不改 CanvasScaler 或平台 logical 换算。提示卡独占其范围内的滚轮（包括滚动边界及无需滚动的状态），场景才缩放；modal 阻止 Camera。目录维持既有“滚轮缩放 Camera、目录自身不滚动”的合同，不与提示卡混用。严格全状态 45% 仍为 PARTIAL：普通最小手机（480×854）及窄 Safe Area 的 Floor Preview、部分短横屏 Surface Preview，以及普通横屏 Furniture Preview 带 Return＋Pickup 时，保留实际可操作高度，尚未获 Owner 接受例外；不将测试通过等同于完成限高要求。结果与未完成项见 Beginner Guide 第 28 节；先前待授权的恢复核查不属于本次工作。

Furniture Tab 保持现有 Phase 7 visual language、card layout、spacing、scroll 和 interaction，增加：

```text
Furniture
[existing furniture cards]

Cash Register
[cash register cards]

Coffee Machine
[coffee machine cards]

[Pick-up Point button]
```

- Cash Register 和 Coffee Machine 使用独立 Catalogue rows，为未来多个型号保留扩展位置。
- Pick-up Point 使用按钮，不使用独立 row。
- 不创建新的 UI architecture，也不启动全面 UI visual redesign。
- Readiness feedback 使用现有 feedback patterns，显示整体状态和具体问题；点击问题突出相关设备、Slot 或 Grid cell 的能力只在实现成本不扩大系统边界时纳入。

## 11. Pick-up Point Indicator

- 使用倒四方棱锥箭头，在目标 Surface Slot 上方浮动。
- 显示对应 `1 × 1` footprint 和 valid / invalid feedback。
- 只在 Decoration Mode 中可见。
- 退出 Decoration Mode 后全部隐藏；数据继续存在。
- 再次进入 Decoration Mode 后，根据当前 confirmed layout 重建 indicators。
- 最终视觉接受由 Studio Owner 在 Play Mode 手工判断。

Phase 8 validation Scene 另外提供 developer-facing anchor gizmos / debug feedback，用来核对自动计算的 Employee / Customer anchors、角色侧和 Grid cells。Debug 表现不进入正常经营画面，也不要求玩家手动编辑 anchors。

## 12. Error Handling 与 Recovery

- invalid Preview 不能 Confirm，但可以继续移动、Cancel 或选择其他 Slot。
- missing support / missing slot / invalid definition / duplicate occupancy 返回明确 failure，不 crash。
- 读取到 orphaned 或损坏 binding 时，report 标记 blocking failure；runtime 不猜测替代 Slot。
- 移动或旋转支撑家具后立即使旧 readiness 结果失效。
- 无效但结构完整的装修可以保留；当前 `CanOpenForBusiness` 为 false。
- 非必要的额外设备只有普通 anchor / reachability 失败时，才可在另一组完整有效组合存在时降为 warning；损坏 binding / definition / direction 数据始终 blocking，不能用额外有效设备掩盖。
- 缺少方向配置的已确认设备仍保留在 Stations、InvalidCount 与具体 failure 中，不从 report 消失。
- readiness 按每项 failure 关联设备、中文角色、站位坐标与原因；自身 IDs 只留在 diagnostics，不拼入玩家文本。长文本在顶部 SafeArea 面板内换行、滚动。
- 一次只允许一个 Preview。2026-09-12 P8R follow-up：切到另一个 Tab 时先完整 Cancel 未确认 Preview，再切换；重复点击当前 Tab 保留 Preview。新物件的临时显示移除，已有家具／CR／CM／Pick-up 恢复正式位置和绑定；confirmed Layout、readiness report/version 不变。Store / Exit 弹窗仍阻止后方 Tab 操作；CR / CM / Pick-up 的 Store Cancel 保持 Preview，确认后才修改正式 layout。
- Furniture Catalogue 不显示绿色 Using check（包括 Counter、Cash Register、Coffee Machine）；可重复摆放的家具不能被表现为互斥选项。正在编辑的条目仍显示 Preview 虚线框；Floor、Wall、Wall Decor 的既有 Using check 规则不变。
- Pick-up 无可用相邻站位时，action bar 必须给出专用原因，同时保留上一条正式 readiness。

Editor 配置工具的安全边界：

- Scene Configure 与 Build Assets 分开。依赖缺失、目标 Scene dirty 或项目 asset dirty 时，先明确拒绝，不隐式保存或重建其他资源。
- 对已存在 Scene，先验证候选配置再保存；失败恢复原 Scene、加载状态、顺序与 active Scene。唯一 loaded Scene 使用临时空 Scene 完成回滚。
- 首次复制 validation Scene 之前也执行相同 preflight。反馈容器的 parent、sibling 和 SafeArea 修复必须落盘；重复正确配置保持 byte-stable。

## 13. Expected File Boundaries

### 13.1 New Runtime Domain Files

预计在 `Assets/Scripts/Layout/` 新增：

- `SurfaceSlotAddress.cs`
- `SurfaceMountedInstance.cs`
- `PickUpPointInstance.cs`
- `FunctionalSurfaceLayout.cs`
- `InteractionAnchor.cs`
- `InteractionAnchorResolver.cs`
- `GridReachabilityEvaluator.cs`
- `StationReadiness.cs`
- `LayoutReadinessFailure.cs`
- `LayoutReadinessReport.cs`
- `LayoutReadinessEvaluator.cs`

### 13.2 New Decoration Runtime Files

预计在 `Assets/Scripts/Decoration/` 新增：

- `FunctionalSurfacePlacementPreview.cs`
- `FunctionalSurfaceDecorationSession.cs`
- `SurfaceMountedSceneRegistry.cs`
- `SurfaceMountedPreviewView.cs`
- `PickUpPointIndicatorView.cs`

### 13.3 Existing Runtime Files Likely Modified

- `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- `Assets/Scripts/Decoration/DecorationModeController.cs`
- `Assets/Scripts/Decoration/DecorationCatalogueAsset.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueModels.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- input hit-kind / routing files only where required to preserve current mouse and future touch ownership contracts。

### 13.4 Editor, Assets, Scene and Tests

- 新增 `Assets/Editor/Phase8/` 下的 asset builder、Scene setup 和 validator。
- 新增 Phase 8 UI / indicator assets and prefabs；沿用现有 presentation。
- 新增 `Assets/Tests/EditMode/Phase8/` 和 Phase 8 PlayMode / EditorSceneLoading tests。
- 更新 `Assets/Scenes/Validation/` 中的 Phase 8 validation Scene，并通过 idempotent setup 接入 `MainCafe`。

最终 exact file list、interfaces 和 task-by-task edits 由 approved implementation plan 锁定。

## 14. Explicitly Not Included

简单说，本 Phase 只回答“功能家具放得对不对、自动站位在哪里、这套布局以后能不能支持营业”，不负责让营业流程真的跑起来。

- NPC、Customer 或 Employee movement。
- Order domain、capacity、task assignment 或 service timing。
- 正式 Queue、queue turning 或多队伍分流。
- Unity NavMesh、正式 path following 或 avoidance。
- Coffee production、payment、inventory、pricing 或家具商店。
- 自动绑定某台 Register、Coffee Machine 和 Pick-up Point。
- 新的 Decoration system、全面 UI visual redesign 或 Phase 8R Polish。
- Save / Load 和 migration；这些属于后续 Phase。
- 本轮 Android / iOS 真机 acceptance。正式目标仍为 Android / iOS Touch（见 Project Design）；Windows Mouse 只用于当前 Unity Editor 开发与验证，不是独立 release target。

## 15. Verification Boundary

- Task 开发使用 focused RED → minimal implementation → focused GREEN → direct regression。
- Phase 收尾集中运行完整 EditMode、PlayMode、real Scene / input integration regression。
- automated tests 与 Studio Owner Manual Tests 分开记录。
- automated PASS 不代替 UI、操作手感和 indicator 可读性的 Studio Owner acceptance。
- 2026-09-08 Owner 明确授权例外：M18 开发用 anchor debug 检查改由 Codex 在 real Scene 执行并记录技术证据；验收标准不变，不计为 Owner 人工 PASS，也不替代 M1–M17 的玩家体验验收。
- 具体 cases 由 `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` 定义。

## 16. Lifecycle 与剩余 Gate

2026-09-21：Owner 确认朋友 review 完成，并授权合并、Phase 8 完成、本地 fast-forward 与归档后清理。PR #7 的 merge commit 为 `8ee0026247c2c3200bd1bc9ac471b7007e4a4f32`；最终 merged-main 验证和完成状态见 Beginner Guide 第 37 节。该整体决定不补造逐项 manual PASS、GitHub formal approval、Player build 或 Android/iOS 真机结果；本次不启动 Phase 8R / Phase 9。

以下为 2026-09-04–09 的历史 gate 记录，保留原始结果与未覆盖项，不作为当前工作状态：

本 design 与配套 test cases 已获批，现作为 Phase 8 baseline；这里的 `Approved` 只表示设计合同已确认，不表示 Task 10 或 Phase 8 已完成。

当前门槛分开记录：

- Automated：本轮 review 修复的最新结果与 XML authority 见 `Docs/Phase8_Beginner_Guide.md` 第 6 节；旧 Task 10 final report 的数字是修复前历史快照，不能代替本轮验证。
- Engineering：最终独立 department decision 为 `PASS`。
- QA：最终独立 department decision 为 `PASS`；P8-M-018 Beginner Guide 可观察性 Minor 已修正。
- Production：final focused re-review 为 `PASS`（`0 Critical / 0 Important / 0 Minor`）。
- Studio Owner Manual / 授权代测（2026-09-08）：M1–M17 为 Owner 人工 `PASS`；M18 经明确授权由 Codex 专项技术代测 `PASS`，最终 PlayMode 15/15（含 4 个新增 M18 tests）、独立 QA Spec / Quality 均 PASS，不计为用户亲自执行。M6 incompatible Slot 子项仍未覆盖，最新记录见 Beginner Guide 第 6 节。

上方最终 department decisions 为 2026-09-04 baseline，不替代当前验收；9 月 8 日 M6 / 玩家提示修复与 M18 代测历史见 Beginner Guide 第 6 节。

2026-09-09 第一轮 bug 修复快照（UX 实施前）：Owner 批准的五项 review 修复及旧 Touch 顺序问题已技术完成。当时完整 PlayMode 781/781、直接 EditMode 83/83、原始 Touch control 26/26 PASS；证据见 Beginner Guide 第 6 节。之后三项 UX 的 789/227 历史验证见 Guide 8.4 / 本 spec 9.4；当前 UX2 的最新合同见 9.5，技术验证 813/235 PASS、manual 7 项 Pending，交回 Ready for manual review，证据见 Guide 8.5。其他未列入 UX2 的建议仍未实施。没有用历史 full EditMode / Player 结果代替 UX2；M6 incompatible Slot 子项仍未覆盖，不自动宣布 Phase Completed 或进入 Phase 8R。
