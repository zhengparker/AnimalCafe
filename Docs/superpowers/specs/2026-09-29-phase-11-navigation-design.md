# Phase 11 — Navigation & Movement Recovery

> 状态：Phase 11 开发与验收完成（现有 `codex/phase-9-order-domain` 分支）；M-001～M-011 均获 Owner PASS。Owner 已授权 review 后 push 和创建 PR；最新 P9–P11 review、完整回归与交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。尚未 merge main，P12 未开始；自动测试与人工验收结果分别记录。
> 日期：2026-09-29。本文是设计方案，不是功能完成报告。
> 配套测试：`2026-09-29-phase-11-navigation-test-cases.md`。
> 实施计划：`../plans/2026-09-29-phase-11-navigation.md`。

## 1. 目标与用户已确认规则

让 Customer 和 Employee 共用可靠的移动服务：从当前位置走到目标，避让其他角色；无法通行时有明确结果。

- 不允许瞬移，包括 Warp、纠正位置时直接挪到 NavMesh、穿障碍后再推回、以销毁重生伪装恢复。
- 卡住后从当前位置重新寻路一次；不能无限 retry。
- 角色应正常避让，不能整个人互相穿过，不能穿墙或穿过实体家具。
- Owner 最新决定优先：到达、转身、避让时，手臂、尾巴或身体边缘短暂轻微穿插可以接受；不要求整个动画 Mesh 在每一帧都零交叠。持续明显穿模、角色穿过对方、靠穿模到达仍不允许。
- 2026-09-29 Owner 补充确认：拥堵时可以等待；装修时不能把家具确认摆到暂停角色身上。
- 使用现有 Shiba、Westie 模型与默认 walk；不制作新角色或新动作。
- 2026-09-30 Owner 授权后续试调：直接修改两份原版 `.blend` 的默认 walk，把摆臂和迈腿幅度放大至 1.5 倍；保留修改前备份，模型尺寸、动作周期和行走速度不变。此项覆盖下文母版只读规则中的肢体幅度部分，验证见 [报告](../../Phase11_Validation_Report.md)。
- Pause 不累计超时；2x 同时影响移动和动画，不重复乘两次倍率。

保留现有约 1.30 m 模型，不为消除所有边缘穿插而缩小模型、强制扩大通道或固定偏移站位。技术默认值与实施范围见本文和配套 plan，最终轻微穿插程度由 Owner 在 Game View 验收。

## 2. 设计准备时的项目证据

依据：Game Design、Development Roadmap 的 Phase 11、Phase Development Process，以及本地 `ParkerGameStudio/project-profile.md`。本节保留实施前的检查快照；最新代码、验证与交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。

| 检查项 | 当前发现 |
|---|---|
| Checkout | 根目录 main 在 `c80f208`；存在用户改动与独立 P9 worktree，不能把其他工作区进展当作本分支已集成 |
| Unity | `ProjectSettings/ProjectVersion.txt` 为 `6000.5.5f1` |
| Navigation dependency | manifest 已有 `com.unity.ai.navigation: 2.0.14`，无需为本阶段另装插件 |
| Assembly | Runtime 尚未引用 `Unity.AI.Navigation`；本方案使用 `UnityEngine.AI.NavMeshBuilder` 的明确 sources 构建，不新增 assembly 引用 |
| Layout | `CafeLayoutRuntime` 提供 `ReadinessVersion` 与 `RecalculateReadiness()`；Grid cell size 为 1 m |
| Anchors | `InteractionAnchorResolver` 提供 Grid position / facing；`DecorationGridSpace` 负责 Grid 到场景坐标转换 |
| Time | `GameTimeService` 已拥有 `Time.timeScale`；复用，不建立第二个时间管理器 |
| Scene | 已有 MainCafe 与 Validation scenes；当前检查未发现正式 NPC 移动代码和导入后的 Shiba/Westie 角色资源 |
| 装修退出 | `DecorationModeController` cleanup 会释放暂停；接入 Navigation 后必须在释放前完成必要检查 |

设计准备时通过 Blender 5.2 background、禁止自动脚本执行，只读打开两个源文件，当时没有保存、导出或修改资源：

| 模型 | Source | Action / 时间 | 实测摘要 |
|---|---|---|---|
| Shiba | `Blender Model Item/Shiba/AnimalCafe_Shiba_Walk_Default_v01.blend` | `AC_Core_Walk_Default`，Action 1–21，30 fps；scene 当前范围 1–20 | Object scale 为 1；高度约 1.307 m；逐整数帧测得水平最大半径约 0.528 m |
| Westie | `Blender Model Item/Westie/AnimalCafe_Westie_Walk_Default_v01.blend` | 同名 Action，1–21，24 fps | Object scale 为 1；高度约 1.307 m；水平最大半径约 0.533 m |

