# Phase 11 — Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for sequential native execution, or superpowers:subagent-driven-development only if the user selects that approach. Steps use checkbox (`- [ ]`) syntax for tracking.
> 状态：Phase 11 开发与验收完成（现有 `codex/phase-9-order-domain` 分支）；M-001～M-011 均获 Owner PASS。Owner 已授权 review 后 push 和创建 PR；最新 P9–P11 review、完整回归与交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。尚未 merge main，P12 未开始；历史实施进度保留在本 plan 与对应 SDD ledger。

**Goal:** Shiba 和 Westie 使用现有 walk，从当前位置可靠行走、避让、等待与有限恢复，支持装修后的路径失效。

**Architecture:** NavMesh 负责完整路径与局部避让，`NavigationWorld` 是角色位置唯一写入者；`NavigationService` 管理请求、超时与回调，`NavigationCollisionGuard` 保护主要身体的移动范围。装修 adapter 将已确认布局转换成 Navigation 数据，并通过现有 GameTimeService 限制无效布局恢复；轻微边缘 Mesh 穿插由 Owner 验收。

**Tech Stack:** Unity `6000.5.5f1`、C#、`UnityEngine.AI`、现有 AI Navigation `2.0.14`、NUnit / Unity Test Framework、Blender `E:/Blender/blender.exe`；不新装或升级 dependency。

**Spec:** [Phase 11 design](../specs/2026-09-29-phase-11-navigation-design.md)

**Test Cases:** [Phase 11 tests](../specs/2026-09-29-phase-11-navigation-test-cases.md)

## Global Constraints

- 不允许瞬移，包括 Warp、纠正位置时直接挪到 NavMesh、穿障碍后再推回、以销毁重生伪装恢复。
- 卡住后从当前位置重新寻路一次；不能无限 retry。
- 角色应正常避让，不能整个人互相穿过，不能穿墙或穿过实体家具。
- 到达、转身、避让时短暂轻微边缘 Mesh 穿插可接受；保留原模型约 1.30 m 尺寸。不得重新强加全部 Mesh 零穿插、固定 6–8 cm 站位偏移或全部通道至少两格。
- 初始半径 `0.45 m`，Capsule 高 `1.30 m`，skin `0.01 m`，数值比较 epsilon `0.001 m`；Agent、bake、guard 使用同一半径。1.00 m 直通道为单人正例，0.85 m 为负例。
- 最大速度 `1.2 m/game-second`；转向 `360°/game-second`；到达距离/remaining distance `0.08 m`，实际速度阈值 `0.05 m/s`，Facing 阈值 `5°`。
- Sampling 上限 `0.15 m`；同站位区域、同柜台侧；起点不得 sampling 后挪动。
- 无进展 `3 game-seconds`；有效进展阈值 `0.02 m`；pathPending 上限 `2 game-seconds`；单段行程上限 `max(10 s, 3 × pathLength / speed)`。
- 运动 substep 最多 `1/60 game-second`，每帧最多 `16` 步；计时使用完整 scaled delta。2x 不重复倍乘。
- P11 不实现 Customer queue、Employee tasks、P9 Order、P10 capacity、经济、Save、多楼层、NavMeshLink 或新动画。
- 复用现有 assemblies，不新建通用 framework；`IGameTimeService`、Layout/Anchor 的既有 domain 规则不变。
- 一个 Unity test/build process；Task focused + 直接回归，Phase 末集中完整 regression、Engineering/QA review 和 Owner manual。
- 保留用户其他文件。最初审批 plan 不包含 Git 交付权限；Owner 后续已另行授权 review 后提交、push 现有分支和创建 PR。main merge、删除 branch/worktree 与 P12 实施不在该授权内，实际交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。

### 实施接口补充（2026-09-29）

- `NavigationObservation.ResolvedTargetPosition` 为可选只读字段：driver 提供已经过区域、同侧和高度验证的 sampled 目标；null 表示原目标。service 检查有限值、0.15 m sampling 范围和 AllowedRegion 后，用该目标判定 0.08 m 到达距离。这样允许合法 sampling，又不放宽到达条件。

## Review Focus

1. 晚到的 path/rebuild 结果不能应用到新 request/revision：Task 1/3/5，E-015、P-022。
2. 两只角色同一帧交换位置，终点看似合法但中途互穿：Task 3，E-017、P-004/009。
3. 退出装修的 Pause handle 释放顺序，以及多 owner block 交错不能造成一帧恢复：Task 6，I-006/008/013。
4. 同名 walk 的 fps、Action 末帧与 scene 范围不同，不能截断循环或重复倍速：Task 2/4，P-010/011/015。
5. 把非必需 station 的失败误当全店失败、或忽略服务间断路：Task 5/6，I-012。

## 文件结构与职责

