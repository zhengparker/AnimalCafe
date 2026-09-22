# Phase 8 — Functional Furniture & Layout Readiness Test Cases

> 状态：Approved Phase 8 Baseline；Task 10 / Phase 8 `In Progress`；2026-09-09 UX2 见第 9 节，automated PASS / Ready for manual review，新增 manual Pending
>
> 日期：2026-09-02
> 对应 Design：`Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-design.md`

## 1. 测试分层

Phase 8 的执行证据分三类：

1. `Automated EditMode Tests`：验证纯 C# domain、transaction 和 validator。
2. `Automated PlayMode / Scene Integration Tests`：验证 Preview、UI、Scene wiring 和 input ownership。
3. `Studio Owner Manual Tests`：由 Studio Owner 在 Unity Play Mode 亲自判断操作、反馈和视觉可读性。

状态门槛则分为五项：`Automated`、`Engineering`、`QA`、`Production` 和 `Studio Owner Manual`。Automated PASS 不能替代任何独立 department decision 或 Manual PASS。Task 阶段运行 focused tests；完整 regression、department reviews 和 Manual Tests 在 Phase 收尾分别记录。

## 2. Automated EditMode — Normal Cases

### P8-E-N-001 Surface Slot address value equality

- Given：相同 support furniture instance ID 与相同 Surface Slot ID。
- When：创建两个 `SurfaceSlotAddress`。
- Expected：两者相等并产生一致 hash；任一 ID 不同时不相等。

### P8-E-N-002 Place Cash Register on compatible Slot

- Given：一个存在、兼容且空闲的 Counter Slot。
- When：Confirm Cash Register placement。
- Expected：创建一个稳定 mounted instance；Slot occupancy 指向该 instance。

### P8-E-N-003 Place Coffee Machine on compatible Slot

- Given：一个存在、兼容且空闲的 Counter Slot。
- When：Confirm Coffee Machine placement。
- Expected：创建一个稳定 mounted instance；不占用 Floor furniture footprint。

### P8-E-N-004 Create multiple functional instances

- Given：多个兼容空闲 Slots。
- When：分别创建两个 Cash Registers、两个 Coffee Machines 和两个 Pick-up Points。
- Expected：每个实例 ID 唯一；每个 binding 和 occupancy 独立。

### P8-E-N-005 Move mounted equipment

- Given：已确认的 mounted equipment 和另一个空闲兼容 Slot。
- When：Move 并 Confirm。
- Expected：旧 Slot 释放，新 Slot 占用，instance ID 保持不变。

### P8-E-N-006 Rotate mounted equipment

- Given：已确认的 Cash Register 或 Coffee Machine。
- When：依次旋转到 0°、90°、180°、270° 并 Confirm。
- Expected：binding 不变，rotation 正确保存。

### P8-E-N-007 Create and move Pick-up Point

- Given：两个兼容空闲 Slots。
- When：创建 Pick-up Point，之后 Move 到第二个 Slot 并 Confirm。
- Expected：ID 保持不变，旧 Slot 释放，新 Slot 占用。

### P8-E-N-008 Support furniture move preserves bindings

- Given：Counter 上有 mounted equipment 和 Pick-up Point。
- When：移动 Counter。
- Expected：所有 `SurfaceSlotAddress` 保持不变；派生 world position 与 anchors 跟随更新。

### P8-E-N-009 Support furniture rotation preserves bindings

- Given：Counter 上有 mounted equipment 和 Pick-up Point。
- When：Counter 旋转 90°。
- Expected：bindings 保持；Slot world relations 和 anchors 随支撑家具旋转。

### P8-E-N-010 Cash Register anchors rotate automatically

- Given：Cash Register authored opposite Employee / Customer sides。
- When：解析四个 equipment rotations。
- Expected：每个 rotation 的 Employee / Customer anchors 正确且始终相反；玩家不提供 anchor input。

### P8-E-N-011 Coffee Machine anchor rotates automatically

- Given：Coffee Machine authored `+Z` Forward。
- When：解析四个 equipment rotations。
- Expected：Employee anchor 随 rotation 正确变化；不存在 Customer anchor。

### P8-E-N-012 Pick-up anchors are deterministic

- Given：Pick-up Point 周围有多个有效相邻 cells。
- When：重复解析相同 layout。
- Expected：优先选择 opposite pair，再选择两个不同 cells，最后才允许共用唯一有效 cell；相同条件使用 North → East → South → West；重复解析结果一致，玩家不提供 rotation。

### P8-E-N-013 Grid search finds route around obstacle

- Given：起点和目标之间有障碍，但存在另一条可走路线。
- When：执行 reachability evaluation。
- Expected：返回 reachable，不要求生成 NPC path following。

### P8-E-N-014 One complete service combination opens business

- Given：至少一个 valid Cash Register、Coffee Machine 和 Pick-up Point 位于同一所需可达区域。
- When：生成 report。
- Expected：`CanOpenForBusiness == true`。

### P8-E-N-015 Extra invalid station becomes warning

- Given：一组完整有效服务点以及一个 blocked Cash Register。
- When：生成 report。
- Expected：整体仍可营业；blocked Register unavailable；report 包含该实例 warning。

### P8-E-N-016 Report returns all station results

- Given：多个 valid 和 invalid functional points。
- When：生成 report。
- Expected：不在第一个 failure 停止；counts 和每个 instance result 完整、稳定排序。

## 3. Automated EditMode — Invalid / Boundary Cases

### P8-E-I-001 Reject Floor placement for mounted equipment

- Given：Cash Register 或 Coffee Machine definition。
- When：尝试使用 Floor placement。
- Expected：返回 unsupported placement surface；不修改 Floor 或 Surface occupancy。