两份文件均有 packed base-color texture；21 个采样帧中 Armature object 没有位移。这不证明骨骼 Root Motion、插值帧、FBX 导出、Unity 材质和 walk loop 已通过验证。

源文件目录被 Git 忽略。实施时从它们导出新的 Unity 资源，保留源文件；不要假设新 worktree 自带这些 Blender 文件。

### 2.1 尺寸 fit 复查（2026-09-29）

本节保留测量与历史依据。Owner 后续接受轻微视觉穿插，以下旧的 0.56 m 全外形范围与 6–8 cm 偏移只用于说明之前的保守估算，不再是实施要求。

**结论：保留现有 Shiba / Westie 尺寸；没有证据要求缩小或重做模型。既有视觉比例有批准记录，动态导航需按实际身体范围验证。**

历史依据：`2026-07-31-phase-3-visual-asset-pipeline-design.md` 第 5.2–5.3 节已经批准 1.30 m 角色基线、0.65 m Work Table 与 0.62 m Coffee Machine；Roadmap 的 P3 closeout 记录 Camera/readability 人工验收完成。该设计明确说明 P3 不决定 NavMesh、通道宽度、Anchor 或移动规则，因此不能把历史比例验收扩大为动态通行 PASS。

本次增加 81 次采样：Blender 中对 Action 1–21 每 0.25 帧评估变形后的 Mesh，相对 Armature object 原点计算范围。该方法覆盖更多插值姿势，但不等同于连续时间证明或 Unity import 验收。

| 数据（m） | Shiba | Westie |
|---|---:|---:|
| 单帧最大左右宽度 | 0.980 | 1.016 |
| 整个动作的左右扫过范围 | 1.039 | 1.064 |
| 动作前后范围 | 0.898 | 0.837 |
| 动作最大水平半径 | 0.528 | 0.533 |
| 最高点（相对原点） | 1.307 | 1.307 |

范围区别很重要：某一帧的宽度能放下，不代表以同一个脚下中心完整播放 walk 都不碰边；不能只看静止 pose。

当前正式 Counter Prefab 的 BoxCollider 为 1 × 0.72 × 1 m，1×3 Counter 为 1 × 0.72 × 3 m；root scale 都为 1。Anchor 在相邻 Grid cell 的中心，对直边的名义距离为 0.50 m。

- **正面站柜台：** 角色前后最远约 0.449 / 0.418 m，小于 0.50 m。因此在轴向、pivot 与导入一致、柜台直边和无其他障碍的条件下，有约 5.1 / 8.2 cm 的源几何间隙。此判断不覆盖端角、转身或其他设备凸出。
- **原地转身：** 完整旋转扫过半径约 0.528 / 0.533 m，可能超出 0.50 m。旧草案 0.56 m 圆形范围故意保守；最新方案已改为主要身体 proxy 与视觉人工验收，不能据此说原模型比例错误。
- **站位处理（最新规则）：** 优先使用原 Anchor；只有导航目标本身需要 sampling 时才在 0.15 m 内找等价位置，不固定偏移 6–8 cm。轻微肢体穿插不自动判失败，也不据此修改模型或 Anchor source。
- **一米通道：** 源 Mesh 动画扫过范围超过一米不等于 gameplay 一定不能通行。当前采用主要身体的移动范围，单人通过与轻微边缘穿插分开验证；不能承诺任意布局都能通过，更不能让两只角色互相穿过。
- **当前布局：** `CafeLayoutRuntime.Initialize()` 的基础 fixture 是 8×8 m 地面、(2,3) 的 1×1 Counter 和 2×2 入口保留区域，空间尺寸本身没有强制缩小角色的依据；这不是任意玩家装修或已打开场景实时状态的通行证明。

当时 `unity status` 未找到配置 Pipeline 的可连接 Editor；设计准备期间没有为 review 安装 package、启动/改动 Unity 场景或导入角色。Unity 实际路径、动画碰撞及 Owner Game View 验收在该检查时为 NOT_RUN；后续实施与验收结果见本文开头的当前状态及合并前 Review。

## 3. 范围与交付

### 本阶段交付

