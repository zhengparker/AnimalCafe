# AnimalCafe Phase 4 Beginner Guide

> **用途：** 这是 Phase 4（Core Architecture & Functional Furniture Models）的 Studio Owner 手动验收 runbook 和记录表。它不执行验收，也不把自动化结果当成人工验收。
>
> **当前状态（2026-08-11）：** Studio Owner 已完成并接受 `M01–M88`；Cash Register development 与 future commercial-release rights 均为 `Yes`。Merge-preparation fresh Unity 验证为 Full EditMode `570 / 570`、Editor PlayMode `62 / 62`、Windows standalone PlayMode `62 / 62`、P3 validator `3 / 3 valid`、P4 validator `5 / 5 valid`，failed、skipped、inconclusive 与 validator issues 均为 `0`；PlayMode assembly 不引用 `UnityEditor`。Phase 4 状态为 **Completed**；本记录不自动开始 Phase 5。

## 先理解这次要看什么

Phase 4 建立了以后家具会共用的“资料卡 + Prefab + 空间标记”合同。简单说，`FurnitureDefinitionAsset` 决定一件东西是什么、占几格、能放在哪里；Prefab 决定模型、Collider 和空间标记在哪里。两者不可互相猜测：`Footprint` 不是从模型尺寸或 Collider 自动算出来的。

这次场景用于验收，不是游戏正式玩法。还没有 Decoration Mode、拖放放置、Customer AI 或完整 Coffee gameplay。

## 范围和不能做的事

- 只在 `E:\Unity\Project\AnimalCafe\.worktrees\phase-4` 操作。
- 使用 Unity `6000.5.5f1`。
- 本文记录 **Studio Owner 实际看到和实际执行** 的结果；本轮 `M01–M88` 已全部完成并记录为 `Passed`。
- 不要在本 runbook 中把 `Passed` 当作“测试应该会过”。只有你亲自完成该条，才可选择 `Passed`。
- 不要因本指南而修改 `Docs/AnimalCafe_Development_Roadmap.md`、`Docs/AnimalCafe_Project_Design.md`，也不要开始 Phase 5。

## 现有自动化证据（参考，不等于人工验收）

以下是最终验收前在 Unity `6000.5.5f1` 串行运行的 fresh evidence。Studio Owner 的人工判断另记录在本表和 Excel tracker 中。

| 自动化 gate | 已记录结果 | 证据位置 |
|---|---:|---|
| Full EditMode | `570 / 570` passed | `outputs/phase4-manual-review/final-editmode.xml` |
| Full Editor PlayMode | `62 / 62` passed | `outputs/phase4-manual-review/final-playmode-green.xml` |
| Windows standalone PlayMode | `62 / 62` passed | `outputs/phase4-manual-review/final-standalone-playmode.xml` |
| RealUi focused PlayMode | `5 / 5` passed | `outputs/phase4-manual-review/focused-realui-round2.xml` |
| P4 production validator | `5 / 5 valid`, `0 issues` | Full EditMode XML：`ProductionContent_AllApprovedDefinitionsPassPhase4Validation` |
| P3 benchmark validator regression | `3 / 3 valid`, `0 issues` | Full EditMode XML：`ProductionBenchmarks_NonReadableImportedMeshesCompleteValidation` |

最终 gate 仍应在手动检查后按项目流程重新收集所需 XML、validator 输出、Console 状态和本表的人工证据；不要把上表复制成手动 `Passed`。

## 开始前：正确打开项目

