# Phase 13 员工任务：Beginner Guide

状态：16项manual已有通过记录：Owner确认 **M-001～004、M-006、M-009、M-016 PASS**；其余9项由已授权的Codex场景代测覆盖（10项代测包含M-006复验）。最新M-009/M-016由Owner明确要求按本人手测通过记录，computer-use未完成的尝试另行保留。台面降低30cm至42cm已实施并完成回归。

2026-10-14本地技术交付及追加回归证据：最终完整 PlayMode 1267 PASS / 0 FAIL / 5项旧 opt-in SKIP；完整 EditMode 2599 PASS（最后微小位移修复前），修复后的P11 Edit增量77 PASS。上述为既有运行，本次仅更新文档及最终review，不重新执行Unity suite。最终证据、修改文件及本轮委托代测结果见 [Validation Report](Phase13_Validation_Report.md)。Codex 场景代测使用真实 Editor / HUD与Decor API，不声称实际鼠标操作；Owner 仍判断动画、杯子与实际操作手感。

## 打开与恢复

1. Unity Hub 打开 `E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`，Editor `6000.5.5f1`。等 Console 编译完成，先保存自己编辑的 Scene，退出 Play。
2. 菜单 `Tools > AnimalCafe > Phase 13 > Create Employee Task Validation Scene` 可重建/补齐独立入口；然后双击 `Assets/Scenes/Validation/Phase13EmployeeTasks.unity`。有 dirty Scene 或正在 Play 时菜单会拒绝，避免保存你的运行中修改。原 P11/P12 validation Scene 不会被重建。
3. 按 Play。独立 Scene 自动准备以下布局、默认关闭进客；沿用顶部时间控制，▶ 在运行时变成暂停，2×可加速。下方只有“收银服务”和“制作咖啡”。等两员工均 `Idle` 才开始任务。

| 设备 | 支撑 Counter 的 Grid cell | 员工站位 | 预置 InstanceId 末八位 |
| --- | --- | --- | --- |
| Cash Register（CR） | `(2,3)`，原 Counter | `(2,4)` | `00000004` |
| Coffee Machine（CM） | `(4,3)` | `(4,4)` | `00000005` |
| Pick-up Point | `(6,3)` | `(6,4)` | `00000006` |

Grid 坐标从入口一侧为 Y=0、向店内增加 Y；X 从左到右0～7。数格从0开始。Scene view 可选 `DecorationModeController` 的 `gridRoot` 引用，查看网格方向；正常经营画面不额外绘制坐标。设备均不旋转。三张 Counter 之间各空一格，前后两排留空。MainCafe 从空店开始时也照此用 Catalogue 放置；每件放好点 `✓`，再拉开 `Catalogue` 选下一件。

4. Hierarchy 选择根 `P13 Employees`。Inspector 有 `EmployeeFlowController`、`PickupCupDisplay`、`EmployeeTaskValidationController`。后一个组件标题右上角菜单提供 **P13** 子菜单：

| Context Menu | 用途 |
| --- | --- |
| `Cancel Barista Task` / `Cancel Cashier Task` | 取消对应员工当前任务；重复操作不补发任务 |
| `Enable Customer Arrivals` / `Disable Customer Arrivals` | 仅在本次 Play 切换现有 P12 AutoSpawn |
| `Let Front Customer Leave` | 只请求 P12 队首受控离场，不代表完成真实订单 |
| `Retry Blocked Employees` | 布局修复后重新验证导航；可能再次暂停，随后检查并点▶/1×再等员工回位 |

下方状态显示角色、状态、剩余游戏秒、Outcome、controller 实际绑定的完整设备 InstanceId、Pick-up Slot 与0～3杯数。`Working`工作，`CarryingToPickUp`持杯去取餐点，`Returning`空手回岗位，`Blocked`无法工作。暂停或忙碌时点按钮会显示拒绝原因，不会排队补做。

员工是 Play 中由 controller 生成的 actor；在 Hierarchy 展开 `P13 Employees`，两个员工实例位于该root下面，选择带`NavigationActor`的root（不是Model子物件），用Inspector的Actor Id末尾`Barista`/`Cashier`辨认。不要改员工 Prefab，不要直接改 Transform 或删 NavMeshAgent。M-014只切换运行时实例的 Active/组件 Enabled。

每个独立用例用 **Stop→Play** 恢复默认布局与0杯。运行时不 Save Scene。需要连续观察恢复的 M-004/M-006/M-007/M-013 在同一次 Play 完成。装修：点既有 Decor 按钮，选择场景家具/桌面设备，`✓`确认，`×`取消 Preview，`Store`及确认弹窗移除，`Done`退出。HUD 在装修时隐藏，原有 Catalogue/时间/Camera 输入继续使用。

## 16 项手工验收

当前结果见各行。Owner最新明确确认M-009、M-016手测PASS；没有把computer-use的BLOCKED尝试改记为PASS，也未补造Owner逐帧测量或截图。台面42cm方案及证据见Validation Report；真实手机/Player不在本指南的Windows Editor证据内。