```text
Assets/Scripts/Navigation/
  NavigationContracts.cs          请求、结果、Target、Observation、driver 接口
  NavigationSettings.cs           统一可序列化参数和 Validate
  NavigationService.cs            请求/计时/retry/recovery/callback
  NavigationActor.cs              actor ID、Agent、proxy、模型引用
  NavMeshMovementDriver.cs        NavMesh path 与实际状态适配
  NavigationCollisionGuard.cs     静态 sweep、动态角色互斥
  NavigationWorld.cs              注册、稳定更新顺序、唯一位移写入
  NavigationWalkPresenter.cs      由实际位移驱动 walk/静止姿势
  NavigationLayoutAdapter.cs      正式布局快照、bake、readiness
  NavigationDecorationBridge.cs   Confirm/exit guard 与时间恢复接线
  NavigationValidationController.cs 验证场景情形、按钮和结果显示
Assets/Editor/Phase11/
  Phase11CharacterBuilder.cs      导出入口、导入设置、Prefab/Animator
  Phase11NavigationSceneSetup.cs  验证场景生成及 MainCafe 最小接线
  Phase11NavigationValidator.cs  验证 asset、配置、场景和源引用
ArtSource/Phase11/Tools/ExportNavigationCharacters.py
Assets/Art/Phase11/Characters/{Shiba,Westie}/
  SM_{Name}_Walk_Default.fbx
  T_{Name}_BaseColor.png
  M_{Name}.mat
  AC_{Name}_Navigation.controller
  PF_{Name}_Navigation.prefab
Assets/Scenes/Validation/Phase11Navigation.unity
Assets/Tests/EditMode/Phase11/
  NavigationServiceTests.cs       lifecycle、timeouts、retry、reentry
  NavigationAssetTests.cs         source/import/configuration
  NavigationCollisionTests.cs    几何互斥、数据边界
  NavigationLayoutTests.cs        snapshot/revision/readiness
  NavigationResumeBlockTests.cs  time gate 生命周期
  NavigationTestSupport.cs       明确时钟、fake driver、结果捕获
Assets/Tests/PlayMode/Phase11/
  NavigationMovementTests.cs     实际 NavMesh 与长帧移动
  NavigationAnimationTests.cs    两只模型、walk、暂停倍速
Assets/Tests/PlayMode/EditorSceneLoading/
  Phase11NavigationSceneTests.cs  专用 scene + 装修 + MainCafe 回归
Docs/Phase11_Beginner_Guide.md    实施时生成操作与证据说明
```

**修改现有文件（按 Task 限制）：**

- `Assets/Scripts/Decoration/DecorationModeController.cs`：在现有 Confirm / Store / Exit 流程添加小型 bridge 调用，不重构整份文件。
- `Assets/Scripts/Core/Time/GameTimeService.cs`：可释放 resume block；`Assets/Scripts/UI/TimeControlPanel.cs`：禁用恢复按钮与原因反馈。
- `Assets/Scenes/MainCafe.unity`：最终加入默认不启用营业 gate 的 Navigation 接线，不生成 NPC。
- `Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md`：Phase 收尾时合并已确认规则、如实更新状态；先读取用户现有改动。
- 不修改 `CafeLayoutRuntime.cs`、`IGameTimeService.cs`、旧 Anchor resolver 或 asmdef；本方案通过其已有查询与 UnityEngine.AI 工作。如实现发现确需扩大范围，先记录具体原因与回归范围。

Unity 自动生成 `.meta`；scene/Prefab/asset 通过 Unity Editor API/CLI 生成，不手写 YAML。不将 `.blend` 放进 Assets 触发隐式导入。

## 实施前检查与测试命令

- [x] Owner 已批准实施，并明确选择 subagent；按依赖顺序开发，Phase 末集中完整回归与 review。
- [x] 按 Owner 最新指示继续使用 `.worktrees/phase-9-order-domain`，branch 为 `codex/phase-9-order-domain`，基于已完成的 P10；不创建新 worktree。
- [x] 已核对基线 `a0877da`、status 和 Unity `6000.5.5f1`，三份文档已带入该 checkout；保留既有 outputs 与根项目改动，不自动 merge。
- [x] 实施前两个 `.blend` 均从根项目源目录只读加载，当时没有复制或改写母文件。2026-09-30 Owner 另行授权的 walk 母版试调见 Task 7 状态。
- [x] 已核对现有 Unity 环境；通过 Unity CLI 串行运行 tests，未安装 Pipeline，未关闭用户场景。

CLI 参数已通过本机 `unity test --help` 核对；实施证据见各 Task 报告与 `outputs/phase11`。命令在获批 checkout 根目录运行：

```powershell
New-Item -ItemType Directory -Force outputs/phase11 | Out-Null
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase11 --output outputs/phase11/focused-edit.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase11 --output outputs/phase11/focused-play.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase11Integration --output outputs/phase11/integration.xml --timeout 600
```