1. 打开 Unity Hub。
2. 选择项目路径 `E:\Unity\Project\AnimalCafe\.worktrees\phase-4`，确认 Editor version 是 `6000.5.5f1`，再点 **Open**。
3. Unity 完成 import 后，先看底部 **Console** window：菜单 **Window > General > Console**。
4. 点击 Console 右上角的 **Clear**。不要先勾选任何 M 项；下一节的 `M05` 是确认清空后的状态。
5. 在顶部菜单运行 **AnimalCafe > Phase 4 > Build Validation Scene**。这是可重复运行的 Scene builder。
6. 再运行 **AnimalCafe > Phase 4 > Validate Production Content**。Console 会显示 `valid=N invalid=N issues=N`；若有 issue，先复制完整文字到对应证据栏，不要只记录“失败”。
7. 在 Project window 打开 `Assets/Scenes/Validation/Phase4CoreArchitecture.unity`。
8. 再次点击 **Clear**，然后点击 Unity 顶部中间的 **Play** 进入 Play Mode。
9. 依顺序执行下面 `M01–M88`。需要看 cyan / magenta / green / yellow / blue gizmo 时，先选中对应 GameObject；必要时确保右上角 **Gizmos** 已开启。
10. 全部实际执行的项目记录后，停止 Play Mode（再次点击 **Play**）。然后再次运行 **AnimalCafe > Phase 4 > Build Validation Scene** 和 **AnimalCafe > Phase 4 > Validate Production Content**，并记录输出。

若任一 validator 报错，不要靠手改 Scene 来隐藏问题。把 `IssueCode`、Asset path、完整 message 和截图/Console 输出填入证据栏，再按 P4 的 RED → GREEN 流程处理。

## Inspector 字段：英文名与中文含义

在 Project window 单击一个 `.asset`，右侧 **Inspector** 会显示该 asset。以下是常用路径和字段；英文字段名保留，方便你在 Unity 搜索或和代码对应。

### Furniture Definition assets

路径：

- `Assets/Art/Phase4/Definitions/FD_Furniture_WorkTable_01.asset`
- `Assets/Art/Phase4/Definitions/FD_Furniture_CounterModule_01.asset`
- `Assets/Art/Phase4/Definitions/FD_Equipment_CoffeeMachine_01.asset`
- `Assets/Art/Phase4/Definitions/FD_Equipment_CashRegister_01.asset`

| Inspector English field | 中文解释 | 要点 |
|---|---|---|
| `Definition Id` (`definitionId`) | 这张家具资料卡的稳定 ID；也可称 `Stable ID`。 | 保持 lowercase 既有格式，不能重复。不要改名来“修复” validator。 |
| `Display Name` (`displayName`) | 给人看的名称。 | 它不代替稳定 ID。 |
| `Footprint Width` (`footprintWidth`) | Floor Grid 的宽度（格）。 | 至少为 `1`；是 gameplay 数据，不从模型大小推断。 |
| `Footprint Depth` (`footprintDepth`) | Floor Grid 的深度（格）。 | 与 Width 一起构成占格；90°/270° 时会交换。 |
| `Allowed Placement Surfaces` (`allowedPlacementSurfaces`) | 允许放置的空间种类。 | 例如 `Floor` 或 `FurnitureSurface`；不是模型高度。 |
| `Function Type` (`functionType`) | 家具将来承担的功能类型。 | 当前只有 `None`、`CoffeeMachine`、`CashRegister`；它不等于 marker。 |
| `Prefab` (`prefab`) | 这张资料卡真正引用的 Unity Prefab。 | 不可缺失；Prefab 内的 marker/Collider/Material 另行验证。 |

`FurnitureDefinitionAsset` 的职责是把 Inspector data 转成不持有 `GameObject` 的 runtime definition；Unity 的 `Prefab` 映射留在 adapter/catalogue。因而不要把 `Collider` 尺寸误当成 `Footprint`。

### Window、Wall 与 Entrance authoring

