# Phase 13 — Employee Task Flow Design

> 状态：Approved Design — Owner 于 2026-10-13 确认本设计与配套 test cases，并授权编写 implementation plan。
> 设计日期：2026-10-13（America/Toronto）。Owner 于 2026-10-14 批准 implementation plan 并授权使用 subagent 开发；实现与 Unity 验证状态见 [Validation Report](../../Phase13_Validation_Report.md)，Owner manual 尚未执行。
> 基线：`302f3e969da667f3f389820d303503865b1b0d63`；branch：`codex/phase-13-employee-task-flow`。
> 工作区：`E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`。

## 1. 目标与边界

让固定 Cashier、Barista 可靠执行受控任务，并让玩家看见 working、端杯移动和桌面交付。复用既有 Layout、StationReadiness、Interaction Anchors、Navigation、GameTime 和角色素材。

- Included：两个固定岗位、稳定员工身份、station binding、单员工单 task handle、服务/制作计时、交付、返回待命、取消、failure/recovery、装修生命周期、工作与端杯视觉、最多三杯的桌面展示，以及真实 Scene 验证。
- Not Included：真实订单派发、付款、材料消耗、Customer 取杯、Order 状态提交、制作队列、经济、Save、离线经营、多员工岗位扩展、新角色 Rig 或重新制作动画。
- P12 顾客队列继续拥有自己的生命周期。Cashier 测试任务完成不让真实队首自动离场，也不修改 P9 Order 或 P10 Capacity。
- P14 再连接真实 Customer / Order；P13 的杯子展示不是订单或取餐容量。

依据：[Game Design](../../AnimalCafe_Project_Design.md)、[Roadmap](../../AnimalCafe_Development_Roadmap.md)、[Phase Process](../../AnimalCafe_Phase_Development_Process.md)。P12 合并与 P13 readiness 的历史交接在主 checkout 的 `outputs/p13-readiness-20261013/Readiness_Review.md`；其中旧 CI counts 仅是历史基线证据，本阶段实际运行结果另见 Validation Report。

## 2. Owner 已确认的玩家规则

### 2.1 岗位与任务入口

- Cashier 在绑定 CR 的 Employee Side 待命，位于 Customer Side 的相反侧。
- Barista 在绑定 CM 的 Employee Interaction Anchor 待命，即 CM 的 `+Z` 正面。
- 不设置另外的休息位置。
- P13 使用两个测试按钮，分别触发 Cashier 服务与 Barista 制作/交付任务。
- Cashier：在 CR 工作位服务，完成后原地待命。
- Barista：CM 前制作 → 手持杯子步行到 Pick-up 的员工交付位置 → 放杯 → 空手返回 CM 待命。

### 2.2 时间与动画

- Cashier 服务：**2 个游戏秒**，期间播放 working。
- Barista 制作：**3 个游戏秒**，期间播放 working。
- 到达正确 Pick-up 交付位置后立即放杯并完成制作/交付，不增加放杯等待时间。
- Pause 冻结位移、转身、working、walk/carry 和工作/受阻计时；`2x` 同时作用于这些行为。
- Barista 送杯时手里实际显示 Cup，上半身保持端杯姿势，下半身播放走路；返回时不拿杯。

### 2.3 杯子展示

- 每个 table module 最多显示三个杯子，三个时呈三角形；轻微视觉穿模可接受。
- 第四杯及之后仍只显示三个；展示饱和不拒绝或阻塞新制作任务。
- 放下杯子后该杯制作/交付流程结束。Barista 仍须实际步行回 CM，才能在 CM 开始下一杯。
- 桌面杯子只是视觉展示，不作为永久家具，不占新的 Layout Surface Slot，不作为取餐库存或容量限制。
- 先前“一个杯子占满 Pick-up、清空后才允许下一杯”的提议已撤回，不得实现。

### 2.4 装修