| ID / 当前状态 | 准备与操作 | 预期 | 恢复 |
| --- | --- | --- | --- |
| M-001 PASS（Owner） | 默认 Scene Play，不点任务；用原 Camera 查看 CR 两侧与 CM 正面 | 仅两员工；Cashier 在CR员工侧，Barista在CM前；朝向正确，不整身嵌入 | 继续下一项或Stop→Play |
| M-002 PASS（Owner） | 1×先按收银服务，结束后制作咖啡，观察 remaining | 收银 working 2游戏秒；制作 working 3游戏秒再出发，不中途跳回 Hold | 等Idle |
| M-003 PASS（Owner） | 从0杯制作一次，完整观察CM→Pick-up→CM | 去程右手一杯、上身稳定、腿走路；停下不原地walk；到点放杯后空手返回，无重复杯 | Stop→Play |
| M-004 PASS（Owner） | 同一次Play做5杯，每次等Barista回CM且Idle | 桌面依次1/2/3/3/3杯，三杯成三角形；第4、5次仍制作/交付成功 | Stop→Play |
| M-005 PASS（Codex场景代测） | working及carrying时各暂停至少2现实秒，再▶恢复；另用2×做任务；暂停时点任务 | 动画/位移/计时一起冻结，暂停不放杯，恢复继续；2×现实耗时约减半；暂停点击显示原因 | 恢复1×，Stop→Play |
| M-006 PASS（Codex＋Owner复验） | working和送杯中各打开Decor后直接Done；再做一次移动Preview但×取消再Done | 同一个任务继续，remaining/手杯保留，无重新制作或误取消 | Stop→Play |
| M-007 PASS（Codex场景代测） | 先交付一杯；另一杯working/送杯时进Decor，将CM旋转90°后✓，或将其支撑桌移到空的`(4,5)`后✓；Done。另测连续两次合法Confirm | 身份与岗位保留，未交付手杯清理；从原位置走回新岗位；已放桌面杯跟随桌，不重复交付；被身体保护拒绝的Preview不算Confirm | Stop→Play |
| M-008 PASS（Codex场景代测） | 按下文“删除与堵路”分别执行 | 明确Blocked/无法营业，不穿墙、不瞬移、不无限原地走；修复后实际回位 | 下文修复或Stop→Play |
| M-009 PASS（Owner手测） | 打开MainCafe并按上表补齐设备；P13组件菜单Enable Customer Arrivals，等至少4客；同时做两个任务，再Let Front Customer Leave；等队伍稳定后确认一次装修 | 同一CR的两侧，身体互相避让；FIFO前移；收银任务不自动消费顾客；装修仅reset顾客、不删员工 | Disable Customer Arrivals；Stop |
| M-010 PASS（Codex场景代测） | 在所有用例期间及Stop后看Console，记录进入前已有warning | 无本阶段新增Error/MissingReference/持续异常；不靠Clear掩盖问题 | 保存日志、不改资源 |
| M-011 PASS（Codex场景代测） | working、carrying、returning分别连点同一按钮3次；另同时触发两员工 | 每人仅一个handle；重复点显示Busy，不多杯、不重置计时、不互相取消 | 等Idle或Stop→Play |
| M-012 PASS（Codex场景代测） | 按下文“多设备”摆法并记录HUD完整绑定ID，再执行任务、移除当前选中设备、修复 | Cashier一直与顾客选中CR一致；Barista任务中不切换目标；装修后稳定重绑或明确Blocked | Stop→Play |
| M-013 PASS（Codex场景代测） | working、carrying、已放杯returning三个阶段，分别用Cancel Barista Task两次；Cashier同理 | 交付前手杯清理、不增加桌杯；交付后已放杯保留；重复cancel不重复副作用，员工走回岗位或Blocked | Stop→Play |
| M-014 PASS（Codex场景代测） | carrying时选运行时Barista root，取消Inspector顶部Active，等1秒再勾上；另一轮仅停用P13 root上的EmployeeFlowController再启用 | 原位停止、身体保护保留、手杯清理；启用从原位验证回位，无瞬移、复活旧杯、重复员工 | 恢复勾选，Stop→Play |
| M-015 PASS（Codex场景代测） | 记录Project Settings > Editor > Enter Play Mode Settings原值；先启用Domain Reload，Play做1杯→Stop→再Play；再关闭Domain Reload做同样两次 | 每次只有两员工/一套P13 HUD/初始0杯；无旧session任务或callback、无新增异常 | Stop，恢复原Enter Play Mode设置，不提交临时配置 |
| M-016 PASS（Owner手测） | MainCafe空店Play先进入Decor，按表补设备；再按下文多Slot步骤做3杯并旋转桌；Game view分别选1080×1920与1600×720，点两个任务/▶/2×/Decor | 空店可装修；有效设备才出现员工/启用任务；杯只在绑定module并随桌；横竖屏真实可点、不遮住原UI；Camera仍可操作 | Stop，还原Game view尺寸 |

## M-008：删除与堵路的固定步骤