### P8-E-I-002 Reject incompatible support

- Given：没有兼容 Surface Slot 的家具。
- When：尝试放置设备或 Pick-up Point。
- Expected：Preview invalid；Confirm 被拒绝；返回具体 failure。

### P8-E-I-003 Reject duplicate Slot occupancy

- Given：Slot 已被 Cash Register 占用。
- When：尝试放 Coffee Machine 或 Pick-up Point 到同一 Slot。
- Expected：拒绝且原 occupancy 不变。

### P8-E-I-004 Reject second Pick-up Point on same Slot

- Given：Slot 已有 Pick-up Point。
- When：尝试 Confirm 第二个 Pick-up Point。
- Expected：拒绝；第一个实例保持不变。

### P8-E-I-005 Missing support furniture

- Given：binding 指向不存在的 support furniture ID。
- When：生成 anchors / readiness。
- Expected：不 crash；返回 `MissingSupportFurniture` blocking failure。

### P8-E-I-006 Missing Surface Slot

- Given：support 存在，但 binding 的 Slot ID 不存在。
- When：生成 readiness。
- Expected：返回 `MissingSurfaceSlot`，包含 support 和 Slot IDs。

### P8-E-I-007 Anchor outside unlocked layout

- Given：rotation 后 anchor 落到 unlocked region 外。
- When：验证 station。
- Expected：station invalid，并指出 anchor position。

### P8-E-I-008 Anchor blocked by furniture

- Given：员工或顾客 anchor cell 被 Floor furniture 占用。
- When：验证 station。
- Expected：station invalid，failure 指明角色侧、设备 ID 和 cell。

### P8-E-I-009 Anchor blocked by wall or locked cell

- Given：anchor 位于 wall constraint 或 locked cell。
- When：验证 station。
- Expected：station invalid，failure reason 具体。

### P8-E-I-010 Pick-up Point has no valid adjacent anchor

- Given：Pick-up Point 周围没有有效相邻 cell。
- When：Preview 或解析 anchors。
- Expected：Preview invalid；Confirm 被拒绝。

### P8-E-I-011 Missing required capability

- Given：分别缺少所有 Cash Registers、所有 Coffee Machines 或所有 Pick-up Points。
- When：生成 report。
- Expected：每种情况 `CanOpenForBusiness == false`，并返回对应 missing capability failure。

### P8-E-I-012 All instances of one capability invalid

- Given：存在多个 Cash Registers，但全部 invalid；其他功能有效。
- When：生成 report。
- Expected：不能营业；同时保留所有 Register failure details。

### P8-E-I-013 Valid stations in disconnected regions

- Given：三种功能分别单独 valid，但不在同一 required reachable region。
- When：生成 report。
- Expected：不能营业；返回 no complete reachable service combination。

### P8-E-B-001 Minimum one-Slot support

- Given：只有一个 `1 × 1 Surface Slot` 的 Counter Module。
- When：放置一个功能点。
- Expected：成功；第二个功能点被 occupancy 拒绝。

### P8-E-B-002 Multiple Slots on rotated long Counter

- Given：三个独立 Slots 的 long Counter。
- When：Counter 分别处于四个 rotations。
- Expected：所有 Slot IDs 保持稳定，world positions 和 bindings 正确。

### P8-E-B-003 Large but legal reachable region

- Given：项目允许的最大合法 Grid region 和多个 obstacles。
- When：执行 reachability。
- Expected：确定性完成，不递归溢出，不修改 layout。

## 4. Automated EditMode — Transaction / Recovery Cases

### P8-E-R-001 Cancel new mounted equipment

- Given：新 Cash Register / Coffee Machine Preview。
- When：Cancel。
- Expected：不创建正式 instance，不占用 Slot。

### P8-E-R-002 Cancel moved mounted equipment

- Given：已确认设备开始 Move Preview。
- When：移动后 Cancel。
- Expected：设备返回原 Slot 和 rotation，occupancy 完整恢复。

### P8-E-R-003 Cancel new Pick-up Point

- Given：点击 Pick-up Point button 后的未确认 Preview。
- When：Cancel。
- Expected：不创建 instance，不占用 Slot。

### P8-E-R-004 Cancel moved Pick-up Point

- Given：已确认 Pick-up Point 开始 Move。
- When：移动后 Cancel。
- Expected：返回原 Slot，ID 不变。

### P8-E-R-005 Store mounted equipment

- Given：已确认设备。
- When：完成 Store confirmation。
- Expected：设备删除，Slot 释放，其他 bindings 不变。

### P8-E-R-006 Store Pick-up Point

- Given：已确认 Pick-up Point。
- When：完成 Store confirmation。
- Expected：Point 删除，Slot 释放。

### P8-E-R-007 Block Store for occupied support furniture

- Given：Counter 上存在一个或多个 mounted equipment / Pick-up Points。
- When：尝试 Store Counter。
- Expected：操作被拒绝；反馈列出需要先移除的内容；所有数据不变。

### P8-E-R-008 Failed Confirm is atomic

- Given：Preview 在确认瞬间因 occupancy 或 binding 变化而失效。
- When：Confirm。
- Expected：正式 layout 无部分写入；旧状态完整保留。

### P8-E-R-009 Layout change invalidates old readiness

- Given：已经生成 report。
- When：移动或旋转支撑家具、改变功能点或相关 occupancy。
- Expected：旧结果不再被当作 current；下一次读取重新计算。

### P8-E-R-010 Corrupt binding does not crash