- 进入 Decoration Mode：暂停员工移动、动画与计时。
- 没有确认任何布局修改就退出：继续原任务，不丢失剩余工作时间或手持杯子。
- 确认布局修改后退出：取消旧任务，清理尚未交付的手持杯子；保留员工角色、身份与岗位，按新布局步行回工作站待命。
- 工作站删除或无法到达：明确显示无法工作，等待布局修复，不瞬移、不销毁重生。
- 员工不参加 P12 的 Customer batch reset。

### 2.5 Station binding

- Cashier 使用 P12 顾客队列选中的 CR。
- Barista 固定使用一台可用 CM 和从该 CM 可达的 Pick-up Point。
- 多个候选按稳定顺序选择；任务中不换设备；确认装修后重新验证绑定。

## 3. 已批准的技术方案

以下方案随本设计获批；Owner 对设计的批准允许编写 implementation plan，不跳过 plan 的审阅和执行方式选择。

### 3.1 独立员工生命周期与设备选择

- 两名员工各有稳定 ID、Role、绑定的 station IDs、当前状态与最多一个 active handle。角色品种作为 Scene 配置，不能把 Cashier/Barista 行为写死到 Shiba/Westie 的类型中；验证先复用现有两种角色。
- 首次生成必须验证工作位、NavMesh 与其他角色身体占用；位置无效时不生成重叠员工。初始化位置仅用于首次出生，后续恢复均从实际当前位置步行。
- 优先读取 P12 的 `SelectedRegisterId`。队列尚未完成初始化时，仅允许按 P12 同一稳定 InstanceId 排序规则得到同一 CR，或保持等待；不能单独选择另一个 CR。
- 选取可用 CM/Pick-up 组合时按 InstanceId 的 ordinal 顺序，检查 FunctionType、Employee Anchor 和既有 Navigation 的完整可达性。没有完整组合时明确失败。
- 同一任务保存 binding IDs 与 layout revision。确认装修后重新选择/验证；不能在旧任务里悄悄改目的地。新目标被其他角色暂时占用时采用既有有界 movement failure/recovery，而不是无限等待。
- P12 可能允许空 Employee Anchor 被队伍使用；出现真实员工后，实际身体继续参与 World collision 与排队/admission 的保护。

### 3.2 Task 状态、成功点与 single handle

建议员工状态：`Initializing / Idle / MovingToWork / Working / CarryingToPickUp / Returning / Blocked / Disabled`。Cashier 已在工作位时可跳过 MovingToWork。

- 员工未 Idle、存在 active handle、布局不可用或处于 Pause/Decor 时，测试入口拒绝新任务并显示原因。P13 不增加 waiting task queue，不决定正式经营中 Pause 下派单的规则。
- 先建立 handle、generation、phase 和目标，再调用 `NavigationService.MoveTo`，兼容同步 callback。
- Cashier 工作计时完成即服务成功；Barista 必须实际收到交付目标的 `Arrived` 并成功完成放杯，才标记交付成功。`Recovered`、中间路点 Arrived、超时或旧 callback 都不是交付成功。
- 交付成功与员工回位状态分开记录：放杯后保留成功结果，员工处于 Returning；返回期间不接新任务。返回实际完成后释放 handle 并 Idle。
- 返回失败时保留已交付结果，退休旧 handle、执行 cleanup 一次，员工进入明确 Blocked；不能伪报 Idle 或回滚已放下的杯子。
- 任务取消/失败时使 generation 失效、停止属于该 handle 的请求、移除未交付手持杯子；cleanup exactly-once。重复 cancel、同步 callback 和过期 callback 不重复提交或清理。
- 取消或失败后，当前布局与工作位可用则尝试步行回岗位；否则保持 Blocked。恢复只在明确布局修复或验证操作后重试，不能产生每帧无界 retry。

### 3.3 时间与动画所有权