- Navigation 请求、完整路径验证、到达判断、取消、超时与一次 retry。
- 不瞬移的 recovery，碰撞前检查，多角色避让和等待。
- Layout 变化后的旧路径失效、重新构建与营业 readiness 检查。
- 两只真实模型的最小 Unity Prefab、walk 播放和碰撞范围。
- 独立 `Phase11Navigation` 验证场景：可选直达、绕家具、迎面相遇、交叉、堵路、布局变化等情形。
- EditMode、PlayMode、装修 integration tests，以及中文 Beginner Guide 和最终证据记录。

### 后续阶段负责

顾客生成与队伍规则、员工任务、订单完成、付款/退款、库存、P10 业务容量、正式营业循环、Save、多楼层和新动画均不在本阶段实现。P11 只返回移动结果，不擅自让客人离场或让订单失败。

P9/P10 不作为 P11 编译依赖；P11 依赖已存在的 Layout、Anchor、Time 和装修基础。P12–14 以后接入移动服务。

### 可见结果

Owner 能在专用验证场景看到 Shiba 和 Westie 正常行走、绕行、等待、恢复或失败。正式 MainCafe 本阶段不自动生成营业 NPC。

## 4. 方案选择

| 方案 | 优点 | 代价 / 判断 |
|---|---|---|
| 仅 NavMeshAgent 自主移动 | 接线少 | 不足以单独保证不能整个人互穿、不能穿过家具；不采用 |
| **NavMesh 寻路和避让 + 单一移动控制器批准每步位移** | 复用现有 package，碰撞前停止，位置所有权清楚 | 需要验证扫掠、等待和 Agent 同步；推荐 |
| 完整时空预约寻路 | 能进一步规划拥堵路权 | 系统复杂，超出当前阶段，不采用 |

推荐方案不保证任意堵死布局都能通行。安全停下并明确失败是有效结果；不能通过整个人互穿、瞬移或临时关闭碰撞达成“成功”。

## 5. 模型尺寸、移动碰撞与视觉容差

### 推荐初始配置

- 保留模型原尺寸。初始技术配置：移动半径 0.45 m、Capsule 高 1.30 m、碰撞 skin 0.01 m；Agent 与移动 guard 使用同一半径，NavMesh bake 也用同一 Agent type。
- 这是主要身体的 gameplay collision proxy，不要求覆盖尾巴、摆臂和全部 Mesh。实施采用 0.45 m，并已完成相应自动与 Owner 人工验收；不能为通过测试临时改值或关闭 guard。
- 角色相互通行用水平圆形范围和高度区间，墙/家具用 Capsule sweep 加起点/终点 overlap 检查。平面内转身不改变 proxy，但边缘 Mesh 可有短暂轻微穿插。
- 角色之间禁止 proxy 相交和沿路径交换位置；边缘 Mesh 的可接受程度由人工验收判断。持续明显嵌入墙体/家具或整个人互穿必须修复。
- 初始 0.45 m 半径让一米净宽单人直通道成为必须验证的正例，不能继续按旧的 1.12 m 下限自动拒绝。对照窄通道为 0.85 m，必须拒绝；两人不能在单人通道中挤过。
- 不增加刚性“所有通道至少两格”的规则，不固定偏移原站位。若实际表现仍明显穿模，先调整导航参数并复测；需要改变模型尺寸、家具或已批准玩法时另行说明影响。

### 位置所有权

1. NavMeshAgent 提供路径和局部避让意图；关闭其自动写入 Transform position / rotation。
2. 只有 `NavigationWorld` 的移动步骤可以批准并应用位移。Root Motion 关闭，不与动态 Rigidbody 争夺位置。
3. 每次移动前检查 proxy 起始 overlap、整段扫掠、NavMesh 边界和终点范围；遇障碍只走到安全距离或保持原位。Physics query 只计入明确登记的已确认墙/实体家具，不把 Preview、Floor 自身或纯表面装饰当作阻挡。
4. 所有角色在同一 coordinator 中计算，按稳定顺序提交；保留本步已批准的扫掠范围，禁止交换位置、交叉穿过或同时进入同一目标。
5. 检查使用当步真实位置与已提交范围，不能依赖尚未同步的 Collider。停着、失败但仍可见的角色仍占据空间。
6. 将批准后的真实位置反馈给 Agent 模拟；不把模拟位置强制复制到角色，也不在运行时重开自动 position sync。

浮点比较容差建议 0.001 m，仅用于 proxy 测试数值比较，不允许 proxy 互穿。测试既检查连续移动区间，也检查最终距离。