- Given：测试 fixture 注入 missing support、missing Slot 或重复 binding。
- When：生成 report / rebuild view。
- Expected：返回稳定 failure；不抛出未处理 exception；不自动猜测修复。

## 5. Automated PlayMode / Scene Integration

### P8-P-N-001 Catalogue rows render consistently

- Furniture Tab 显示 Furniture、Cash Register、Coffee Machine 三行。
- 三行使用一致 card layout、spacing、scroll 和 interaction。
- Pick-up Point button 存在且不作为第四条 Catalogue row。

### P8-P-N-002 Cash Register Surface Preview

- 选择 Cash Register 后出现 Preview。
- 兼容空闲 Slot 显示 valid；Floor 和占用 Slot invalid。
- Move、Rotate、Confirm、Cancel 与现有 furniture interaction 一致。

### P8-P-N-003 Coffee Machine Surface Preview

- 行为与 Cash Register 一致，但使用 Coffee Machine anchor contract。

### P8-P-N-004 Multiple mounted equipment

- 在多个 Slots Confirm 多个 CR / CM 后，Scene representations 数量、IDs 和 domain 一致。

### P8-P-N-005 Pick-up Point button creates successive indicators

- 第一次点击创建第一个 Preview 并 Confirm。
- 再次点击创建第二个独立 Preview 并 Confirm。
- 两个 indicators 和 domain instances 独立。

### P8-P-N-006 Pick-up indicator lifecycle

- Decoration Mode 中 indicators 可见。
- 退出 Decoration Mode 后全部隐藏。
- 再次进入后按 confirmed layout 恢复。

### P8-P-N-007 Pick-up indicator actions

- Move、Confirm、Cancel、Store 与 design 一致。
- 不显示 Rotate action。
- invalid footprint 时 Confirm 不可用。

### P8-P-N-008 Support furniture follows and Store blocking

- 移动 / 旋转 Counter 后 mounted views 和 indicators 跟随。
- 有内容时 Store Counter 被阻止，Scene 和 domain 不变化。

### P8-P-N-009 Readiness feedback matches report

- UI 显示整体 ready / not ready、blocking reasons 和 warnings。
- 文本中的设备 / Slot 与 domain report 一致。

### P8-P-N-010 Validation anchor gizmos match domain

- Phase 8 validation Scene 显示 developer-facing Employee / Customer anchor gizmos 或等价 debug feedback。
- 四个 rotations、support move / rotate 和 Pick-up automatic selection 后，显示的角色侧与 Grid cells 必须和 resolver result 一致。
- 正常经营画面不显示这些 debug visuals。

### P8-P-I-001 UI input does not leak into Scene

- 点击 Catalogue rows、cards、Pick-up button 和 action buttons 时，不触发背后的 Scene placement / drag。

### P8-P-R-001 Pointer cancel restores preview

- drag 中收到 canceled input、关闭相关 UI 或退出 editing flow 时，Preview 安全取消并恢复 domain / views。

### P8-P-R-002 Idempotent Scene setup

- Phase 8 setup 连续执行两次。
- MainCafe / validation Scene 中没有 duplicate controller、registry、view、Canvas 或 runtime roots。

### P8-P-R-003 Existing Decoration direct regression

- Floor furniture placement / move / rotate / store 继续工作。
- Wall-mounted decoration 继续工作。
- Floor / Wall surface preview、Confirm 和 Cancel 继续工作。
- Camera、pause 和 input ownership 无退化。

## 6. Studio Owner Manual Tests

> 每项由 Studio Owner 在 Unity Play Mode 执行。结果记录为 `PASS / FAIL`；失败时记录步骤、Scene 状态、Console message 和截图或短视频。

### 6.1 真实执行环境与入口

- Project / worktree：`E:\Unity\Project\AnimalCafe\.worktrees\phase-8-functional-furniture`
- Unity：`6000.5.5f1`
- 正常 Scene：`Assets/Scenes/MainCafe.unity`
- Debug Scene：`Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`
- 正常入口：Play Mode → Game view 右侧 `Decoration` → `Furniture`
- readiness feedback：Game view 顶部中央的 `Phase8_ValidationMessage`
- readiness 只由已经 Confirm / Store 的正式结果发布；移动中的 Preview 与 Cancel 不会改写上一条正式 readiness。

不要运行 `Build Assets`，也不要运行任何 `Configure ...` menu；这些命令会修改 assets 或 Scenes，不属于 Studio Owner manual review。

### P8-M-001 Catalogue 三行可读性

准备：进入 `MainCafe` Play Mode 和 Decoration Mode，打开 Furniture Tab。

步骤：

1. 查看 Furniture、Cash Register、Coffee Machine 三行。
2. 用鼠标按住拖动横向 / 纵向浏览当前 Catalogue；分别把鼠标放在物品行与 Catalogue 外层，转动滚轮。
3. 点击每行中的 card，再返回 Catalogue。

预期：三行 layout 与现有 Furniture row 一致；标题、cards、spacing 清楚，没有重叠、截断或难以点击。滚轮不移动 Catalogue 内任何内容，镜头仍可缩放；鼠标 / touch 拖动仍可浏览 Catalogue。

### P8-M-002 Cash Register 正常放置

步骤：

1. 从 Cash Register row 选择一个 card。
2. 把 Preview 移到兼容空闲 Counter Slot，观察未确认 ghost 悬浮于桌面，footprint 留在桌面。
3. Rotate 后点击 `✓`。
4. 再选择该设备，执行 Move、Cancel、Move、Confirm。

预期：操作感觉与普通 furniture 一致；未确认 ghost 使用普通 furniture 的悬浮高度，Rotate 不改变高度；只有 compatible free Surface Slot 可 Confirm，确认后设备回到桌面；Cancel 返回原位；无需设置 anchors；Console clean。