| Asset / component | 路径或对象 | Inspector English field | 中文解释 |
|---|---|---|---|
| Window definition | `Assets/Art/Phase4/Definitions/WD_Wall_Window_01.asset` | `Definition Id`, `Display Name`, `Footprint Width`, `Footprint Height`, `Prefab` | Window 是 Wall-mounted definition。它固定使用 Wall footprint，不提供 P4 自由旋转控制。 |
| Wall | `PF_Environment_Wall_BackLeft_8x3.prefab` / `PF_Environment_Wall_BackRight_8x3.prefab` | `Surface Id`, `Columns`, `Rows`, `Slot Size` | 每面 Wall 独立拥有 `8 × 2`、每格 `1m × 1m` 的 Wall Slots。 |
| Entrance | `PF_Environment_Entrance_2x2.prefab` | `Entrance Id`, `Origin X`, `Origin Y` | 入口的稳定 ID 和 `2 × 2` clearance 起点；该区域可走，但家具不得占用。 |
| Counter / Work Table slot | 对应 Prefab 子物体 | `Slot Id` | `SurfaceSlotMarker` 的稳定 local ID。cyan gizmo 表示可用台面 slot；不是自动从 `Footprint` 产生。 |
| Coffee Machine direction | Coffee Prefab 子物体 | `ForwardMarker` / local `+Z` | Coffee Machine 的 employee side 必须是 Unity local `+Z`。 |
| Cash Register side | Cash Register Prefab 子物体 | `Side Type`, `Local Direction` | 需刚好一个 `Employee` 和一个 `Customer`，且相反；green 为 Employee、yellow 为 Customer gizmo。 |

## Cash Register source-rights gate（必须由 Studio Owner 填）

Cash Register 的新来源是 `Blender Model Item/vintage computer monitor 3d model.glb`；旧 `pos terminal 3d model.glb` 不是 P4 候选，但并未删除。自动化测试不能替你确认版权、商用授权或来源许可。

在 `M01` 之前/同时，Studio Owner 要写下明确结论（不要只写“应该可以用”）：

```text
Source asset: Blender Model Item/vintage computer monitor 3d model.glb
Permission for development: Yes
Permission for future commercial release: Yes
Evidence location (license, purchase record, creator permission, or owner statement): Studio Owner statement in the Codex P4 development task
Studio Owner name: Studio Owner / Creator
Date: 2026-08-04
Decision:
```

若任一项不是明确 `Yes`，Art gate 为 `Blocked`；即使所有 tests 都绿，也不能把 Phase 4 标为 Completed。

## 记录规则

每条 M 项的 `Status` 只能在实际检查后选择一个：`Passed`、`Failed`、`Blocked`，或有原因的 `Not Applicable`。截至 2026-08-08，下表 `M01–M88` 已由 Studio Owner 完成并接受，全部记录为 `Passed`。

- `Evidence` 至少写：日期、场景/Prefab/Inspector 路径、你实际做的操作、看到的结果。
- `Failed` 写：可复现步骤、Console/validator 完整错误或截图文件名、影响范围。
- `Blocked` 写：卡住原因和需要谁提供什么（例如 license proof、Unity 登录、缺失 source）。
- `Not Applicable` 只能用于确实不适用的条目，并写原因；不能把“还没做”写成 N/A。

## M01–M88 手动验收记录

### A. Pre-development and rights（M01–M05）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M01 | 按上方模板确认新 Cash Register source 同时允许 development 与未来 commercial release。 | Passed | 2026-08-04；Studio Owner 在 Codex P4 development task 中明确回答两项均为 `Yes`。 |
| M02 | 在 `E:\Unity\Project\AnimalCafe\.worktrees\phase-4` 以 Unity `6000.5.5f1` 打开项目。 | Passed | Unity Hub/Project path 与版本。 |
| M03 | 检查 P4 worktree 的 baseline，没有把无关改动当作本次验收结果。 | Passed | `git status --short` 输出或截图。 |
| M04 | 确认 main checkout 未被此过程用于编辑，P4 工作仅发生在 phase-4 worktree。 | Passed | main 与 phase-4 路径/状态记录。 |
| M05 | 在进入 Play Mode 前清空并查看 Console，没有遗留未解释的 P4 error。 | Passed | Console 截图或 error count。 |