每 Task 将 filter 缩小到下文 fixture；输出分别用 `taskN-red.xml` / `taskN-green.xml`。RED 必须来自缺失行为；必要的最小编译 stub 允许，编译失败/许可证失败不冒充 RED。GREEN 必须检查 XML 中确有预期 test fullnames、失败数 0、所有必要用例实际执行；空报告、skip 或 runner error 不算通过。

## Task 1 — 请求状态与有限恢复

**Create:** `NavigationContracts.cs`、`NavigationSettings.cs`、`NavigationService.cs`、`NavigationServiceTests.cs`、`NavigationTestSupport.cs`。

**Consumes:** C#、Unity Vector3/Bounds；无 Scene、P9/P10 或静态 event bus。

**Produces（namespace `AnimalCafe.Navigation`）：**

```csharp
NavigationTarget(Vector3 position, Vector3? facing = null, Bounds? allowedRegion = null)
NavigationService(NavigationSettings settings)
bool Register(string actorId, INavigationDriver driver)
void Unregister(string actorId)
MoveStartResult MoveTo(string actorId, NavigationTarget target,
    Action<MovementResult> completed, NavigationTarget? recoveryTarget = null)
bool Cancel(long requestId)
void Tick(float scaledDeltaTime)
void InvalidateLayout(int revision)
void Shutdown()
// INavigationDriver:
Vector3 Position { get; }
NavigationFailure BeginPath(NavigationTarget target, int generation, int layoutRevision)
NavigationObservation Observe()
void Stop()
```

`MovementStatus = Arrived, Recovered, Failed, Cancelled, LayoutChanged`；`NavigationFailure = None, InvalidArgument, UnknownActor, Busy, InvalidStart, InvalidTarget, IncompletePath, PathTimeout, MovementTimeout, RecoveryFailed, ActorUnavailable, LayoutUnavailable`。`MoveStartResult` 为 get-only 的 Accepted/RequestId/Reason；拒绝时 ID=0、不回调。成功接受的 ID 为单 service 正 long 递增，到上限后拒绝，不回绕。

`MovementResult` 字段逐项见 spec 第 6 节；三个 failure 字段使用 NavigationFailure。`NavigationObservation` 是 get-only snapshot：Generation、LayoutRevision、PathState（Pending/Complete/Invalid）、Position、PathLength、RemainingDistance、ActualSpeed、FacingErrorDegrees、Failure。不提供任意 setter 改请求状态；无 Facing 时角差为 0。

`void NavigationSettings.Validate()` 对上述所有参数执行有限值/正值/范围检查；NavigationTarget 拒绝非有限 Position、零长度/非水平 Facing 和无法容纳目标的 AllowedRegion。Register 对重复 ID、空 ID/null driver 返回 false，不替换原 actor；Unregister 结束其活动请求一次，Reason=ActorUnavailable。

- [x] 写 `RetriesOnceFromLatestPosition`、`PauseDoesNotConsumeTimeout`、`RecoveryIsNotArrival`、`RejectsBusyWithoutReplacing`、`TerminalCallbackIsOnce`、`OldGenerationIsIgnored`，覆盖 E-001…011/013…015。fake driver 可设置上述 Observation，记录 BeginPath 入参及调用时真实 Position；不自行实现 production timeout。
- [x] 建立可编译 stub 后运行 `AnimalCafe.Tests.EditMode.Phase11.NavigationServiceTests`，确认 retry/终态行为失败。
- [x] 实现单 actor 单请求、pending→moving→retry→optional recovery→terminal；初始目标无效直接失败，恢复段无 retry。每次开始段重置该段时限，原目标重试额度和 request ID 不变；错误 generation/revision 丢弃。先删除活动请求再调用回调；回调异常记录一次但不中断其他请求；service shutdown 拒绝新请求。
- [x] 加入具体边界断言：首次实际移动后重算起点等于 fake 当前 Position；再次卡住且无 recovery 时 `Status == Failed`；配置可达 recovery 后 `Status == Recovered` 且 `OriginalFailure != None`；`callbackCount == 1`；重新进入回调创建新请求不被旧 Cancel 影响。

恢复成功 fixture 的关键断言（result 为完成回调捕获值）：

```csharp
Assert.That(result.Status, Is.EqualTo(MovementStatus.Recovered));
Assert.That(result.OriginalFailure, Is.Not.EqualTo(NavigationFailure.None));
Assert.That(callbackCount, Is.EqualTo(1));
```

- [x] 对 E-010 写 `OscillationCannotExtendSegmentDeadline`，对 E-011 写 `LongFrameCountsFullTimeout`；按 spec 使用 3 s、0.02 m、总时限和完整 delta，非有限/负 delta 拒绝且无副作用。
- [x] 同一 fixture GREEN，检查所有结果；记录 RED/GREEN 摘要。此 Task 不跑完整历史 regression。