### P8-M-003 Coffee Machine 正常放置

重复 P8-M-002 的主要流程。

预期：Coffee Machine 只能使用桌面 Slot，Preview / actions 与 furniture 一致，不出现 Customer anchor 设置。

### P8-M-004 Floor 和占用 Slot invalid feedback

步骤：

1. 尝试把 CR / CM Preview 移到 Floor，观察 ghost 相对地面悬浮、地面 footprint 红色；松手后重新抓 ghost 拖回桌面。
2. 尝试放到已占用 Slot。
3. 尝试点击 `✓`。

预期：在 Floor 上 ghost 仍可见且可重新抓起，footprint 留在地面并显示红色；invalid feedback 清楚，Confirm 不可用。回到兼容空闲 Slot 才恢复 Confirm；未确认或 Cancel 不改变原有设备和 occupancy。

### P8-M-005 创建多个设备

步骤：在不同 Slots 放置至少两个 Cash Registers 和两个 Coffee Machines。

预期：每个设备可独立选择、移动、旋转和 Store；操作一个不会错误影响另一个。

### P8-M-006 Pick-up Point indicator 视觉接受

步骤：点击 Pick-up Point button，把 indicator 依次移到空闲桌面、occupied Surface Slot 和地面。地面上松手，重新抓起模型拖回空闲桌面；对已确认 point 再执行 Move → 地面 → Cancel。

预期：细长灰白倒四方棱锥有清楚的立体面，与 `1 × 1` footprint 分离；只有 footprint 随有效性变绿 / 红。空闲桌面 valid；occupied 或地面 invalid 且不可 Confirm。地面保留悬浮模型，可重新抓起拖回桌面，Cancel 恢复原位置。最终视觉判断由 Studio Owner 决定。

执行限制（2026-09-08）：当前 `SurfaceSlotDefinition` 没有类型兼容字段，现有 MainCafe Counter Slots 无法构造“类型不兼容 Slot”分支。该要求暂不删除，也不擅自改为 N/A；incompatible 分支记“未覆盖”，closeout 前另行确认。Studio Owner 已批准地面悬浮模型 + 红 footprint 的修复，拖离 Slot 后隐藏不再是接受标准；模型本身不变红。

### P8-M-007 Pick-up Point actions

步骤：

1. 创建并 Confirm 一个 Pick-up Point。
2. Move 后 Cancel，确认回到原位。
3. Move 后 Confirm。
4. Store 并确认移除。

预期：只有 Store、`✓`、`×` 等适用 actions，不出现 Rotate；每一步与现有 furniture interaction 一致。

### P8-M-008 多个 Pick-up Points

步骤：连续使用 Pick-up Point button 创建至少两个 points。

预期：第二次点击创建新实例，不移动或覆盖第一个；不能让两个 points 或 Point 与 CR / CM 重叠。

### P8-M-009 Indicator 只在 Decoration Mode 可见

步骤：创建 Pick-up Points，退出 Decoration Mode，再重新进入。

预期：退出后 arrows / footprints 不可见；重新进入后全部 confirmed indicators 在正确 Slots 恢复。

### P8-M-010 支撑家具移动和旋转

步骤：移动并旋转一个带有 CR / CM / Pick-up Point 的 Counter。

预期：所有桌面内容与 Counter 一起移动和旋转；没有掉落、漂移、重复或留在旧位置；readiness 重新计算。

### P8-M-011 禁止 Store 有内容的 Counter

步骤：选择有 mounted equipment 或 Pick-up Point 的 Counter，尝试 Store。

预期：Store 被阻止；提示清楚列出需要先移除的内容；Counter 和桌面内容全部保留。

### P8-M-012 Readiness — 完整有效组合

准备：在 `MainCafe` 中从空布局或已知干净布局开始。

步骤：

1. 在 compatible free Surface Slots 上各放置并 Confirm 一个 Cash Register、Coffee Machine 和 Pick-up Point。
2. 保持这些设备四周的自动 anchors 没有被 Floor furniture、墙或 locked cell 挡住，并让所需 customer / employee 区域连通。
3. Confirm 最后一项操作，观察 Game view 顶部中央的 `Phase8_ValidationMessage`。

预期：显示精确文本 `布局已准备好，可以营业`。

### P8-M-013 Readiness — 缺少必要功能

准备：从 P8-M-012 的有效组合开始；每次只测试一种缺失类型。

步骤：

1. Store 当前布局中的每一个 Cash Register，Confirm 后应显示 `暂时不能营业：还需要至少一个收银机`；随后重新放回并 Confirm 至少一个有效 Cash Register，恢复 P8-M-012 的 ready 状态。
2. Store 当前布局中的每一台 Coffee Machine，Confirm 后应显示 `暂时不能营业：还需要至少一台咖啡机`；随后重新放回并 Confirm 至少一台有效 Coffee Machine，再恢复 ready。
3. Store 当前布局中的每一个 Pick-up Point，Confirm 后应显示 `暂时不能营业：还需要至少一个取餐点`；随后重新放回并 Confirm 至少一个有效 Pick-up Point。

预期：一次只缺一种必要功能；三种情况各自出现对应的 type-specific blocking message，恢复该类型后再进入下一种测试。

### P8-M-014 Readiness — 多点局部失败

准备：保留 P8-M-012 的一组有效 CR / CM / Pick-up，不移动或阻挡它们的 anchors。

步骤：