### B. Inspector（M06–M14）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M06 | 打开 `FD_Furniture_WorkTable_01.asset`，核对其 `Definition Id`、`Prefab`、`Footprint Width/Depth`、`Allowed Placement Surfaces`、`Function Type` 可读且符合 Work Table。 | Passed | Inspector 截图；实际值。 |
| M07 | 打开 `FD_Furniture_CounterModule_01.asset`，核对同一组字段，确认 Counter 为 `1 × 1`、Floor、`None`。 | Passed | Inspector 截图；实际值。 |
| M08 | 打开 `FD_Equipment_CoffeeMachine_01.asset`，核对 FurnitureSurface 与 `CoffeeMachine`。 | Passed | Inspector 截图；实际值。 |
| M09 | 打开 `FD_Equipment_CashRegister_01.asset`，核对 FurnitureSurface 与 `CashRegister`。 | Passed | Inspector 截图；实际值。 |
| M10 | 打开 `WD_Wall_Window_01.asset`，核对 Window definition 的 `Footprint Width/Height` 和 `Prefab`。 | Passed | Inspector 截图；实际值。 |
| M11 | 在 Inspector 中确认 `Footprint Width` / `Footprint Depth` 的单位是 Grid cells，字段可理解，且最小值限制为 `1`。 | Passed | 观察说明；如测试修改，先恢复原值。 |
| M12 | 对一个可安全恢复的复制/临时 asset 查看 invalid Width、Depth、duplicate ID 或 missing Prefab 的 validation message；不要破坏 production asset。 | Passed | 临时对象路径；完整 message；恢复证明。 |
| M13 | 打开 `FC_Phase4Production.asset`，确认 Ceramic Cup 不在 Furniture catalogue；它只是 transient product visual。 | Passed | Catalogue Inspector 截图；条目列表。 |
| M14 | 确认 Window 使用固定 Wall footprint，P4 没有自由 rotation authoring control。 | Passed | Window Inspector/fixture 截图。 |

### C. Environment（M15–M31）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M15 | 在 `Phase4CoreArchitecture.unity` 确认 Floor visual surface 对应 `8 × 8` initial grid。 | Passed | Scene 截图；选中对象。 |
| M16 | 从正常 Camera/Scene 角度确认 Floor 与 Grid overlay 对齐。 | Passed | 截图；Camera 视角。 |
| M17 | 移动 Scene view / Camera，确认 Floor 与 Grid overlay 没有闪烁或 Z-fighting。 | Passed | 观察步骤；截图/录屏名。 |
| M18 | 确认 Floor 使用 Palette B 的浅黄方向（参考 `#F8E9A8`，最终以 Lighting 下可读性为准）。 | Passed | Material 路径；Camera 截图。 |
| M19 | 确认只可见 Back-left 与 Back-right 两面固定 Wall。 | Passed | Scene 截图。 |
| M20 | 确认两面 Wall 使用 Palette B 的分层色彩，而非意外同色/默认材质。 | Passed | 两个 Material 路径；截图。 |
| M21 | 确认两面 Wall 的交角干净，没有明显 gap、重叠或穿插。 | Passed | Corner 近景截图。 |
| M22 | 确认靠近 Camera 的前方边界保持开放，没有额外可见侧墙遮挡。 | Passed | Camera 截图。 |
| M23 | 选中 Entrance，确认稳定 `Entrance Id` 和开放边界位置正确。 | Passed | Inspector 实际值；Scene 截图。 |
| M24 | 确认 Entrance 内侧的 blue gizmo/overlay 是准确 `2 × 2` Clearance Zone。 | Passed | 选中 Entrance 截图。 |
| M25 | 用现有 fixture/可恢复测试确认家具尝试占用 Entrance 时得到明确 `ReservedEntranceClearance`（或等价明确原因）。 | Passed | 重现步骤；完整 rejection reason。 |
| M26 | 确认 Clearance Zone 仍表现为可行走区域，而不是把角色通行也封死。 | Passed | 观察方法与结果。 |
| M27 | 确认 Entrance Collider 没有不合理覆盖 Clearance，亦未遮挡预期区域。 | Passed | Collider Inspector/Scene 截图。 |
| M28 | 确认 Floor 不会阻挡本阶段所需的 furniture selection、placement raycast 或 navigation smoke 行为。 | Passed | 操作与结果。 |
| M29 | 确认 Validation Scene 没有意外多出的可见 Wall、Floor 或 Entrance fixture。 | Passed | Hierarchy/Scene 截图。 |
| M30 | 确认 Scene 中的 environment grouping 清楚，能区分 Floor、Walls、Entrance 与 furniture fixtures。 | Passed | Hierarchy 截图。 |
| M31 | 运行 Build Validation Scene 后重新打开 Scene，确认上述 environment visual contract 仍可复现。 | Passed | builder 后截图；Console 输出。 |