## Task 2 — 现有 Shiba / Westie 最小导入

**Create:** `ExportNavigationCharacters.py`、`Phase11CharacterBuilder.cs`、`Phase11NavigationValidator.cs`、`NavigationActor.cs`、`NavigationAssetTests.cs`；两套 Character 目录中的五类资源。

**Consumes:** Task 1 settings；已确认源文件和 Action 1–21，Shiba 30 fps、Westie 24 fps。

**Produces:** `NavigationActor.ActorId`、`Agent`（NavMeshAgent）、`Proxy`（CapsuleCollider）、`Settings`、`ModelAnimator`、`ModelRoot` 只读引用；`void Phase11CharacterBuilder.Build(string sourceProjectRoot)`；`Phase11NavigationValidator.ValidateCharacters()` 返回 `IReadOnlyList<string>`（空=未发现问题）。

- [x] 写 `CharacterUsesOriginalScaleAndWalk`、`NoRootMotionOrAutomaticAgentTransform`、`BothClipsIncludeActionLastFrame`，检查 Prefab root scale=1、约 1.30 m、半径=0.45、高=1.30、`applyRootMotion=false`、Agent updatePosition/updateRotation/autoRepath/autoTraverseOffMeshLink 均 false。

```csharp
Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
Assert.That(actor.Proxy.radius, Is.EqualTo(0.45f).Within(0.001f));
Assert.That(actor.Proxy.height, Is.EqualTo(1.30f).Within(0.001f));
Assert.That(actor.Agent.updatePosition, Is.False);
Assert.That(actor.ModelAnimator.applyRootMotion, Is.False);
```

- [x] 运行 `AnimalCafe.Tests.EditMode.Phase11.NavigationAssetTests`，缺失导入资源应为明确断言失败；不是直接调用缺失路径导致 fixture 崩溃。
- [x] 编写导出脚本参数 `--source-root` 与 `--output-root`；只读打开两份源文件，导出仅 Mesh/Armature 与当前默认 walk，抽取 packed texture；不 save `.blend`、不带帽子/围裙 working files。导出落到目标 checkout。
- [x] Builder 导入 Generic Rig、单位/轴向正确、Root Motion 禁用、walk loop；创建 URP Material 和双状态 Animator（Walk、从 walk 抽取的 Hold pose，Hold 作为 controller subasset 保存）。不切换到 Hurry，不额外启用其他动作。
- [x] 校准 model child 的脚底/pivot，不移动运行中的 actor；创建统一 capsule 与 NavMeshAgent 配置，记录 clip duration 和导出设置。程序化验证 height、scale、材质引用和循环边界；不以自动检查代替 Owner 外观验收。
- [x] 同一 fixture GREEN；仅回归现有 asset validator 的直接共享路径（若没有改共享 importer，则不跑旧全量）。源文件重新读 hash 或时间/尺寸，确认未保存母版。

## Task 3 — NavMesh adapter 与移动保护

**Create:** `NavMeshMovementDriver.cs`、`NavigationCollisionGuard.cs`、`NavigationWorld.cs`、`NavigationCollisionTests.cs`、`NavigationMovementTests.cs`。

**Consumes:** Task 1 service/driver/observation，Task 2 actor 配置。

**Produces:** `NavigationWorld.Service`（NavigationService）、`bool Register(NavigationActor actor)`、`void Unregister(NavigationActor actor)`、`void Step(float scaledDeltaTime)`、`void SetGeometry(IReadOnlyList<Collider> solids, int revision)`；`NavMeshMovementDriver(NavigationActor actor)` 实现 INavigationDriver，并提供 `Vector3 DesiredVelocity` 与 `void AcceptPose(Vector3 position, Quaternion rotation, float actualSpeed)`；guard 提供 `Vector3 ClampDisplacement(NavigationActor actor, Vector3 proposedDisplacement, IReadOnlyList<NavigationActor> actors, IReadOnlyList<Collider> solids)`，每 substep 的已批准扫掠由 world 持有并传入 guard 的内部上下文。