1. 在另一张 Counter 的 compatible free Slot 上新增并 Confirm 一个额外 CR 或 CM。
2. 在只会挡住这个额外设备某一个 anchor、不会挡住原有效组合 anchors 的位置，放置并 Confirm 一件 Floor furniture。
3. Confirm 后观察 `Phase8_ValidationMessage`；核对以 `可以营业，但` 开头的 warning 中的设备类型、中文角色、站位坐标与原因。

预期：仍可营业；文本以 `可以营业，但` 开头，给出例如 `互动位置被阻挡` 的具体原因，以中文角色和站位坐标区分受影响位置。不显示长 GUID、`slot.0` 或英文 Employee / Customer；原有效三件组合仍可用。按 2026-09-08 approved clarification，stable IDs 只保留内部诊断，不再要求玩家可见。此修改不代替本项实际 manual review。

### P8-M-015 Readiness — 没有完整连通组合

准备：先保留 P8-M-012 的有效组合，确保三种必要功能都存在且每个设备自身仍 valid。

步骤：

1. 用已 Confirm 的 Counter / Floor furniture 横向排成一道分隔带；把 CR 与 Pick-up 的 Customer anchors 朝向 Entrance 一侧，把 CR / CM / Pick-up 的 Employee anchors 留在分隔带另一侧。不要让家具直接占住任何 anchor cell。
2. 在 employee 一侧，再从分隔带向后墙放一列已 Confirm 的 Floor furniture / Counter，放在 CM 或 CR 的 employee anchor 与 Pick-up employee anchor 之间，把 employee 侧分成左右两个仍各自可走的小区域。
3. Confirm 最后一件分隔家具；确认每个设备没有单独出现 `互动位置被阻挡` 或 `互动位置无法到达`。
4. 观察 `Phase8_ValidationMessage`。

预期：三种功能仍分别 valid，但没有一组 CR / CM / Pick-up 的 employee anchors 处于同一个可达区域；显示 `暂时不能营业：还没有完整且可到达的营业动线`。

如果尝试时某个设备先变成单独 invalid，说明分隔家具压到 anchor 或切断了它自身所需路线：撤销/Store 本次分隔，恢复 P8-M-012 的有效三件组合，再把分隔线向外移一格后重试。

### P8-M-016 现有 Phase 6–7 Decoration regression

步骤：完成一次 Floor furniture、Wall Decoration、Floor style 和 Wall style 的 Preview、Confirm 与 Cancel。

预期：现有操作、Camera、Pause、UI 和 Store behavior 没有明显退化。

### P8-M-017 Console 与长流程稳定性

步骤：在一次 Play Mode 中重复创建、移动、旋转、Cancel、Store 多个功能点，并多次进出 Decoration Mode。

预期：Console 没有新的 error、exception 或持续 warning；Scene 中没有 duplicate views 或残留 Preview。

### P8-M-018 Validation anchors 与数据一致

2026-09-08 执行方式变更（Owner 已批准）：因 Validation Scene 不便人工观察，本项改由 Codex 补充并执行 real Scene 专项技术验收；下列要求不变，不记作 Owner 亲自操作。禁止运行 Configure / Build Assets、保存 Scene 或修改 production behavior。

准备：退出原 Play Mode，打开 `Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`，重新进入 Play Mode 和 Decoration Mode。fresh Scene 只有初始 Counter，上一场 runtime 布局不会保留；先在这个 Scene 内按 `Phase8_Beginner_Guide.md` 的 P8-M-012 三 Counter 示例重新放置并 Confirm CR、CM 和 Pick-up，再查 markers。此 Scene 已启用 debug，不需要也不得运行任何 `Configure ...` menu。

步骤：

1. 在 Hierarchy 中找到 `InteractionAnchorDebugRoot`；其子对象命名为 `AnchorDebug_<Role>_<instanceId>`。
2. 核对 Employee markers 为蓝色，Customer markers 为橙色。
3. 核对 Cash Register 有一枚 Employee 与一枚 Customer marker，且位于相反两侧；Coffee Machine 只有 Employee marker。
4. 核对 Pick-up Point 的 Employee / Customer anchors 是系统自动选择的，两者允许共用一个 cell；Pick-up action bar 不应有 Rotate。
5. Rotate Cash Register、Coffee Machine，或 Rotate / Move 它们的 supporting Counter，然后 Confirm；不要尝试 Rotate Pick-up Point。
6. Confirm 后等待下一帧，确认 `AnchorDebug_<Role>_<instanceId>` 随当前 resolver 结果更新，没有旧 marker 或 duplicate；Preview / Cancel 不改写已确认布局的 markers。
7. 退出该 Scene，打开 `Assets/Scenes/MainCafe.unity` 进入 Play Mode，确认 debug visuals 为 off。

预期：marker 的颜色、角色、数量、方向、Grid cell 与自动 resolver 结果一致；Pick-up 没有 Rotate；正常 `MainCafe` 不显示任何 `AnchorDebug_...` 辅助 visual。

## 7. Phase Final Verification

### Historical full automation baseline (2026-09-04)

以下是 2026-09-04 Phase 1–8 review 修复后的全量通过证据，早于本次 M1/M2 修改。2026-09-08 当前状态：M1/M2 新增 19 cases PASS、direct EditMode 61/61、Core 195/195；Scene/Input 32/33，仍有旧 Touch 顺序验证问题。最新 XML、对照结果和资产审计以 `Docs/Phase8_Beginner_Guide.md` 第 6 节为准，不能用旧 baseline 代替当前验证。