- `Time.deltaTime` 已被 GameTime/Unity timeScale 缩放，工作与其他 P13 计时只消费一次 scaled delta，不能再乘 CurrentSpeed。
- 工作时长与动画循环长度独立：working 按游戏时间循环，2/3 秒到期才结束任务，不按 Animation Event 决定业务成功。
- root motion 保持关闭；NavigationWorld 继续是位移的唯一执行者。P11 的碰撞、角色尺寸、skin、到达容差、避让与失败规则不放宽。
- 员工的 Animator 状态必须只有一个选择者。当前 `NavigationWalkPresenter` 会按真实速度写 `Walk / Hold`；员工扩展须协调这个入口，避免 working 被 Hold 覆盖。允许为员工模式增加最小 presentation hook，Customer/默认 P11 模式行为保持，并跑直接回归。
- Carry 建议使用 upper-body AvatarMask / Animator Layer：保留现有 Walk/Hold 驱动下半身，将固定 Carry pose 作用到胸部、头部、双臂与手；不覆盖 root、pelvis 和腿部移动。
- 杯子实例挂到已验证的 `hand_socket.R`。手持杯与桌面杯分别由各自生命周期拥有，交付/取消不会留下重复杯或漂浮杯。

### 3.4 素材证据与导入

2026-10-13 用 Blender 5.2 后台、`--disable-autoexec` 只读打开下列原始文件，未保存或修改：

| 素材 | 已核实内容 | P13 用法 |
| --- | --- | --- |
| `Blender Model Item/{Shiba,Westie}/AnimalCafe_{Species}_Work_Generic_Busy_v01.blend` | Action `AC_Core_Work_Generic_Busy`；Shiba Scene 1–36、Action 1–37 帧/30fps，Westie Scene/Action 1–37 帧/24fps；双臂、胸部、头部运动，腿部固定 | 两岗位 working loop，导出完整 Action endpoint |
| `Blender Model Item/{Shiba,Westie}/AnimalCafe_{Species}_Carry_Cup_v01.blend` | Action `AC_Core_Carry_Cup`；1–2 帧固定姿势，没有任何随时间变化的骨骼；`CarryProp_Cup` 挂在 `hand_socket.R` | 提取上半身固定 pose，组合现有 Walk；不是已有完整 CarryWalk clip |
| `Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab` | 已有 Cup visual 与底部 pivot、Collider、Material | 手持与桌面杯复用 |
| `Assets/Art/Phase11/Characters/{Shiba,Westie}/` | 当前 Unity Controller 为 Walk / Hold | 保留原角色与 Customer 行为，增加员工专用资产/配置 |

- 原始 Blender Model Item 是主 checkout 的本地素材，未随 worktree 复制。实现时从 `E:/Unity/Project/AnimalCafe/Blender Model Item` 只读导出到 P13 worktree；不保存母版、不复制整个素材目录。
- 2026-10-14 只读预检进一步确认：既有 P11 FBX 省略 non-deform sockets，员工专用导出须保留 `hand_socket.R`；Carry Action 仅右臂有固定曲线，需烘焙源文件的完整上半身 pose 后再用 mask，不能只复制这些曲线。上述帧数修正是素材证据澄清，不改变已批准的 2/3 游戏秒工作规则。
- 延续 `ArtSource/Phase11/Tools/ExportNavigationCharacters.py` 的导出约定，验证骨骼路径、scale、forward、帧率、loop 与 socket。不能直接使用仅导出 Walk 的原脚本假定已有 work/carry。
- Blender 自带的 `CarryProp_Cup` 作为挂点参考，不与运行时 Cup Prefab 重复导出为第二个手持杯。
- 不改 locked MasterRig 或已接受的 P11 walk。只复用本阶段需要的已有动画；Roadmap P21 的完整角色/动画 pipeline 不在本轮完成。
- Blender 技术采样不代表 Unity 动画效果或 Owner 视觉 PASS；接入后仍需人工观看。

### 3.5 桌面杯显示