- [x] 写 `CrossingSweepsCannotExchangeSides`、`StationaryActorKeepsItsSpace`、`QueryOverflowStopsSafely`、`LongFrameCannotTunnel`，覆盖 E-012/016/017/020。用独立线段最近距离检查两个圆形 proxy 的扫掠，不只断言 guard 返回 true。
- [x] 运行 `AnimalCafe.Tests.EditMode.Phase11.NavigationCollisionTests`，确认行为 RED；随后添加 PlayMode `CompletePathArrives`、`PendingOrPartialNeverArrives`、`OneMetreCorridorPasses`、`Point85CorridorFails`、`SameSideSamplingOnly`（P-001…009/012/018/020/023）。
- [x] Driver 首先验证真实起点、有限目标、完整 path；sampling 只移动目标、不改 actor。验证目标与采样点间没有墙/柜台遮挡、同高度层、满足 AllowedRegion；禁用 OffMeshLink。一次 BeginPath 对应 generation，pathPending 由 service 超时处理。
- [x] 新 actor 先验证 authored 起点再启用 Agent；测试 Agent 初次绑定/重建后启用也不自动 snap Transform。无效起点明确拒绝，不利用组件 Enable/Disable 隐式修复位置。
- [x] World 在唯一 Update 中取完整 scaled delta 给 service 计时，运动预算按 spec 限步；按稳定 ActorId 排序提交 proxy sweep。宽路让 NavMesh avoidance 提供绕行方向；受阻停下，有空间才走，不能用推开角色修复碰撞。
- [x] 静态起点 overlap / sweep / 终点检查只命中登记 solids；动态 actors 用当前真实位置与本步 sweep。忽略 self/trigger；非分配 query 缓冲溢出时扩大受限缓冲或停止，绝不悄悄漏掉 hit。失败 actor 留在注册表中占位直到实际移除。
- [x] 应用仅被批准的有限位移与正常转向，然后同步 Agent.nextPosition；位置自动同步始终关闭。ActualSpeed 从批准位移/运动时间取得，不直接用 Agent.desiredVelocity。停止和平滑转向后按 spec 的位置/remaining/速度/5°全部条件判断到达。
- [x] 运行 EditMode fixture 和 `AnimalCafe.Tests.PlayMode.Phase11.NavigationMovementTests` 至 GREEN；必须有宽路双人到达正例和窄路安全失败反例，不能以全部停住充当避让成功。

对照通道测试分别创建 1.00 m / 0.85 m fixture；result 为各自完成值：

```csharp
Assert.That(oneMetreResult.Status, Is.EqualTo(MovementStatus.Arrived));
Assert.That(narrowResult.Status, Is.EqualTo(MovementStatus.Failed));
Assert.That(minimumSweptSeparation, Is.GreaterThanOrEqualTo(0.9f - 0.001f));
```

最后一项是双 actor 情形下按独立几何计算得出的连续最短距离，不应用到单人通道 fixture。

## Task 4 — walk、暂停倍速与可操作验证场景

**Create:** `NavigationWalkPresenter.cs`、`NavigationValidationController.cs`、`Phase11NavigationSceneSetup.cs`、验证 scene、`NavigationAnimationTests.cs`、`Phase11NavigationSceneTests.cs`。

**Consumes:** Task 2 Prefabs、Task 3 World.Service/actor 实际位移、既有 GameTimeService。

**Produces:** `void NavigationWalkPresenter.SetMotion(float actualSpeed)`；`void Phase11NavigationSceneSetup.CreateValidationScene()`；`void NavigationValidationController.RunScenario(string scenarioId)` 和 `void CancelAll()`，只控制验证场景的请求，不作为正式玩家 click-to-move 系统。

- [x] 写 `WalkStopsWhenGuardBlocks`、`PauseFreezesPoseAndTimeout`、`FastIsTwiceNotFourTimes`；运行 `AnimalCafe.Tests.PlayMode.Phase11.NavigationAnimationTests` 观察 RED。
- [x] Presenter 以 actualSpeed 驱动 Walk/Hold，不因 Agent 想走就原地踏步；采用正常 scaled Animator，速度校准按各自 clip 时长，不再乘 timeScale。到达/等待后的 Hold 为稳定姿势，不 freeze 在任意单脚抬起帧。
- [x] Scene builder 使用现有 scene setup 的保存保护方式：用户 dirty scene 不被覆盖；创建两只真实角色、1×1/1×3 Counter、墙、1.00/0.85 m 对照通道、GameTimeService、简单 TMP/Button 控制面板。用 NavMeshBuilder 从明确 floor/solid sources 构建 fixture 导航，P11 不新建 UI 系统。
- [x] 提供 scenario IDs：`straight`、`detour`、`crossing`、`narrow-wait`、`same-target`、`recovery`、`crowd8`；面板显示 actor、request、运行中/完成状态，以及完成结果中的 RetryCount 和失败原因。只有退出 Play 后重建 fixture 可重新放置角色；运行中不以 Reset 瞬移现有角色。
- [x] 用 `Phase11Integration` namespace 的 scene tests 验证资源完整、按钮对应真实请求；P-010/011/014…017/019 全部有测试，预期约半时长容差由同路线起停误差给出，不能固定等待后仅看动画。

```csharp
// normalSeconds/fastSeconds 为同一路径、实际回调到达的 wall-clock 时间。
Assert.That(fastSeconds / normalSeconds, Is.InRange(0.4f, 0.6f));
Assert.That(Vector3.Distance(beforePause, afterPause), Is.LessThanOrEqualTo(0.001f));
```