| Scope | Result |
|---|---|
| Phase 8 focused EditMode | `190 / 190` passed |
| Phase 6 migration / validator / layout guards | `372 / 372` passed，包含 `18` 项 layout / tracker guards |
| Full EditMode | `1,689 / 1,689` passed（`p8r-final-full-editmode-round3.xml`） |
| Full core PlayMode | `647 / 647` passed，包含 Phase 8 `81 / 81` |
| Full EditorSceneLoading | `80 / 80` passed，包含 Phase 8 `8 / 8` |
| Full real StandaloneWindows64 | `619 / 619` passed |

上表各套的 failed / skipped / inconclusive 均为 `0`；完整 EditMode 包含并通过上表 `190` 项 Phase 8 与 `372` 项 Phase 6 子集。历史 `p8r-final-full-editmode.xml` 的 67 项失败与 `p8r-final-full-editmode-verified.xml` 的 2 项失败均不能计为 GREEN；共同原因及 tracker 生命周期已修复，Phase 6 共新增 23 项 guards。最终全量只以 `p8r-final-full-editmode-round3.xml` 为准。最终资产审计为 `p8r-final-side-effect-audit.csv` / `p8r-final-side-effect-summary.csv`：1,984 files、0 hash difference / missing / unexpected；37 个测试副作用已备份并恢复。旧 Task 10 report、RED 和 interrupted runs 只保留为历史证据。

### Historical remaining independent gates（2026-09-08；当前状态见第 8–9 节）

- Automated verification（2026-09-08）: **partial / open issue**。M1/M2 focused、direct EditMode / Core PASS；Scene/Input 32/33，旧顺序问题未解决；资产核对 PASS。见 Beginner Guide 第 6 节。
- Engineering independent review: **PASS**.
- QA independent review: M1/M2 bounded code / focused review **PASS**；overall Scene regression **非 PASS**。2026-09-04 的最终 QA decision 与 P8-M-018 Minor 修正为历史 baseline，不消除当前 open issue。
- Production review: **PASS**；focused re-review `0 Critical / 0 Important / 0 Minor`。
- Studio Owner manual / 授权代测（2026-09-08）：**17 项 Owner manual PASS + 1 项 Codex technical PASS / 0 FAIL**。M18 最终专项 PlayMode 15/15（含 4 个新增 M18 tests），独立 QA Spec / Quality 均 PASS，具体证据见 Beginner Guide 第 6 节；不混入 Owner 人工 PASS 数量。M6 incompatible Slot 子项仍未覆盖。
- Task 10 / Phase 8: **In Progress**；M1–M18 主验收记录已齐，但仍待旧 Touch 顺序验证问题、M6 未覆盖子项与 Phase closeout 验证，不标 `Completed`，不自动进入 Phase 8R。

### Historical / superseded Round 3 automation evidence

| Scope | Result |
|---|---|
| Phase 8 Editor PlayMode fixture | `13 / 13` passed |
| Phase 6 drag focused Editor PlayMode | `1 / 1` passed |
| PlayMode assembly boundary | `1 / 1` passed |
| Full StandaloneWindows64 PlayerWithTests | `596 / 596` passed |
| Full graphics-enabled Editor PlayMode | `703 / 703` passed |

以下 Round 3 数字只保留作历史追踪，已由上方 final fresh matrix supersede。其 finalized runs 的 failed/skipped/inconclusive 均为 `0`；`-nographics` Editor run 是保留的 invalid evidence，不是 GREEN，详情见 `task-10-fix-round-3-report.md`。

### Accepted / Deferred Minor Register

九项已知限制的唯一完整 register 位于 `.superpowers/sdd/2026-09-02-phase-8-functional-furniture-layout-readiness/task-10-report.md`。它们是已接受/延期的 test、diagnostic 或 polish limitations：不是隐藏 blocker，也不会自动成为 Phase 8R scope；是否在未来处理仍需单独 review / approval。

### Manual Execution Ledger

Studio Owner reports actual results for M1–M17; M18 is explicitly delegated to Codex on 2026-09-08 and records technical evidence separately. A `PASS` requires the case's stated expectation; a `FAIL` records the failed step, Scene state, Console message, and screenshot/short-video location. Do not repair production while executing this ledger.

| Case | Result | Evidence / notes |
|---|---|---|
| P8-M-001 | PASS | 2026-09-08 Studio Owner 明确报告修复后复测 PASS；此前滚轮移动 Catalogue 内容的 FAIL 已关闭。 |
| P8-M-002 | PASS | 2026-09-08 Studio Owner 明确报告修复后复测 PASS；此前 CR / CM Preview 悬浮、地面红色 footprint 与禁止确认的 FAIL 已关闭。 |
| P8-M-003 | PASS | 2026-09-08 Studio Owner 报告其余 M1–M5 cases 通过；M2 共用 Preview 视觉修复后补看 Coffee Machine 悬浮效果。 |
| P8-M-004 | PASS | 2026-09-08 Studio Owner 报告通过；M2 修复后额外复核地面 invalid Preview 的显示与禁止确认。 |
| P8-M-005 | PASS | 2026-09-08 Studio Owner 报告通过。 |
| P8-M-006 | PASS | 2026-09-08 Studio Owner 在修复后明确反馈“现在好了，继续11-15的步骤”；模型、颜色和地面 Preview 的此前 FAIL 已关闭。incompatible Slot 子项仍未覆盖，不擅自记为通过或 N/A，closeout 前确认。 |
| P8-M-007 | PASS | 2026-09-08 Studio Owner 明确报告“7–10 都 pass”；后续 M6 修改需要相应动作回归，不预先撤销人工 PASS。 |
| P8-M-008 | PASS | 2026-09-08 Studio Owner 明确报告通过。 |
| P8-M-009 | PASS | 2026-09-08 Studio Owner 明确报告通过。 |
| P8-M-010 | PASS | 2026-09-08 Studio Owner 明确报告通过。 |
| P8-M-011 | PASS | 2026-09-08 Studio Owner 明确报告“11-15也通过了”。 |
| P8-M-012 | PASS | 2026-09-08 Studio Owner 明确报告“11-15也通过了”。 |
| P8-M-013 | PASS | 2026-09-08 Studio Owner 明确报告“11-15也通过了”。 |
| P8-M-014 | PASS | 2026-09-08 Studio Owner 明确报告“11-15也通过了”；独立验收已执行。 |
| P8-M-015 | PASS | 2026-09-08 Studio Owner 对照示意图操作后报告“11-15也通过了”。 |
| P8-M-016 | PASS | 2026-09-08 Studio Owner 明确报告“16 - 17都pass”。 |
| P8-M-017 | PASS | 2026-09-08 Studio Owner 明确报告“16 - 17都pass”。 |
| P8-M-018 | PASS — Codex technical | 2026-09-08 Owner 明确授权代测；`outputs/phase8-m18-delegated-20260908/m18-play-final.xml` 15/15、failed / skipped / inconclusive 为 0，含 4 个新增 M18 tests；独立 QA Spec / Quality PASS。角色 / palette / 世界姿态 / Confirm 更新 / shared-cell / MainCafe 清理均覆盖，不计为 Owner 人工 PASS。 |