### D. Counter（M32–M40）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M32 | 从常用 Camera 检查 Work Table 约 `0.90 × 0.65 × 0.90m` 的可读比例。 | Passed | Camera 截图；观察。 |
| M33 | 检查 Counter module 约 `1.00 × 0.72 × 1.00m`，其 controlled non-uniform derivative 已在 Blender apply 后导出，Unity Prefab root scale 为 `(1,1,1)`。 | Passed | Prefab Transform 截图；实际尺寸。 |
| M34 | 检查两个相邻 Counter modules 的 seam/gap/intersection 看起来合理。 | Passed | 近景截图。 |
| M35 | 检查三个相邻 Counter modules 的 seam/gap/intersection 看起来合理。 | Passed | 近景截图。 |
| M36 | 确认相邻 modular Counters 保持独立 instances，没有自动 merge 成一个物体。 | Passed | Hierarchy 截图。 |
| M37 | 逐个确认每个 `1 × 1` Counter module 有一个可读的 cyan `SurfaceSlotMarker`。 | Passed | 选中模块截图；`Slot Id`。 |
| M38 | 确认 `PF_Validation_Counter_1x3_01.prefab` 在 fixture 中是一个长 Counter instance，而不是三个独立实例。 | Passed | Hierarchy 截图。 |
| M39 | 确认长 Counter 有三个独立 local Slots；旋转后 Slots 随 local transform 一同旋转。 | Passed | 0°/90° 截图；Slot IDs。 |
| M40 | 检查 Counter Collider 不明显超出 visual bounds，也不遮挡可用 Surface Slot 平面。 | Passed | Collider/Slot 近景；Inspector。 |

### E. Coffee Machine（M41–M46）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M41 | 确认 Coffee Machine 约 `0.65 × 0.62 × 0.50m`，可合理放入一个 Counter Surface Slot。 | Passed | 组合截图；实际观察。 |
| M42 | 确认 Unity local `+Z` 是 Coffee Machine 的 employee interaction side。 | Passed | 选中 ForwardMarker 截图。 |
| M43 | 检查 0° 时 Forward 与 employee side 一致。 | Passed | 0° 截图；方向。 |
| M44 | 检查 90° 时 Forward 与 employee side 一致。 | Passed | 90° 截图；方向。 |
| M45 | 检查 180° 时 Forward 与 employee side 一致。 | Passed | 180° 截图；方向。 |
| M46 | 检查 270° 时 Forward 与 employee side 一致，并确认组合高度、Collider 和 Camera 下可读性。 | Passed | 270° 截图；Collider/Camera 结果。 |