该数值用于 collision proxy；轻微视觉穿插不按毫米自动判定。Owner 看 Game View 中的程度与持续时间，不能用测试 proxy 不重叠替代视觉验收。

## 6. 请求、到达与恢复

### 最小接口

- `NavigationService.MoveTo(string actorId, NavigationTarget target, Action<MovementResult> completed, NavigationTarget? recoveryTarget = null)` 返回 `MoveStartResult`，包含 Accepted、RequestId 或明确拒绝原因；取消使用 `Cancel(long requestId)`。
- `NavigationTarget` 保存 world Position 和可选水平 Facing；`MovementResult` 保存 RequestId、ActorId、Status、Reason、OriginalFailure、RecoveryFailure、FinalPosition、RetryCount（0 或 1）。完整接口见 plan Task 1。
- 同一 actor 只允许一个请求；已有请求时新请求返回 Busy，显式 Cancel 后才能替换。
- destination 可以来自 Anchor 或测试场景。请求记录 request ID、layout revision、原目标及原始失败原因。
- 预期失败通过结果返回；不以反复 Console exception 表示正常的堵路。
- 结果区分 `Arrived / Recovered / Failed / Cancelled / LayoutChanged`；失败原因至少区分无效起点、无效目标、不完整路径、超时、恢复失败、角色失效。
- `Recovered` 只表示正常走到 recovery point，原任务没有完成；上层不能把它当作柜台服务或取餐到达。

### 开始与到达

- 起点必须就是当前真实位置，并已经在有效区域内；不能采样附近点后搬动角色。
- 目标允许小范围 sampling（初始上限 0.15 m），但必须保持同一可行走区域与 Anchor 语义；检查障碍、完整路径和最终身体范围。不能采样到墙背后、柜台对侧或其他楼层。
- 相邻柜台优先原 Anchor；sampling 只能保持同一站位区域、同一柜台侧和 facing，不能按旧的全外形包围强制偏移。轻微边缘穿插按第 5 节处理。
- 检查 complete path；partial path 不算成功。路径计算等待也有 scaled timeout。
- 到达同时要求有效完整路径、实际位置距采样目标不超过 0.08 m、剩余路径不超过 0.08 m、实际速度不超过 0.05 m/s；有 Facing 时还需角差不超过 5°。停止时可以正常转向，转向最大速度 360°/game-second；不能仅凭速度为零或 remainingDistance 为零。
- 目标被另一角色占用时等待，不挤入；目标同时到达冲突由 coordinator 阻止。这是空间互斥，不代替 P10 的业务 reservation。

### 避让与超时

- NavMesh 局部避让提出绕行方向；coordinator 检查后才移动。迎面相遇采用稳定优先顺序，低优先角色先等待，有安全侧方空间才正常绕行。
- 不能承诺所有窄路相遇都能自动解开；没有足够退让空间时停下并按超时规则结束。
- 初始建议：最大速度 1.2 m / game-second，3 game-seconds 无有效进展触发卡住检测；每次路径计算最多 2 game-seconds。
- 单次行程上限建议 `max(10 s, 3 × pathLength / speed)`；不能靠左右抖动无限重置总时限。
- 首次卡住或行程超时，从当前位置重算原目标一次；保留请求的 retry-used 标志。
- 仅沿路径剩余长度减少累计至少 0.02 m 才重置无进展计时；纯转向阶段以角差减少为进展，沿用总时限。目标被占、被 guard 阻止移动或局部左右摆动不构成无限延期理由。
- 第二次失败：未配置 recovery point 就返回 Failed。若调用方配置了安全点，只允许额外一次完整路径验证并正常走过去；也受时限约束，不能再次 retry 或递归 recovery。
- 初始目标本身无效直接失败，不先走一段；验证场景以无 recovery point 为默认，另有专门测试验证 walk-only recovery。
- 取消、disable、destroy、场景卸载、成功、失败的 terminal callback 只调用一次；先封闭旧请求再通知调用方，防止回调重入影响新请求。

## 7. 时间和动画