删除：默认场景做咖啡，等HUD变`CarryingToPickUp`，点Decor。点`(4,3)`桌上的CM，`Store`→确认→Done。应看到Barista明确无法工作、任务不再交付。重新Decor，在同一桌面的空slot放回`Coffee Machine`→`✓`→Done；用P13菜单`Retry Blocked Employees`，随后检查时间，若为Paused再点▶/1×。等待原来那一位实际走回CM并Idle，随后重新制作。Pick-up删除/添回在`(6,3)`同样操作。删除设备后尚不能营业时仍应允许再次进入Decor。

堵路：重新Play，在Barista已离开CM的送杯途中打开Decor；Furniture选`Counter Module`，放在CM的员工格 **`(4,4)`** 并✓，Done。必须在员工已离开该格时操作，否则身体保护正确拒绝Confirm。该柜台占据CM唯一工作位置，导航应阻止继续营业/回位；它不授权穿墙。修复时Decor选择刚放的空Counter（不是`(4,3)`设备桌），Store→确认→Done，P13 Retry，随后检查并点▶/1×，等实际回位。Retry会重新验证导航并可能暂停时间，不能只看gate已清除就认为已恢复运行。若身体始终挡住目标、无法确认堵路布局，记录该分支BLOCKED和当时位置，不把Preview红色当作已执行堵路。

## M-012：多设备的可重复布局

从默认场景、两员工Idle开始，Decor中添加3个`Counter Module`，保持初始方向：第二组CR在 **`(1,6)`**、第二组CM在 **`(3,6)`**、第二组Pick-up在 **`(5,6)`**。每张桌均先✓再放设备，工作侧`(1,7)/(3,7)/(5,7)`及通道留空。点Done，恢复▶，查看HUD：Cashier工作ID应与`P12 CR`整行/实际队首服务台一致；HUD显示controller真实完整ID，第二组不会因为新建就抢走任务中绑定。

先记录当前Barista的CM/Pick-up完整ID，制作中观察不改变。Cashier绑定ID直接和HUD的`P12 CR`整行核对，无需猜Inspector内部字段。Idle后再Decor，Store默认`(4,3)`CM→确认→Done，原CM选择失效，保留第二台；先Retry，再检查并点▶/1×，等实际回位后检查转到可达第二台或给明确原因。Pick-up同理删默认`(6,3)`上的点；CR同理删默认`(2,3)`上的设备。每次只删一个、等恢复再测下一个。若新增设备不合法，先看现有readiness原因，不重试并忽略红色Preview。本轮Codex场景代测状态见Validation Report，Owner观感仍另行判断。

## M-016：多Slot模块选择

默认场景Play，Decor新增长柜台 **Counter 1 x 3（definition `counter.preset.1x3`）**，起点 **`(5,5)`**，初始方向，占`(5,5)/(5,6)/(5,7)`（1格宽、3格深）。先✓。Store旧`(6,3)`Pick-up→确认，再从Catalogue取`Pick-up Point`，点长柜台**中间格`(5,6)`**→✓，Done。HUD应显示`slot.1`。按三次制作，每次等Idle：三杯均在中间module，另外两个module无杯。下一轮在Decor选择长柜台，移动到`(5,2)`并旋转90°、✓，会占`(5,2)/(6,2)/(7,2)`；不要移到员工身体上。Done后杯跟随原module。若位置有障碍/身体而Confirm被拒，换安全位置后再Confirm并记录坐标。Windows自动场景另验证整套grid/representation/floor刚性旋转37°后的真实交付；Owner仍需看杯与动画。

## MainCafe接线范围

`Tools > AnimalCafe > Phase 13 > Install Employee Tasks in MainCafe` 只补齐`P13 Employees`及其两按钮debug HUD，保留已有P12 root、AutoSpawn、时间/Decor/Camera。MainCafe不自动种设备；初始未就绪不会生成无效员工。原P11/P12场景与原Ceramic Cup Prefab保持原样。两个task是受控验证，不连接真实订单、库存或自动派单。

结果请写入Phase13验收报告的Owner记录，尤其保留working/carry整体观感、挂杯位置、三角形读图的实际判断。轻微杯子视觉穿模按已批准边界观察，不将自动数值测试当作视觉接受。

## 42cm 台面试调后的观感复查（2026-10-14）

Owner已选择降低30cm；本轮保留角色/working动画和CM/CR尺寸。打开P13验证Scene后Play，复查：

1. **桌面与设备**：观察默认CM/CR是否贴台，没有浮空或明显沉入；点击“收银服务”“制作咖啡”，观察手与桌沿的高度是否合适。此项仍由Owner判断，自动高度测试不代替观感。
2. **多格与旋转**：Decor添加1×2、1×3、2×3柜台，放CM/CR并旋转90°，确认设备仍贴台、比例不变，退出装修后员工可步行到工作侧；如Preview被身体占用拒绝，换合法位置。
3. **取餐杯**：连续制作四次，每次等Idle；桌上保持三个杯子，落在新台面，第四次仍能完成；再移动/旋转取餐柜台，杯子跟随原Slot。可结合原M-016一起检查。

以上保留为42cm版本的复查入口。M-009/M-016现按Owner明确指示记录手测PASS；无需因文档更新重复整套manual。单项PASS不扩写为未逐项记录的其他观感结论。