Phase 收尾至少需要：

- focused RED → GREEN 摘要；
- Phase 8 EditMode focused suite；
- Phase 8 PlayMode / EditorSceneLoading focused suite；
- 完整 EditMode regression；
- 完整 PlayMode regression；
- MainCafe / validation Scene validator；
- P8-M-001–P8-M-017 Studio Owner results，以及 Owner 明确授权的 P8-M-018 Codex 技术代测证据；
- Critical / Important findings 已修复并完成对应 focused regression；
- known Minor items 已进入 accepted/deferred register；不把它们静默变成 Phase 8R 承诺。

## 8. Lifecycle Gate

2026-09-09 三项 UX 的历史交回：UX-01–UX-06 cases、RED/GREEN 与 6 步 manual 操作保存在 `Docs/Phase8_Beginner_Guide.md` 8.4。当时完整 PlayMode **789/789**（686 core + 103 Scene/Input）、Phase 8 EditMode + surface sessions **227/227** PASS，failed/skipped/inconclusive 均为 0。XML：`TestResults/ux-20260909-all-play-verified.xml`、`ux-20260909-edit-verified.xml`。当时独立审查无未处理 Critical/Important，Owner manual Pending，交回 Ready for manual review。当前后续 UX2 见第 9 节；789/227 与以下 781/83/26 都不能当作 UX2 PASS。

本 test-cases 文档与 Phase 8 design 已获批，作为当前 baseline。`Approved` 不等于 manual PASS 或 Phase 完成。2026-09-08 Studio Owner 已确认 M1–M17 PASS；M18 授权 Codex 技术代测 PASS，最终专项 PlayMode 15/15、独立 QA 复查 PASS。M6 incompatible 子项仍未覆盖。

2026-09-09 review 修复最新 authority：完整 PlayMode 781/781、直接 EditMode 83/83、原始 Touch control 26/26（无临时诊断）PASS，failed / skipped / inconclusive 均为0；原 Touch 顺序问题已解决。五项修复的 test cases、RED/GREEN XML、独立 review 与待人工复测步骤集中保存于 `Docs/Phase8_Beginner_Guide.md` 第6节。没有改写上方历史 Owner manual ledger，也不将新修复的 automated PASS 当作 Owner 已复测；Phase closeout 另行决定。

## 9. UX2 Additional Verification / Manual Ledger（2026-09-09）

对应批准合同为 design 9.5 与 Beginner Guide 8.5：选项 2–5 加半透明光感 footprint；选项 1 保留 icon，Exit 不动。本轮 automated **PASS**：完整 Editor PlayMode **813/813**（707 core + 106 Scene/Input），`TestResults/ux2-20260909-all-play-verified.xml`；全部 Phase 8 EditMode + SurfaceSession **235/235**，`TestResults/ux2-20260909-edit-verified.xml`。两套 failed/skipped/inconclusive 均 0、exit 0；asset/font focused 8/8（`ux2-20260909-assets-green.xml`）已包含在 235 内，不重复相加。独立 code 与 7 张截图复核无未处理 Critical/Important，交回 **Ready for manual review**。下列新增 manual 全部 Pending，与历史 M1–M18 分开，不覆盖 M6 incompatible Slot 未执行子项。

表中 Automated PASS 仅表示该验收项对应的自动化覆盖已经通过，不表示左栏所有人工场景、视觉条件或手机组合均已逐项观察。窄屏使用 runtime 真实 Prefab 的 320 logical pixels fixture；本轮未执行 full EditMode、standalone Player 或 Android/iOS 真机。中间 810/813 非 GREEN；Floor Confirm 文案、展开详情后检查原因、Quad 在父空间的旋转后 `1 × 1` 几何三处旧预期已迁移，原行为断言保留，最终完整重跑通过。