### F. Cash Register（M47–M58）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M47 | 确认场景使用新 vintage Cash Register，而非旧 `pos terminal` 候选。 | Passed | Prefab/model 路径；截图。 |
| M48 | 确认 Cash Register 约 `0.43 × 0.45 × 0.26m`，root position 为 zero、rotation identity、scale one，pivot 在 bottom-center。 | Passed | Prefab Transform/尺寸截图。 |
| M49 | 确认 Cash Register 在 Counter Slot 上没有明显穿插或悬空，且适合一个主要 slot。 | Passed | 组合近景截图。 |
| M50 | 确认刚好一个 green `Employee` side marker，并能解释它代表员工操作侧。 | Passed | Marker Inspector；gizmo 截图。 |
| M51 | 确认刚好一个 yellow `Customer` side marker，并能解释它代表顾客交互侧。 | Passed | Marker Inspector；gizmo 截图。 |
| M52 | 确认 Employee Side 与 Customer Side 是相反 cardinal directions，而不是同向或 90°。 | Passed | 两个 `Local Direction` 实际值。 |
| M53 | 检查 0° 时两侧仍相反。 | Passed | 0° 截图；方向。 |
| M54 | 检查 90° 时两侧一起旋转且仍相反。 | Passed | 90° 截图；方向。 |
| M55 | 检查 180° 时两侧一起旋转且仍相反。 | Passed | 180° 截图；方向。 |
| M56 | 检查 270° 时两侧一起旋转且仍相反。 | Passed | 270° 截图；方向。 |
| M57 | 从 Customer Side 观察未来 Queue 的初始 outward direction 指向店外/远离收银机；P4 不实现 NPC Queue。 | Passed | Scene/gizmo 截图；判断说明。 |
| M58 | 在常用 Camera 检查 `A2 + P1` 可读性、Texture 质量、LOD 和 Collider；Collider 不应挡住 side markers。 | Passed | Camera/Inspector 截图；观察结果。 |

### G. Wall and Window（M59–M72）

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M59 | 选中 Back-left Wall，确认 magenta gizmo 为 `8 columns × 2 rows`。 | Passed | Inspector/Scene 截图。 |
| M60 | 选中 Back-right Wall，确认 magenta gizmo 为 `8 columns × 2 rows`。 | Passed | Inspector/Scene 截图。 |
| M61 | 确认 Wall lower row 约覆盖 world height `0.5–1.5m`、upper row 约 `1.5–2.5m`，Wall 高约 `3m`。 | Passed | Scene 测量/观察。 |
| M62 | 确认 `Slot Size` 为 `1m × 1m`，不是按 Floor Footprint 生成。 | Passed | Inspector 实际值。 |
| M63 | 确认默认 `1 × 1` Window 位于 Back-right Wall lower-row 中央附近。 | Passed | Scene 截图；Wall/slot 位置。 |
| M64 | 确认 Window 贴合所属 Wall Slot，不越界、不跨 Wall corner。 | Passed | 近景截图。 |
| M65 | 移动视角确认 Window 与 Wall 没有明显 Z-fighting。 | Passed | 观察步骤；截图/录屏名。 |
| M66 | 检查 `1 × 2` Wall fixture 的合法/非法结果与 spec/validator 给出的原因一致。 | Passed | Fixture 路径；结果；完整 reason。 |
| M67 | 检查 `2 × 1` Wall fixture 的合法/非法结果与 spec/validator 给出的原因一致。 | Passed | Fixture 路径；结果；完整 reason。 |
| M68 | 检查 overlap fixture 被拒绝，且没有改变已有合法 Wall placement。 | Passed | 重现步骤；reason；前后截图。 |
| M69 | 检查 out-of-bounds 或 cross-corner fixture 被拒绝，且原因明确。 | Passed | Fixture；完整 reason。 |
| M70 | 确认 P4 Wall-mounted item 没有自由 rotation；Window 不出现允许旋转的 authoring control。 | Passed | Inspector/fixture 截图。 |
| M71 | 确认 Floor occupancy 与 Wall occupancy 的 gizmos/数据是独立的，Window 不占 Floor furniture cells。 | Passed | 同时选中/对比截图。 |
| M72 | 确认 Wall Slot、Window 与相关 gizmo 在正常 Camera 下可读。 | Passed | Camera 截图。 |