用足够长的直线路径减少起停占比；Pause 用 unscaled 等待观测，不能用 scaled WaitForSeconds 导致测试自身挂起。
- [x] 两个 PlayMode fixture GREEN，捕获少量实际 Game View 画面给 Owner 后续 M-001…007/011；当前自动结果不能标记人工验收 PASS。

## Task 5 — 正式布局快照、NavMesh rebuild 与 Confirm guard

**Create:** `NavigationLayoutAdapter.cs`、`NavigationDecorationBridge.cs`、`NavigationLayoutTests.cs`；**Modify:** `DecorationModeController.cs`（Confirm/Store 成功通知、确认前 guard）。

**Consumes:** CafeLayoutRuntime.Layout/FunctionalSurfaceLayout/CurrentReadiness、FurnitureSceneRegistry 的实际确认视图、DecorationGridSpace、Task 3 World。

**Produces:**

```csharp
// NavigationLayoutAdapter:
int Revision { get; }
NavigationReadiness CurrentReadiness { get; }
bool RefreshConfirmedLayout()
NavigationReadiness RebuildAndValidate()
// NavigationDecorationBridge:
bool CanConfirm(IReadOnlyList<NavigationSolidPose> candidateSolids, out string reason)
void ConfirmedLayoutChanged()
```

同文件定义公开 readonly `NavigationSolidPose`（world Position/Rotation/Size、家具 ID，对复杂 Collider 用保守 bounds）和公开只读 `NavigationReadiness`（CanResume、StationFailures、Reason）。adapter 配置用序列化的 layoutRuntime、gridRoot、floor、walls、confirmed furniture roots、world；不能全场景 `FindObjectsOfType<Collider>` 直接作为最终 sources。

- [x] 写 `PreviewDoesNotChangeRevision`、`AppearanceDoesNotRebuild`、`ConfirmedPoseInvalidatesRequests`、`ActorOverlapRejectsBeforeCommit`、`ObsoleteBakeCannotPublish`；运行 `AnimalCafe.Tests.EditMode.Phase11.NavigationLayoutTests` 确认 RED。
- [x] 从确认后的 ID/definition/pose/几何/Anchor 生成可比较 snapshot；仅真实变化递增 revision，调用 service.InvalidateLayout。明确 sources 为地面、墙、实体家具；排除 NPC/Preview/外观层，独立持有本 world 的 NavMeshDataInstance。
- [x] RebuildAndValidate 在暂停中构建/替换自己持有的数据，验证真实起点、入口到必要服务组合、员工工作站到 Pick-up 等服务间路径；同角色半径。复用 P8 的“至少存在必要可用组合”与非必需 station 局部降级，不要求每个 station 都可用。失败保留具体 station ID/reason。
- [x] 所有实体 Confirm 入口在 mutation 前调用 CanConfirm：floor、rotation、mounted equipment、实体 wall decoration；用候选正式 collider 的 world pose，不让 Preview 的 collider 参与自碰撞。对 Store 不做新增重叠拒绝，但成功移除要触发失效；移动承载家具时包含其附属实体。
- [x] controller 仅添加 bridge 引用、调用与既有反馈，不改 domain transaction。拒绝文案为“这里有角色，请换个位置。”，保留 Preview 与正式布局；不先提交再回滚。
- [x] GREEN 覆盖 E-018/019、I-002/003/004/009/011/012、P-021/022；直接回归 `AnimalCafe.Tests.EditMode.Phase8` 及相关 P8R furniture tests。添加非必需 station 被堵仍允许必要组合、关键服务间断路阻止恢复的两个反例。

```csharp
Assert.That(optionalStationBlocked.CanResume, Is.True);
Assert.That(essentialConnectionBlocked.CanResume, Is.False);
Assert.That(layoutAfterRejectedConfirm, Is.EqualTo(layoutBeforeConfirm));
```

最后一项使用测试自己捕获的 ID/pose 数据 snapshot，不比较同一可变 Layout 引用。

## Task 6 — 装修退出与营业恢复限制

**Modify:** `GameTimeService.cs`、`TimeControlPanel.cs`、`NavigationDecorationBridge.cs`、`DecorationModeController.cs`、`Phase11NavigationSceneSetup.cs`、`Phase11NavigationSceneTests.cs`；**Create:** `NavigationResumeBlockTests.cs`；**Scene:** 验证 scene 完整启用，MainCafe 仅准备接线。

**Consumes:** Task 5 RebuildAndValidate、既有 UiPauseCoordinator（不改 IGameTimeService）。

**Produces:** `GameTimeService.AcquireResumeBlock(object owner, string reason): IDisposable`、`IsResumeBlocked: bool`、`ResumeBlockReason: string`、`event Action ResumeAvailabilityChanged`；bridge `PrepareExit(): bool`、`ResumeAfterRepair(): bool`、序列化 `enforceBusinessReadiness`。