| Case | Automated 核验点 | Automated | Owner manual |
|---|---|---|---|
| UX2-M-001 | Tab 竖向 / CategoryId 横向位置分别恢复；内容变化后 clamp；切换终止 drag/惯性；新 session 清空，不写 Save。 | PASS | Pending |
| UX2-M-002 | 无 Preview 显示“继续添加”，有 Preview 恢复 Catalogue / Return 限制；Confirm 不自动展开，入口只在点击时展开，不自动新建。 | PASS | Pending |
| UX2-M-003 | summary + 完整 blocking/warnings details；单一整体原因无多余按钮；新报告默认收起；展开/清理不发布 readiness、不透传输入；窄屏可滚动。 | PASS | Pending |
| UX2-M-004 | 新 CR/CM 优先屏幕内最近中心的合法空 Slot，跳过占用/不兼容；同距、无可见候选和缺 Camera/marker 的 stable fallback；Pick-up 原起点不变，Camera/confirmed 数据不变。 | PASS | Pending |
| UX2-M-005 | Floor 真实 delta count，重复涂抹不增加；Apply All / Undo 只改 Preview；Whole Room Undo 仍禁用；中文 footer 可读。 | PASS | Pending |
| UX2-M-006 | 家具地面 / 墙饰 / CR/CM / Pick-up 四路径半透明绿红 footprint，原占用尺寸/姿态和有效性不变；白 icon、模型、入口蓝区保留。 | PASS | Pending |
| UX2-M-007 | 小窗口 notice / Catalogue / Floor 文案与输入边界；Cancel、退出、重进清理，不残留 Preview/详情/Console exception。 | PASS | Pending |

具体 7 步玩家操作及已查看的 7 张截图链接以 `Docs/Phase8_Beginner_Guide.md` 8.5 为准；每项实际反馈后才更新 Owner manual。截图在 `outputs/ux2-20260909/`，均为真实 MainCafe 640×480 临时 ScreenSpaceCamera。资产 audit `TestResults/ux2-20260909-assets-audit.json` 验证 authoring 后的 1,393 文件基线在 full regression 前后零漂移，不等于本轮无资产修改。既有 Android/iOS 真机 gate 不因 Windows automated 或截图通过而提前完成。技术核验和本表 manual 完成均不自动授权 Phase closeout、Phase 8R、commit / push / merge。

### Footprint brightness follow-up（2026-09-09，8.6 历史；最新见下节）

本节 813/235 现在是提亮前 UX2 baseline。仅提亮 footprint 的最新视觉复测见 Beginner Guide **8.6**，原 UX2-M-006 / 007 与新增亮色检查全部 manual **Pending**。真实 ThemeValid / ThemeInvalid / WallValid 像素测试加旧 7 项共 **10/10 PASS**，`TestResults/footprint-bright-20260909-edit-green.xml`；有效 RED 为 7 PASS + 3 预期亮度 FAIL，最初 Assert.Multiple 编译错误不计 RED。直接 PlayMode **84/84 PASS**，`TestResults/footprint-bright-20260909-play-green.xml`（MainCafe 10 + WallTouch 58 + FootprintLight 2 + FunctionalSurfaceView 14）。两套 failed/skipped/inconclusive 均 0、exit 0；本次未重跑 full Phase。WallInvalid 未独立单测保留 Minor；新目录 `outputs/footprint-bright-20260909/` 的 4 组前后截图已由根代理和独立 UX reviewer 技术复核，无阻挡，Guide 8.6 提供链接，旧图保留。Ready for manual review 不等于每个状态组合、Owner 偏好或手机验收通过。

### Signal-light follow-up（2026-09-09，配色沿用；最新 Preview 见下节）

Owner 批准红绿灯式发光；最终参数 6 / 3 / 1.5、opacity 0.45 / softness 0.12。Beginner Guide **8.7** 集中记录完成的 test cases 与截图：四种真实 tint（含补齐 WallInvalid）、HDR 截断前透明/柔边、亮灰/暖底 LDR 防泛白、旧材质/mesh/depth/dirty guard/幂等检查全部 PASS。Focused **15/15**：`TestResults/footprint-signal-20260909-edit-final-green.xml`；直接 PlayMode **84/84**：`TestResults/footprint-signal-20260909-play-green.xml`，两套 failed/skipped/inconclusive 0、exit 0。RED 11/15、中间 11/15 与 14/15 如实留在 Guide，不算 GREEN；未降低测试阈值，未重跑 full Phase / Player / 手机。

4 组新旧截图独立技术复核无阻挡，新图 `outputs/footprint-signal-20260909/`、旧图保留。所有新增人工项目及 Owner 色彩偏好仍 **Pending**：四路径红绿光、亮暗底纹/刺眼、拖动/Confirm/Cancel/退出无残留。原人工历史结果不改。Ready for manual review 不授权 Phase closeout 或 Git 操作。

### Natural Preview follow-up（2026-09-09，最新 authority）

合同 design 9.6；Guide **8.8** 保存 NP-001–004 已完成 test cases 与 manual 操作：普通家具原材质/MPB 与 footprint 状态恢复；CR/CM 同样原色及事务隔离；墙饰 20 cm Preview 悬浮、跨墙、Confirm 原 contact/同高度、Cancel；五个真实 prefab 与 MainCafe 相框/搁板实际占格/cleanup。直接 PlayMode **301/301 PASS**，`TestResults/preview-natural-20260909-play-green.xml`，failed/skipped/inconclusive 0、exit 0。旧中间 RED/15-of-16 如实记录在 Guide，focused 不重复计数；本轮没有 full Phase、EditMode、Player 或手机验收。

NP-001–004 的 Automated 均 PASS，**Owner manual 全部 Pending**。三个真实近景截图在 `outputs/preview-natural-20260909/`，旧图保留；临时 Camera close-up 不改变游戏相机配置，不能替代手感验收。UX2-M-006 的原“模型保持”以本轮批准的原色/墙饰悬浮为准；其余 ledger、M6 incompatible Slot 未覆盖子项与 Phase/Git gate 均不变。