### H. Presentation and regression（M73–M88）

#### MainCafe regression 的准确操作

`M76–M78 必须在 MainCafe 完成`，不要在独立的 P4 Validation Scene 寻找
Pause / `1x` / `2x` 按钮：

1. 保存当前 Scene，打开 `Assets/Scenes/MainCafe.unity` 并进入 Play Mode。
2. 依次检查 Pause、`1x`、`2x`；每一步都查看 Console 是否出现新的 P4 error。
3. 在 Hierarchy 展开 `TEMP_P4_ManualReviewFixtures_DELETE_LATER`；使用已配置好的
   `ReviewCube_Moving` 和 `ReviewCube_Static`，不要再创建额外 Cube。
4. 用 `ReviewCube_Moving` 检查 Pause、`1x`、`2x` 的移动速度；用两个 cubes 作为
   Camera drag/scroll 的视觉参照。
5. 分别点击两个 cubes，再点击空白处，检查 select/deselect；两者已经包含
   `BoxCollider`、`ColorSelectable` 和各自的 URP Lit material。
6. 退出 Play Mode；确认没有新增 runtime-only object，也没有把测试期间的 Transform
   或 Component 修改写回 Scene。两个 committed review cubes 会继续存在，这是预期结果。
7. 确认 `MainCafe.unity` 没有未保存修改，再返回 Phase4CoreArchitecture Validation Scene。

这两个 committed review cubes 会保留到 MainCafe 有正式视觉参照物时再删除。届时必须同时删除
Scene root、两个 `M_TEMP_ManualReviewCube_*` materials、setup utility、无剩余引用的 mover，
并同步更新对应 PlayMode regression tests。

| ID | 亲自执行/观察的检查 | Status | Evidence |
|---|---|---|---|
| M73 | 从默认 Camera 检查 Palette B 的 Floor、两面 Wall、Furniture 视觉层级清楚。 | Passed | 默认 Camera 截图。 |
| M74 | 同时看 Work Table、Counter、Coffee Machine、Cash Register、Cup、Window 时，确认各自用途可辨认。 | Passed | 多资产截图；观察。 |
| M75 | 在常用 Camera framing 下检查小资产没有被遮挡或小到不可读。 | Passed | Camera 截图；观察。 |
| M76 | 在 `MainCafe` Play Mode 中按 **Pause**，确认场景没有异常 error 或失控变化。 | Passed | Pause 截图；Console 状态。 |
| M77 | 在 `MainCafe` Play Mode 中选择 **1x**，确认场景稳定、无 P4 error。 | Passed | 1x 截图；Console 状态。 |
| M78 | 在 `MainCafe` Play Mode 中选择 **2x**，确认场景稳定、无 P4 error。 | Passed | 2x 截图；Console 状态。 |
| M79 | 在一个常用宽屏分辨率/Aspect 下检查无明显裁切、重叠或可读性失败。 | Passed | 分辨率/Aspect；截图。 |
| M80 | 在第二个不同 Aspect/分辨率下重复 M79。 | Passed | 分辨率/Aspect；截图。 |
| M81 | 退出 Play Mode 前后查看 Console，确认没有新增未解释的 P4 error。 | Passed | Console 截图；error count。 |
| M82 | 打开 `Assets/Scenes/MainCafe.unity`，确认旧场景仍能启动；然后返回 Validation Scene。 | Passed | MainCafe Play Mode 截图；操作记录。 |
| M83 | 在 `MainCafe` Play Mode 使用 `ReviewCube_Moving` 和 `ReviewCube_Static` 作为视觉参照，检查 Camera pan/zoom 仍可工作。 | Passed | drag/scroll 操作、Camera Transform/Size 与结果。 |
| M84 | 分别点击两个已配置 `ColorSelectable` 的 review cubes，再点击空白处，确认 select/deselect 没有被 P4 干扰。 | Passed | Cube 选中颜色变化及取消选择结果。 |
| M85 | 退出 MainCafe Play Mode，确认没有额外 runtime-only object 或测试修改写回 Scene；两个 committed review cubes 仍存在。 | Passed | 退出后 Hierarchy、Scene dirty 状态与无未保存修改证据。 |
| M86 | 退出 Play Mode 后重跑 P3 benchmark validator，记录 `3 / 3 valid, 0 issues` 或完整实际 issue。 | Passed | Console 输出/报告路径。 |
| M87 | 退出 Play Mode 后重跑 P4 production validator，记录 `5 / 5 valid, 0 issues` 或完整实际 issue。 | Passed | Console 输出/报告路径。 |
| M88 | 汇总实际 M01–M87、Cash Register rights、自动化证据、已知限制；只有全部 required gates 已接受时，才提交是否更新 Roadmap 的 Studio Owner 决定。 | Passed | 2026-08-08；Studio Owner 明确要求其他项目完成后将 M88 标为完成；Roadmap 更新为 Phase 4 `Completed`。 |