- [x] 写 `BlockedTrySetSpeedCannotResume`、`OneOwnerCannotReleaseAnother`、`ReleaseDoesNotAutoRun`、`DecorationReleaseCannotRunOneFrame`；运行 `AnimalCafe.Tests.EditMode.Phase11.NavigationResumeBlockTests` 确认 RED。
- [x] GameTimeService 每个 handle 独立且 Dispose 幂等，获得 block 时先进入 Pause；所有非 Paused TrySetSpeed 在写 timeScale 前检查 block，返回 false 且不改 lastRunningSpeed。最后一个 block 释放仅通知可恢复，不自动设 1x/2x；保持 `IGameTimeService` 原接口与旧场景零 block 行为。

```csharp
using (time.AcquireResumeBlock(owner, "路径被挡住"))
{
    Assert.That(time.TrySetSpeed(GameSpeed.Fast), Is.False);
    Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
}
Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
```

- [x] Bridge 在释放装修 pause handle 之前持有 Navigation block 并 PrepareExit；处理 pending preview/cancel 后构建最终布局。成功先解除自己的 block，让旧 UiPauseCoordinator 恢复原速度；失败保持暂停但允许退出编辑，文案“路径被挡住了，请进入装修调整。”，时间按钮显示阻挡原因。
- [x] 后续修复成功 ResumeAfterRepair 使用最近一次有效的装修前运行速度；不把因 Navigation block 造成的 Pause 覆盖为用户原速度。显式用户 Pause 则仍保留 Pause；另一个 block 存在则返回 false。已失效的旧 pending restore 不能随后覆盖用户新选择的速度。
- [x] 配置失效/bridge disable 时先停止 world/service，再释放自己的资源；营业场景不能因 bridge 消失自动变 ready，保留由仍存活 world 持有的阻挡直到重新验证。scene 卸载完整清理，不遗留 static owner；测试关闭 Domain Reload 的连续 Play。
- [x] 验证 scene `enforceBusinessReadiness=true`；MainCafe 本阶段无营业 NPC，明确设 false、零注册角色，只准备接线，不把缺少营业设备导致的暂停回归引入现有装修演示。P12–14 正式营业接入时必须显式开启，不依赖隐含默认。
- [x] 在 `AnimalCafe.Tests.PlayMode.Phase11Integration` 运行 I-001/005…008/010/013、P-022；直接回归 `AnimalCafe.Tests.Phase5.UiPauseCoordinatorTests`、`AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RTwoButtonTimeControlTests`、`AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RFurnitureFlowTests`。GREEN 要证明 block 多 owner、重入退出、原 2x、用户 Pause、修复恢复和 MainCafe 旧行为。

## Task 7 — 集中验收与文档收尾

此前自动验证与决策记录：[Phase11_Validation_Report.md](../../Phase11_Validation_Report.md)。Owner 操作：[Phase11_Beginner_Guide.md](../../Phase11_Beginner_Guide.md)。Task 7 技术自动收尾与集中 review 已完成；Owner 确认 **M-001～M-011 全部 PASS**。Phase 11 在现有 P9 分支完成开发与验收；Owner 已授权 review 后 push 和创建 PR，最新 P9–P11 review、完整回归与交付状态统一见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。尚未 merge main，P12 未开始。

以下保留此前局部修复证据，不覆盖最新完整回归。先前截图失败、GUI 拖拽未可靠完成的代理尝试均保留为历史，不覆盖 Owner 判断。2026-09-30 Owner 授权两只原版 `.blend` walk 幅度试调 1.5；备份位于 `outputs/phase11/walk-amplitude-20260930/source-backup/`，只改 8 条上下肢旋转 curve，frame 1～21 / FPS 不变，模型尺寸与行走速度目标不变。幅度试调 focused EditMode 8 PASS / PlayMode 8 PASS，均 0 FAIL / 0 SKIP；之后碰撞修复 focused Edit 14 PASS、直接 Play 66 PASS / 0 FAIL / 1 opt-in SKIP；预览/teardown focused Play 14 PASS / 0 FAIL / 0 SKIP。这些局部修复当时未重跑完整 suite，原始证据见验证报告。

**Create:** `Docs/Phase11_Beginner_Guide.md`；**Update:** 本 plan/test cases、Game Design、Roadmap。开发与人工验收已完成；Roadmap 同步和 Git 收尾分别按当前进度记录。

**Consumes:** Task 1–6 的 XML/log、真实验证 scene、Owner 原尺寸与轻微穿插偏好。