- 使用现有 `Time.timeScale` 与 scaled delta；Pause 时 movement、timeout、walk 均停止。
- 为长帧将实际运动拆为最多 1/60 game-second 的检查步，每帧最多 16 步；超过的运动预算丢弃，允许减速，不能用大位移追赶。超时累计完整 scaled delta，不因运动限步延长请求期限。
- 重用正常 walk，不使用 Hurry。导出以 Action 1–21 为依据，避免 Shiba scene 1–20 截断循环。
- 保留源文件各自时间信息（Shiba 30 fps、Westie 24 fps）；Unity 中按实际位移速度分别校准步频，不把同名 Action 当作同样时长。
- 只在实际移动时播放 walk；等待、失败和到达时切换到从该 walk 提取的静止姿势。无需新增 Idle 动作；Owner 验收姿势是否自然。
- walk 使用原地播放，根节点平移由移动服务控制。若导出发现骨骼整体漂移，只处理导入副本的 Root Motion，不重写 Blender 母版。

## 8. Layout 变化和已完成工作影响

Navigation 使用 `NavMeshBuilder` 的显式 `NavMeshBuildSource` 列表；来源是已确认地面、墙和实体家具几何，不从整个 scene 自动收集。NPC、Preview、调试图形、壁纸/地面外观层不进入 bake。按 world 持有 NavMeshDataInstance，重建在暂停期间进行，结果仅能发布到匹配的 revision；构建失败保留恢复限制。P11 不使用 NavMeshLink，不自动 traverse OffMeshLink，Agent 关闭 autoRepath 以遵守一次 retry。

### 接入顺序

1. 装修入口先暂停角色；Preview 不改变正式 Navigation 数据。
2. 家具确认前检查候选实体碰撞形状是否覆盖暂停角色的 collision proxy；覆盖则拒绝此次确认并保留 Preview，提示先改变摆放位置。不能把角色挤出、隐藏或删除。该规则不是给持续明显穿模开例外；小范围边缘动画穿插仍需 Owner 视觉验收。
3. 合法 Confirm / Store 后标记 Navigation revision 失效，停止使用旧路径。材质、壁纸等仅外观修改不应无故重建。
4. 退出装修前，在角色仍暂停时用已确认的实体布局重建 NavMesh，并同步 Collider。
5. 检查所有现有角色位置、入口与必需服务 Anchor 路径。Anchor 无效则给出具体位置或 station ID。
6. 只有 Layout readiness 和 Navigation readiness 都通过才允许恢复；失败可退出编辑保留装修，但继续禁止经营恢复，并提供重新进入装修的提示。
7. 旧请求以 LayoutChanged 结束一次；不能沿旧 path 自动继续。后续调用方从当前位置提交新的请求；验证场景用明确按钮演示重发。

### 影响说明

- 新增装修 Confirm 的 actor-overlap guard、布局失效通知及退出/恢复 gate，会触及已完成 P8/P8R 接线；这部分必须随本文一起批准。
- 保持 `LayoutReadinessReport` 的既有含义；Navigation readiness 独立组合，不把“格子可达”当作“角色身体可通行”。
- 当前 `ReadinessVersion` 是重算次数，不能不加区分地当作几何版本；adapter 只在正式布局的阻挡或 Anchor 变化时发出失效。
- 不在纯 Layout domain 内加入 Unity/NavMesh 依赖。controller 在成功提交实体或 Anchor 变化后通知 adapter；adapter 按确认后的 instance ID、definition、pose、实体几何和 Anchor 内容作结构比较，变化才递增独立 revision。外观改变不计入；不用碰撞风险较高的单个 hash 作为唯一比较证据。
- 当前暂停协调器释放时可能恢复速度。接线必须在释放之前设置有效的经营恢复限制，并覆盖时间按钮绕过、重复退出、disable cleanup；不能一帧先恢复再暂停。
- 新 guard 仅在场景明确配置 Navigation integration 时启用；现有无 NPC 的验证场景保留兼容路径。
- 保留 P8 的局部可用性：Navigation 检查现有角色起点、入口、可参与营业的站位组合；缺失必要组合才阻止营业，单个非必需 station 路径失败只标记该 station 不可用。不能要求所有 station 都可达才开门，也不能把所有 station 检查成功但关键服务间路径断开当作 ready。
- P11 首先在独立验证场景接入真实装修组件和 adapter，`enforceBusinessReadiness=true`。MainCafe 不加自动 NPC，本阶段只加入待营业使用的接线并明确设为 false，避免改变既有无 NPC 装修演示；P12–14 接入营业时必须显式启用 gate。该开关不允许作为验证场景中绕过失败的办法。
- 无 Save schema 迁移；不改用户已有 P9 文件或 unrelated root changes。模型母版仅允许上文 2026-09-30 授权的 walk 肢体幅度试调。

## 9. 预计文件范围

设计准备阶段仅更新本文、配套 test cases，并新增 implementation plan。以下为当时批准的实施范围，具体文件与后续调整由 plan 记录：