## 常见问题排查

| 现象 | 先检查什么 | 该怎么记录/处理 |
|---|---|---|
| 找不到 Phase 4 菜单 | 等待 Unity import 结束；确认在 phase-4 worktree 打开；看 Console 是否有 compile error。 | 记录 Unity version、路径和完整 Console error；不要通过复制脚本或切到 main 绕过。 |
| `Validate Production Content` 显示 issue | 复制每个 `[IssueCode] AssetPath: Message`。 | 填为 `Failed` 或 `Blocked`；issue 可能是 Definition、marker、Collider、Material、Texture、root transform 或 Wall/Entrance contract。 |
| 画面闪烁 | 在 Scene/Camera 移动时看 Floor/Grid 或 Window/Wall 是否重叠。 | 录屏/截图，并记录发生角度；不要把视觉问题标为 Passed。 |
| 看不到 gizmo | 选中目标 GameObject，开启 Scene view 的 **Gizmos**；在 Play Mode 需要重新选中。 | 仍看不到时记为 `Blocked`，附 Hierarchy 和 Inspector 截图。 |
| Slot 被 Collider 挡住 | 选中 Counter，检查 Collider 是否高于 Surface Slot plane。 | 记录 Collider component 和 `Slot Id`；这是 production validator 的明确 contract。 |
| Cash Register 两侧看起来不对 | 分别选择 `Employee` / `Customer` marker，检查 `Side Type` 和 `Local Direction`。 | 不要只凭模型屏幕朝向猜；记录四个 rotation 的方向。 |
| Window 或 Wall fixture 不知道是否该通过 | 先查看该 fixture 的目标和 validator reason。 | 合法性不明时记 `Blocked`，不要写 `Passed` 或猜测 N/A。 |
| Unity licensing / 无 XML | 这不是通过，也不是失败的测试结果。 | 记录 licensing 状态和缺失的 XML；完成授权后重跑。 |

## Phase 4 的真正完成条件

以下条件全部满足、由 Studio Owner 明确接受后，才可以讨论更新 Roadmap：

1. 新鲜自动化 evidence 可用，且 P4/P3 validators 无未解释 issue。
2. `M01–M88` 每条都有 `Passed`、`Failed`、`Blocked` 或有理由的 `Not Applicable`，没有把未执行项目伪装成 Passed。
3. 所有 required manual 项无未解释 `Failed` / `Blocked`。
4. Cash Register development + future commercial-release rights 都明确为 `Yes` 并有证据。
5. Studio Owner 明确接受 Phase 4。

上述条件现已全部满足，Phase 4 已完成。Phase 5 仍需单独讨论、批准后才开始。