- [x] 检查代码与资源无自动 Warp/位置纠正、依赖升级、源模型重写、P9/P10 写入或正式营业 NPC；运行 Phase11NavigationValidator，修复所有阻断项。Task 7 focused validator 无 issue，空 Animator layers 返回 issue 不抛异常；完整结果见报告。
- [x] 此前串行执行完整 regression：Edit 原始 2432 PASS / 1 FAIL，assembly 唯一失败由批准的 1 PASS 边界补测及 25 PASS moved fixtures 解决；该次最终完整 Play 1102 PASS / 0 FAIL / 5 SKIP；指定 Integration 27 PASS / 0 FAIL / 1 SKIP。原始失败 XML 不改写为全绿；最新合并前完整回归见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。

```powershell
unity test . --mode EditMode --output outputs/phase11/final-edit.xml --timeout 5400
unity test . --mode PlayMode --output outputs/phase11/final-play.xml --timeout 5400
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase11Integration --output outputs/phase11/final-integration.xml --timeout 900
```

- [x] 核对 XML/log 的 test fullnames、counts、fail/skip；完整五项 opt-in SKIP 原因和历史失败记录见报告，全量明细见 `outputs/phase11/final-results-summary.json`。完整 tests 不因 docs-only 改动重跑。
- [x] controller 组织集中 Engineering、QA、Art/UX review 及针对性修复复核。无开放 Critical/Important；两个 Art Minor 保留。完整回归发现的 test assembly / scene input cache 问题已修复；两组顺序各 3 PASS、联合 92 PASS / 1 SKIP，最后完整 Play 解决原 25 个输入失败。
- [x] Beginner Guide 提供场景路径、scenario 按钮、预期状态、Pause/2x、装修堵路/修复步骤、M-001…011 检查表和 Console 位置。区分自动 PASS 与 Owner 验收；只要求轻微短暂边缘穿插可接受，不恢复旧零穿模指标。M-011 的 Test Runner 入口保留固定模拟帧、自动卸载的受限观察说明；Owner 已确认 M-011 PASS。
- [x] 按 Owner 截图反馈修复 M-003 的 InvalidStart 与 M-005 A 的柜台绕行超时：实际提交终点检查、受限 Box 零距离接触的连续安全证明；focused Edit 14 PASS、直接 Play 66 PASS / 0 FAIL / 1 opt-in SKIP。原 RED 和修复证据见 `outputs/phase11/manual-navigation-fix-20260930/`，不改模型、速度、半径、skin 或 retry。
- [x] M-008 原有拒绝/Cancel 功能与新增红色 footprint 观感获 Owner PASS；红色预览与 teardown 经真实 RED/GREEN、最终 14/14 PlayMode 验证，独立 review Approved。M-009 真实 Confirm 堵路/修复逻辑由 Codex 自动 PASS，后由 Owner 明确确认 PASS。M-010 Owner PASS；先前 GUI 拖拽尝试未可靠完成的记录仍保留在验证报告。
- [x] Owner 最新澄清 M-001～M-010 均已通过人工测试；不再要求重测这些项目。
- [x] M-011 保留原尺寸时观察 1×1 / 1×3 Counter 前的实际步行、停步与转向，Owner 确认 PASS；Test Runner 固定模拟帧和自动卸载限制仍保留为观察边界，绿色断言不代替 Owner 反馈。
- [x] Roadmap 的 P11 状态已同步为开发与验收完成，未宣称 merged-main 或发布。
- [x] Owner 已另行授权 review 后提交、push 现有分支并创建 PR；实际交付状态统一记录于 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)，不以此授权记录代替推送成功证据。main merge 留待 Owner 决定，P12 未开始；保留用户原文件改动。
- [x] 文件清单、RED/GREEN、准确 PASS/FAIL/SKIP/NOT_RUN、截图/字体/手动限制已写入报告与指南；此前阶段记录继续保留，最新 P9–P11 回归与 PR 交付统一指向合并前 Review。

## 覆盖与审核记录

| Spec 范围 | 实施 Task | 主要验证 |
|---|---|---|
| 请求/一次 retry/回调 | 1 | E-001…015 |
| 原模型/完整 walk/比例 | 2、4 | R、P-014…016、M-001/002 |
| 采样/完整 path/连续安全/避让 | 3 | E-016/017/020，P-001…009/012/018/020/023 |
| Pause/2x/长帧/动画 | 1、3、4 | E-011/012、P-009…011/014 |
| walk-only recovery | 1、3、4 | E-008/009、P-013、M-007 |
| Layout invalidation/候选摆放/构建 | 5 | E-018/019、P-021/022、I-002…004/009/011/012 |
| 退出/恢复/多 owner/旧场景 | 6 | I-001/005…008/010/013 |
| Owner 轻微穿插验收与收尾 | 7 | M-001…011、完整 regression |

文档自检重点：接口名称在 Tasks 间一致；所有高风险输入有归属；旧 0.56 m 和固定站位偏移仅作为历史解释，不是测试要求。实施中的完成步骤以 checkbox 与对应测试证据为准；沿用 Owner 指定的 P9 worktree，Phase 关闭仍需完整回归和 Owner 手动验收。