| 位置 | 用途 |
|---|---|
| `Assets/Scripts/Navigation/` | Request/result、可测试的请求生命周期、NavMesh adapter、单一移动 coordinator、碰撞 guard、walk presentation、layout adapter |
| `Assets/Editor/Phase11/` | 受控 FBX 导入配置、模型与场景验证、验证场景生成入口 |
| `Assets/Art/Phase11/Characters/{Shiba,Westie}/` | 从现有源文件导出的 FBX、纹理、Material、最小 Prefab 与 Animator |
| `Assets/Scenes/Validation/Phase11Navigation.unity` | 独立人工验收场景；包含多个可复现情形 |
| `Assets/Tests/EditMode/Phase11/` | 生命周期、时间、互斥、布局失效和参数验证 |
| `Assets/Tests/PlayMode/Phase11/` | 真实 NavMesh、碰撞与模型动画验证 |
| `Assets/Tests/PlayMode/EditorSceneLoading/` | 真实装修组件、Pause/Resume 和验证场景 integration |
| `Assets/Scripts/Decoration/CafeLayoutRuntime.cs` | 不改变现有 readiness 语义；通过已有 Layout/Readiness 查询供 adapter 使用，无需新增 event 或 NavMesh 依赖 |
| `Assets/Scripts/Decoration/DecorationModeController.cs` | 最小 Confirm / exit guard 接线，复杂新逻辑放 adapter |
| `Assets/Scripts/Core/Time/GameTimeService.cs`、`Assets/Scripts/UI/TimeControlPanel.cs` | 可释放的 resume block 与状态提示；保留 `IGameTimeService` 接口和未接入场景行为 |
| `Assets/Scenes/MainCafe.unity` | 仅批准的 Navigation/装修接线，不新增营业循环 |
| `Docs/` | 后续实施计划、Beginner Guide、Phase report；验收时更新 Roadmap，已批准规则合并到 Game Design |

Unity 自动生成对应 `.meta`；通过 Unity Editor/工具生成 scene、Prefab、asset，不手写其 YAML。复用现有 assembly，不建通用服务框架。

## 10. 验证与交付边界

- 先实现可失败的 focused tests，再实施对应行为；每 Task 只跑直接相关 regression。
- 模型导出导入后先检查材质、单位、骨骼、loop、碰撞包围，才开始依赖其尺寸做路径验收。
- 用真实 NavMesh PlayMode 测试证明 Unity 行为；纯逻辑 mock 不能代替。
- 收尾按项目流程跑完整 EditMode / PlayMode、Engineering 与 QA review，以及必要的模型视觉检查。
- Owner 在真实 Game View 验收：模型尺寸、walk、停步、避让、狭路等待、装修后恢复，以及边缘穿插是否轻微短暂；不能要求全部 Mesh 零穿插，也不能漏掉明显穿过障碍或角色。
- 自动化、视觉人工验收分别报告；未运行标记 NOT_RUN，缺资源/环境标记 BLOCKED，不把 fixture 或截图当作 gameplay PASS。
- 设计阶段的 Blender 只读检查本身不代表 Phase 11 通过；自动证据与 M-001～M-011 Owner PASS 分开记录。Phase 11 开发与验收已完成，Owner 已授权 review 后 push 和创建 PR；最新完整回归与交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)，尚未 merge main，P12 未开始。

## 11. 技术参考与下一步

本地 AI Navigation 2.0.14 package 为实施版本；官网 2.0 页面当前可能显示更新的小版本，不据此升级 dependency。

- [Unity：NavMesh 与其他组件配合](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/MixingComponents.html)：保持位置单一写入者，避免 Root Motion/Physics 争夺位置。
- [Unity：SamplePosition](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html)：找到附近 NavMesh 位置不能代替障碍与完整路径检查。
- [Unity：CapsuleCast](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.CapsuleCast.html)：扫掠之外仍需检查起始重叠。

Owner 已确认保留现有模型、可以等待、拒绝把家具摆到角色身上，以及允许到达/转身/避让时轻微短暂边缘穿插。配套 plan 已将这些规则落实为实施步骤和验收；Owner 最终确认 M-001～M-011 PASS。此前测试证据与 Test Runner 观察限制见 plan、Phase11 Beginner Guide 和 Validation Report；最新 P9–P11 review、完整回归与现有 P9 分支的交付状态统一见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。main merge 留待 Owner 决定，P12 未开始。