- 建议以 Pick-up 绑定的 `1 × 1 Surface Slot` 对应 table module 作为显示单元；不会因为桌子有多个 Slot 就自动生成额外 Pick-up。
- 每次有效交付让该单元的 display count 增加到 `min(3, count + 1)`。三个固定的家具局部 offsets 构成三角形；第一杯/第二杯逐个显示对应位置。
- 第四杯之后完成手持杯清理与交付成功，桌面数量仍为三；不再无限 instantiate、叠加隐藏 objects 或维护真实未取餐订单数量。
- Cup 是商品视觉，不能注册为 furniture/Navigation obstacle。显示模式避免 Cup Collider 干扰角色路径或装修输入；不修改原 Cup Prefab 的全局 Collider 合同。
- 桌面杯按支撑家具/Slot 的局部位置显示，家具移动/旋转时跟随。确认删除支撑家具、移除对应 Pick-up 或 Slot 失效时清理对应展示；取消 Preview 不清理。
- 已交付桌面杯不归旧员工 task 的 cancel cleanup 管理。测试场景的重新初始化可清空展示，但不以清空为继续制作的前提。

### 3.6 装修、disable 与失效 callback

- 进入 Decor 后保留 active handle 与剩余计时，仅暂停；没有正式布局修改则恢复同一任务。
- 一旦正式 layout revision 变化，先退休旧 generation / request，保证旧 callback 无效；退出 Decor 且布局可运行后重新绑定并从真实位置回岗位。
- 多次 Confirm 属于同一装修会话，也只清理每个旧 task 一次。Customer reset 不访问员工列表或桌面杯 task ownership。
- 组件/角色 disable 取消当前任务并停止动画/移动；重新启用从实际位置验证工作位，不自动恢复已取消的任务。仍存在的身体保留既有碰撞保护。
- destroy 清理该员工所有 owned 请求与未交付杯，不能影响另一员工、顾客或已交付杯。
- Missing station、回位失败、配置错误、真实 occupant 阻挡都有可读的状态；修复前不能假装任务或 Idle 已完成。

## 4. 文件范围建议

保持现有 namespace 与 asmdef，不增加新 package。

| 位置 | 用途 |
| --- | --- |
| `Assets/Scripts/Employees/` | Role、binding、controlled task、员工流程、动画/手持杯和桌面杯展示；只拆分有独立责任的小类 |
| `Assets/Editor/Phase13/` | 可重复构建验证场景、员工动画导入/配置；不编辑 Blender 母版 |
| `Assets/Art/Phase13/Employees/` | 从现有 work/carry 源导出的 clips 与员工专用 Controller/Prefab |
| `ArtSource/Phase13/Tools/` | 小型只读导出工具，输入主 checkout 母版，输出当前 worktree |
| `Assets/Scenes/Validation/Phase13EmployeeTasks.unity` | 两岗位、工作站、两个任务按钮和失败/装修验证 |
| `Assets/Tests/EditMode/Phase13/`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase13/` | focused 状态/计时与真实场景验证 |
| 既有 Navigation presenter/Actor、Scene setup 或 Layout integration | 仅加入必要接线；先在 plan 列出准确 files 与直接 regression |
| `Docs/Phase13_Beginner_Guide.md`、`Docs/Phase13_Validation_Report.md`、Game Design/Roadmap | 在相应交付时更新，不提前声称实现/完成 |

建议先用独立验证 Scene 做最小流程，再接入 MainCafe 与 P12 队列共同验证；不将单场景测试 PASS 代替 MainCafe 集成。每个 Task 的准确文件名、RED/GREEN 顺序与接线由 written implementation plan 列出。

## 5. 验收与后续 gate

- 对应 [P13 Test Cases](2026-10-13-phase-13-employee-task-flow-test-cases.md)。
- Task 内跑 focused tests 和直接 regression；Phase 收尾集中跑完整 EditMode/PlayMode、Engineering/QA 与必要 Art 技术 review。
- Owner 人工观看 working、稳定端杯行走、放杯/空手返回、三角形杯子、连续制作、暂停/倍速和装修恢复。
- Android/iOS Player/device 单独记录，本轮 Editor 证据不代替它们。
- 当前状态：设计与初版 test cases 已批准；实现 `NOT_RUN`；tests `NOT_RUN`；Owner 视觉验收 `NOT_RUN`；commit/push/PR/merge `NOT_RUN`。
- 下一步：审阅 [P13 Implementation Plan](../plans/2026-10-13-phase-13-employee-task-flow.md) 与随计划补充的边界/手工用例，选择执行方式后才开始代码实现。
