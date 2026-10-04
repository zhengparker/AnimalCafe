# Phase 11 自动验证与实现决策

**状态（2026-09-30）：Phase 11 开发与验收完成（现有 P9 分支），M-001～M-011 全部 Owner PASS。** Owner 已补充确认“11也完成了”，柜台站位与转向的最后一项手动验收关闭。Owner 已授权 review 后 push 和创建 PR，尚未 merge main，P12 未开始。最新 P9–P11 review、完整回归、修复与交付状态统一见 [合并前 Review](Phase9_Phase11_Merge_Review.md)；本文保留此前各次验证的原始结果与实现决策。

工作目录为 `E:/Unity/Project/AnimalCafe/.worktrees/phase-9-order-domain`，P11 开发基线为 `a0877da`，既有分支为 `codex/phase-9-order-domain`。[操作指南](Phase11_Beginner_Guide.md)保留场景操作和已完成的 Owner 验收记录，无需重复手测。

## 1. 此前自动验证记录

试调前的最终完整 PlayMode 为 **1102 PASS / 0 FAIL / 5 SKIP**，指定 Integration 为 **27 PASS / 0 FAIL / 1 SKIP**。完整 EditMode 原始 **2432 PASS / 1 FAIL**，唯一失败经批准的精确补测解决；下面保留各次原始结果，不叠加重复测试计数，也不把它们作为 1.5 幅度新版本的结果。

| 验证 | 结果 | 原始证据 |
|---|---|---|
| Task 7 Edit focused：service、字符资源、validator | 32 PASS / 0 FAIL / 0 SKIP | `outputs/phase11/task7-polish-green-edit.xml` |
| Task 7 Play focused：动画、真实 P11 场景 | 29 PASS / 0 FAIL / 1 SKIP | `outputs/phase11/task7-polish-green-play.xml` |
| 修复前有界 Phase11Integration（开启截图） | 25 PASS / 1 FAIL / 0 SKIP；唯一失败为 batch ScreenCapture 未产生文件 | `outputs/phase11/task7-review-integration.xml` |
| 同一截图用例改用现有非 batch Editor | 1 PASS / 0 FAIL / 0 SKIP；实际 Canvas+Camera 两张图 | `outputs/phase11/task7-native-capture.xml` |
| 完整 EditMode 首次尝试（修复前） | TIMEOUT：1800 s，无完整/partial XML；不能计 PASS / FAIL | `outputs/phase11/task7-first-edit-timeout.log` |
| 集中修复 Edit focused | 70 PASS / 0 FAIL / 0 SKIP | `outputs/phase11/final-fix-focused-edit.xml` |
| 集中修复 Play focused | 60 PASS / 0 FAIL / 1 SKIP | `outputs/phase11/final-fix-focused-play.xml` |
| 最后 HUD 修复后直接回归 | 35 PASS / 0 FAIL / 1 SKIP | `outputs/phase11/final-fix-hud-final-play.xml` |
| 最终非 batch Canvas 截图 | 1 PASS / 0 FAIL / 0 SKIP；真实 Counter Confirm blocked | `outputs/phase11/final-fix-native-capture-v2.xml` |
| 最终角色资源 validator | 1 PASS / 0 FAIL / 0 SKIP | `outputs/phase11/final-validator.xml` |
| 修复后完整 EditMode（发现 test assembly 问题） | 2432 PASS / 1 FAIL / 0 SKIP，2433 total；XML 1912.326 s | `outputs/phase11/final-edit.xml`；原样备份 `final-edit-before-assembly-fix.xml` |
| test assembly 归属修复后边界复测 | 1 PASS / 0 FAIL / 0 SKIP | `outputs/phase11/final-assembly-boundary.xml` |
| 移动后两个 Editor-backed fixture | 25 PASS / 0 FAIL / 0 SKIP；Animation 4、Movement 21 | `outputs/phase11/final-moved-fixtures.xml` |
| 完整 PlayMode（发现输入 fixture 顺序问题） | 1077 PASS / 25 FAIL / 5 SKIP，1107 total；XML 435.346 s | `outputs/phase11/final-play-before-input-investigation.xml` |
| 原失败两个 fixture 隔离运行 | 32 PASS / 0 FAIL / 0 SKIP | `outputs/phase11/final-input-isolation.xml` |
| 最小 P11 → Mouse → Touch 顺序复现 | 1 PASS / 2 FAIL；同完整运行中的输入失败 | `outputs/phase11/final-input-order-probe.xml` |
| 输入缓存清理后两组顺序复测 | 两组各 3 PASS / 0 FAIL / 0 SKIP | `final-input-order-green.xml`、`final-layout-input-order-green.xml` |
| 输入修复后联合受影响回归 | 92 PASS / 0 FAIL / 1 SKIP，93 total | `outputs/phase11/final-input-isolation-green.xml` |
| 最终完整 PlayMode | 1102 PASS / 0 FAIL / 5 SKIP，1107 total；XML 393.548 s | `outputs/phase11/final-play.xml` |
| 最终指定 Phase11Integration | 27 PASS / 0 FAIL / 1 SKIP，28 total；XML 22.147 s | `outputs/phase11/final-integration.xml` |

Focused 和指定 Integration 的唯一 SKIP 是 P11 Canvas capture，需要显式 `ANIMALCAFE_P11_CAPTURE=1`，不计作 PASS。早期 batch 截图未产生文件的 FAIL 与后续原生截图 PASS 分别保留，不能把截图环境失败说成导航产品失败。初轮记录见 `outputs/phase11/task7-review-results-summary.json`；最终 XML 的全部 test fullnames、counts、时间和 SKIP/FAIL 原因见 `outputs/phase11/final-results-summary.json`。

最终完整 Play 的五个 SKIP 如下；这次没有启用历史截图/审计开关。P11 原生 v2 的单独 1 PASS 不改变完整运行中的 SKIP。

| 完整 test fullname | 原因 |
|---|---|
| `AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RCashRegisterSideIndicatorTests.CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery` | 需 `ANIMALCAFE_CASH_SIDE_CAPTURE=1` 的原生截图 |
| `AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RCompactChromeTests.SpacingAudit_WhenRequested_ExportGeometry` | opt-in spacing geometry audit |
| `AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RReadinessSafeAreaTests.ReadinessChecklist_CaptureNativeExamplesWhenRequested` | opt-in 原生 Game View 截图 |
| `AnimalCafe.Tests.PlayMode.EditorSceneLoading.P8RUiEnhancementSceneTests.NativeUiExamples_WhenRequested_CaptureActualGameView` | opt-in 实际 Editor Game View 截图 |
| `AnimalCafe.Tests.PlayMode.Phase11Integration.Phase11NavigationSceneTests.CaptureRepresentativeCanvasAndCamera` | 需 `ANIMALCAFE_P11_CAPTURE=1` 与 graphics Editor |

首次 full Edit 在 2026-09-29 Toronto 20:40 开始，30 分钟上限触发后 CLI 退出；21:11 核对 Unity 已无进程，最后日志是历史 Phase 7 资源导入和 Shut down。不能根据活跃日志推测已通过数量。修复后完整 Edit 于 21:54 开始，所有 capture / gallery opt-in flags 均未开启。上表明确区分修复前与修复后的 focused 证据。

完整 Edit 唯一失败是 `AnimalCafe.Tests.EditMode.PlayModeAssemblyBoundaryTests.PlayModeTestAssembly_DoesNotReferenceUnityEditor`：两个使用 AssetDatabase 的 fixture 放在 player-compatible test assembly，使 Editor 编译引用 UnityEditor.CoreModule。经 controller 裁定，只把 NavigationMovementTests / NavigationAnimationTests 及各自 meta 移至既有 `EditorSceneLoading/Phase11/`，四个文件逐字节不变、test fullnames 不变。边界复测 1 PASS，移动后两个 fixture 25 PASS。Engineering 已核对字节与 discovery 并批准这次纯 assembly 归属修复；controller 接受完整 Edit 加精确补测的证据，不重复 37 分钟完整 Edit。**原始完整 Edit 保持 2432 PASS / 1 FAIL，唯一失败已由针对性复测解决；不存在“最终完整 Edit 2433 PASS”的新运行。**

完整 Play 的 25 个旧输入失败在隔离运行中全部通过，但 P11 scene / Layout 各自接 Mouse、Touch 的两个最小顺序都复现失败。新 fixture 未释放共享 UI InputAction 的 cached controls，Layout fixture 还留下 MainCafe 场景。最终修复仅作用于两个测试文件：loaded roots 出现后记录非空 action assets，安全卸载自有场景后调用既有 `Phase8SceneInputTestCleanup.DisposeReleasedAssets`；该 helper 检查 assets 已禁用且无剩余 UI module owner，保留序列化 binding。未改生产输入逻辑，也未跳过或吞掉旧测试。

一次中间尝试继承 InputTestFixture 引入 EnhancedTouch reset 异常，联合结果 54 PASS / 39 FAIL，已撤回并保留 `final-input-isolation-first-fix-failed.xml`。后续修正了 capture 必须在异步场景 roots 加载后的时机；最终两组顺序测试各 3 PASS，联合回归 92 PASS / 1 SKIP。最终完整 Play 按 fullname 核对，原 25 个失败全部 PASS。

## 2. 此前局部修复与 RED → GREEN

**2026-09-30 walk 幅度试调：** Owner 明确授权编辑两只原版 `.blend` walk 母版；保存前备份位于 `outputs/phase11/walk-amplitude-20260930/source-backup/`。新增 `ArtSource/Phase11/Tools/SetWalkLimbAmplitude.py` 只放大 8 条上下肢旋转 curve：上臂范围 16°→24°，大腿 20°→30°，小腿 12°→18°，脚部 12.25°→18.375°。两份源文件保存后重新打开，与备份逐项核对：其余 59 条 curve、Mesh、skin weights、UV、骨架 rest pose、object transforms、packed texture、frame 1～21 和各自 FPS 均不变；见 `source-verification.json`。

既有 exporter 和 Unity builder 已重新生成两份 FBX、Walk/Hold controller 与 prefab。`generated-comparison.json` 确认只有这 6 个生成文件改变，纹理、材质与所有 meta 保持原字节。ModelRoot 沿用既有脚底校准，导航根位置、proxy 尺寸、速度与步频逻辑未改。

| 本次受影响验证 | 结果 | 证据（均在 `outputs/phase11/walk-amplitude-20260930/`） |
|---|---|---|
| 放大前 imported limb 测试 | 0 PASS / 2 个预期 FAIL；旧上臂范围 15.519°，目标 24° | `red.xml` |
| 放大后 NavigationAssetTests | 8 PASS / 0 FAIL / 0 SKIP；幅度、尺寸、材质、时长、loop、Hold 与 validator | `edit-green.xml` |
| 动画与真实场景 focused PlayMode | 8 PASS / 0 FAIL / 0 SKIP；Pause、2x、停步、全周期脚底与 root/proxy、直行、crowd8、原 Counter Anchor | `play.xml` |

源曲线精确放大 1.5 倍；Unity 导入后的骨骼范围检查允许 ±1° 的 FBX 重采样/压缩偏差，远小于新旧幅度差。中间一次测试编译因当前 NUnit 不支持 `Assert.Multiple` 而失败（`edit.log`，未生成 XML）；已改为汇总偏差后统一断言，并获得上述 GREEN。该次试调没有重跑全量 suite。原先只记录 M-001 与原幅度 M-002 PASS；Owner 随后确认 M-001～M-010 全部通过，包含 1.5 幅度版 M-002。后续碰撞修复与人工验收状态见下文，最新完整回归见 [合并前 Review](Phase9_Phase11_Merge_Review.md)。

- 终止失败的 driver.Stop 原来调用两次；现在每次终止调用一次，在 retry / recovery 开始前仍先停止旧路径。RED 为实际 StopCount `2`，期望 `1`。
- 空 Animator layer 原来在 validator 索引越界；现在返回 `Animator needs Walk and Hold` issue。测试临时修改内存中的 controller，finally 恢复 layers 与原 dirty 状态，不保存资源。
- 停用角色后 Presenter 不再对 inactive Animator 调用 Play。初版测试漏匹配小写 `animator`；修正为忽略大小写，并撤回 guard 重新看到实际 warning 导致 RED，再恢复修复得到 GREEN。
- 新 scenario 清空 Results 时同步把 SubmittedCount 置零。RED 为第二个 scenario 计数 `2`，期望 `1`。
- 补充两种原 Walk 的实际最后 imported key 和 loop seam 断言，均在既有资源上直接 PASS，无重新导出或改 clip。

集中 review 后另一次修复处理了 actor disable：先停止 native intent / animation、清空活动请求，再以 ActorUnavailable 终止回调；disabled 角色不能继续移动、转向或重入提交。仍存在的身体保留位置占用，重新启用不会自动挪动角色。真实 Counter Confirm 堵路/修复用例补齐 I-005～007，触发的原 Coffee Anchor 凸角问题用一次有界 native 边界切向投影处理。最后将验证 UI 放到底部，并为顶部时间控件和阻挡提示保留空间。具体 RED/GREEN 与文件清单见 `.superpowers/sdd/2026-09-29-phase-11-navigation/final-fix-report.md`。

RED 证据：`task7-polish-red-edit.xml` 为 5 PASS / 2 FAIL；`task7-polish-red-play.xml` 为 1 PASS / 1 FAIL；修正 warning 断言后的 `task7-animator-red.xml` 为 0 PASS / 1 FAIL。以上预期失败是修复证据，不是最终 regression 状态。

### 2026-09-30 M-003 / M-005 碰撞修复

Owner 授权检查 crowd8 的 `InvalidStart` 和 detour 的 `MovementTimeout`。本次只修改一个运行文件 `NavigationCollisionGuard.cs`，补充两个既有测试文件；模型、原版 1.5 倍 walk、速度、目标、半径 0.45 m、skin 0.01 m、timeout 和 retry 策略不变。

- **crowd8：** 相对位移算式与实际 `start + delta` 的 float32 舍入不同。旧 guard 批准的终点间距为 0.909999847 m，低于要求的 0.909999967 m，导致下一次 retry 被判 `InvalidStart`。现在提交前同时检查实际终点；原有连续相对 sweep 保留。
- **detour：** 真实可变帧率场景复现柜台圆角超时：路径完整、起点检查通过，但注册柜台的 CapsuleCast 返回 distance=0、point=0、normal=-direction，错误地清零切向移动。这与 [Unity 对初始接触查询的说明](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.CapsuleCastAll.html)一致；固定坐标测试也复现了同一命中与零位移。
- **修复边界：** 仅为世界坐标轴对齐、单位缩放的 Box 零距离接触计算真实表面法线。只有起点和实际 float32 终点保持在同一个面/角区域，且 double 计算证明整段都保留完整半径加 skin，才允许该段移动。动态避让缩短位移后再次证明；其余命中、overflow、最终 Overlap 检查仍有效。旋转 Box 或其它形状不使用此特例，继续保守阻挡；本次不宣称解决所有形状的边界停滞。

证据目录：`outputs/phase11/manual-navigation-fix-20260930/`。失败记录保留，不覆盖为绿色结果。

| 验证 | 实际结果 | 原始证据 |
|---|---|---|
| 实际浮点终点回归，修复前 | 0 PASS / 1 FAIL | `collision-red.xml` |
| 15/30 FPS 的 crowd8 与 detour，修复前 | 2 PASS / 2 FAIL；两个 crowd8 均出现 InvalidStart | `scene-red-v2.xml` |
| 实际可变帧率 detour，修复前 | 0 PASS / 1 FAIL；另三次重放 2 PASS / 1 FAIL | `detour-variable.xml`、`detour-variable-replays.xml` |
| 精确柜台角点，修复前 | 0 PASS / 1 FAIL；真实 sentinel 命中，切向位移为零 | `corner-red.xml` |
| 最终 CollisionTests | 14 PASS / 0 FAIL / 0 SKIP；含 inward、第二障碍物、两端安全但中途穿柜台、薄墙和 overflow | `final-edit.xml` |
| 最终直接 PlayMode 回归 | 66 PASS / 0 FAIL / 1 SKIP，67 total；Movement、Animation、P11 scene、Layout integration | `final-play.xml` |

该次修复后的 crowd8 在 15/30 FPS 均为 8 Arrived、retry=0，没有 InvalidStart；detour 在 15/30/144 FPS 与实际可变帧率均为 Arrived、retry=0。原有 Pause、2x、恢复、柜台站位、blocked85、窄路及布局直接回归通过。唯一 SKIP 是 `CaptureRepresentativeCanvasAndCamera` 的 opt-in ScreenCapture，该次未开启截图。独立 Engineering 安全 review 与 QA 复核通过；当时未重跑完整 suite，自动结果不替代 Owner 视觉 PASS。

中间探针发现测试帧数上限先于模拟时间耗尽，现改为 60 s 模拟时间与 120 s 实际时间的双重上限。两次测试代码编译错误及其日志保留：最初的 `Assert.Multiple` 不受当前 NUnit 支持，后一次安全测试多了一个括号；均已修正，未更改 dependency。三个未复现问题的 flat-pose 探针和重复帧率诊断已移出最终测试，历史结果与诊断源码另存于证据目录。

### 2026-09-30 M-008 预览与装修退出清理

Owner 已确认 M-008 原有拒绝 Confirm / Cancel 功能 PASS，并要求角色占用格的 footprint 显示红色。本轮四个 runtime 文件、两个既有测试文件作局部修复：家具、挂载物、墙面预览受阻时显示红色，移开后恢复绿色；缺失候选几何按无效预览处理；退出装修的 teardown 不重新 bake 或留下 `Navigation confirmed solids`，bridge 已销毁时不再抛 `MissingReferenceException`。正常退出仍验证正式布局。M-009 的真实 Counter Confirm 堵路、修复与恢复由 Codex 自动用例通过；当时 Codex GUI 拖拽未可靠完成。Owner 随后反馈“好了8，9也pass”，确认 M-008 新红色 footprint 和 M-009 堵路/修复手动复测 PASS。M-010 的 Owner PASS 保持。

| 本轮证据（`outputs/phase11/preview-validity-20260930/`） | 结果 | 边界 |
|---|---|---|
| `red.log` | Unity 6.5 的 `GetInstanceID` 编译错误，无测试 XML | 仅测试代码问题，不是行为 RED |
| `red-v2.xml` | 1 PASS / 6 FAIL | 四项颜色真实 RED、destroyed bridge 真实 RED；disable 用例第二次选 preview 的 fixture 失败不计行为 RED |
| `red-v3.xml` | 0 PASS / 1 FAIL | 真实 teardown RED：禁用 Decoration 时 BakeCount 1→2 |
| `green.xml` | 72 PASS / 0 FAIL / 1 opt-in ScreenCapture SKIP，73 total | M-008、teardown、M-009 及既有直接回归 |
| `missing-geometry-red.xml` | 0 PASS / 1 FAIL | 缺失候选几何时预览刷新抛异常 |
| `final.xml` | 14 PASS / 0 FAIL / 0 SKIP；CLI exit 0 | 最后局部修复的直接复测；独立 review Approved |

此前这次预览与 teardown 修复未重跑完整 EditMode / PlayMode suite，当时也未 commit、push 或 merge。测试生成的单个旧材质 `M_WallProjection_Invalid.mat` 序列化漂移已备份 after 并精确恢复原字节；settings/packages 无变更。后续合并前 review 与完整回归另见 [合并前 Review](Phase9_Phase11_Merge_Review.md)。

## 3. 范围与限制

- 实际角色 Transform 只由 NavigationWorld 在批准 substep 内按 `start + guarded delta` 写入。Agent.nextPosition 从真实位置同步；起点 sampling 不挪动 actor。没有 Warp、穿障碍后纠正、销毁重生恢复。
- 沿用现有 dependency、assembly、原 `.blend` 和 exporter 来源。新增导出资源放在 P11 独立目录。P9 Order / P10 Capacity domain 没有修改；MainCafe 仅 passive 接线且零营业 NPC。
- Scene / prefab / asset authoring 沿用 Unity APIs；2026-09-30 的幅度试调仅重导上述角色资源，没有改场景。
- 最终截图为 `outputs/phase11/final-fix-normal-screen-v2.png` 与 `final-fix-blocked-screen-v2.png`，均是实际 ScreenCapture（Canvas 加 Camera），`1080×1920`、`batch=False`，已用 view_image 查看。正常和 blocked 两帧均可看到八个角色，底部面板与 Camera 分开，P8 时间控件、英文 readiness 横幅和中文阻挡原因完整可读。旧 task7 截图与首版 final-fix 截图保留为历史证据，其 UI 遮挡问题由 v2 修复证据取代。
- blocked v2 使用真实 `furniture.counter.module.01` 的 Preview / Confirm：放到 Grid `(4,4)` 挡住 Coffee `(4,3)` 的 Employee station；修复为同一 Counter 移到 `(0,6)`。Codex 自动用例验证退出保持 Pause、1x/2x 被拒绝、没有一帧错误恢复，修复后恢复原 Fast，Westie_8 从原位置步行到原 Coffee Anchor 并 Arrived。此逻辑已自动 PASS；此前 Codex GUI 拖拽未可靠完成，Owner 随后自行实测并确认 M-009 PASS。
- Art/UX scoped re-review 确认两项 Important 静态布局问题已解决。保留两个 Minor：blocked 顶部保留区残留上一帧场景片段（文字仍可读），浅色 Westie 与地面反差偏弱。Owner 已确认 M-001～M-011，包括动画、穿插、相机操作及柜台站位与转向。此次确认不扩展为不同设备画幅的实机验证。
- `NavMeshAreas.asset` / `EditorSettings.asset` 无 diff。完整 Edit 的历史 builder 留下 30 个原本干净的资源变更：此前 23 个 Phase7 Materials / 两个旧验证 Scene，加一个 Phase4 Material、一个 Phase5 字体及三个 UI prefab。controller 逐项批准后保存 after bytes、patch 和 hash manifest，按 HEAD 原字节恢复。Play 最后残留的一个 `M_WallProjection_Invalid.mat` 也按同一白名单另行备份并恢复。MainCafe/P11 与本次 preflight snapshots 字节一致，未参与恢复；见 `outputs/phase11/final-scene-preservation.json`。
- 日志分类：Edit 的缺失 scene 诊断来自历史删除/拒绝配置负例；对应测试 PASS。完整 Play 有旧共享 `NotoSansSC-Regular SDF` 的缺字 warning（2320 行，分布在 21 个 Phase6/Phase8 用例），属于既有 UI 字体限制，不能宣称所有 UI 文字完美；P11 指定 Integration 缺字 warning 为 0。最终日志未见 inactive Animator、NullReference、MissingReference、EnhancedTouch reset 或 C# 编译 warning/error。Mono abort/debugger-port 和 license IPC 信息属于 runner 诊断，与 NUnit 结果分开。
- 本次不是 Android / iOS 真机、player build、正式营业 NPC、queue/tasks、经济、Save、多楼层或 NavMeshLink 验收。

## 4. 实现决策附录

| 决策 | 原因和实际取舍 |
|---|---|
| 保留约 1.30 m 原模型，Agent / bake / guard 半径一致为 0.45 m | 支持 1 m 单人通道；proxy 保护主要身体，轻微短暂边缘 Mesh 穿插由 Owner 判断。旧 0.56 m、统一向外挪 Anchor、全通道两格的规则已被新要求取代。 |
| observation 增加可选 ResolvedTargetPosition | 允许 0.15 m 内合法目标采样，同时用实际目标判断 0.08 m 到达；原始目标与采样后目标不能混为一谈。service 检查有限值、距离和 AllowedRegion，driver 检查高度与柜台同侧。 |
| 只有最终站位的转向可重置无进展计时 | 防止远处原地旋转一直延后 3 s timeout；最终转身仍可完成 facing。每段总 deadline 继续有效。 |
| 起点仅容忍 NavMesh 垂直量化误差 ≤0.05 m，水平 ≤0.001 m | 兼容真实 bake 网格；独立 proxy clearance 仍需通过，actor Transform 不改变，也不用于跨楼层恢复。 |
| 实体角点允许一次有界切向投影 | native diagonal intent 的轻微向内分量会卡在 0.01 m skin；只去掉 inward 分量，不增加步长、不发明绕行方向，并重跑 NavMesh 和全套 guard。无递归 slide planner。 |
| native NavMesh 凸角同样允许一次有界切向投影 | 真实 Coffee 原 Anchor 路线发现 smoothed intent 会横切凸角；使用实际 Raycast 命中的有限水平法线，只去掉跨边界分量并限制原步长，重新执行 owned NavMesh raycast、端点和全部实体/动态 guard。不改 Anchor、半径、deadline 或 retry。 |
| disable 同步终止，保留仍存在身体的占用 | component / GameObject inactive 后不再运动、转向或推进请求；回调前停止 driver/presenter。inactive GameObject 仍保留 stationary proxy，直到 unregister / destroy，避免别人在原身体处生成；重新启用需新请求验证，不自动 relocation。 |
| 验证 UI 底部 dock，Camera 为顶部提示留空间 | 让八个角色和 P8 控件在当前竖屏代表帧可见；不改模型、场景序列化或正式产品 UI。保留顶部旧帧残影 Minor，完整输入体验由 Owner 判断。 |
| 每个 adapter 拥有独立 native agentType 和 NavMeshData | 防止 foreign world 的数据让自身错误 ready；代价是显式资源生命周期。仅清理自己拥有的 settings/data/agents。EditMode fixture 恢复原 native settings 与字节，避免测试污染 NavMeshAreas。 |
| 明确检查 formal representation roots 与 gridRoot 矩阵 | 支持共同平移/yaw；不支持 nonuniform scale、shear、错位时直接 fail-closed，避免猜测家具 Collider 位置。 |
| 原资源导入 globalScale=1.0，BakeMesh(mesh,true)，ModelRoot yaw=180° | 试调前真实运行 Mesh 高约 1.306 m，视觉前方与导航一致。Task 2 曾写的 .01 scale 结论已被 Task 4 runtime 证据取代；不能继续引用为最终值。2026-09-30 幅度试调保留模型几何与缩放，并已通过 Unity 尺寸、脚底和全周期检查；见上文 focused 证据。 |
| 新阻挡文字使用私有 runtime 中文字体资源 | 既有静态 SDF 缺部分汉字；仅新标签创建并释放自己的 font/material/atlas，不改共享字体。连续两次关闭 Domain Reload 的 Play 生命周期已有资源清理测试。 |
| business config 统一 gate，验证 fixture 显式保留 required startup roster | 缺 adapter/time/floor 或一个 actor 注册失败不能让剩余 7 个角色误报 Ready；原参数/真实位置修复后可重新验证。普通 world 保留动态注册/销毁语义。 |
| MainCafe passive、独立 P11 scene enforceBusinessReadiness=true | 保护现有装修演示；真实营业接入仍需后续阶段明确启用，不能因为预接线就声称 Customer/Employee 已完成。 |

## 5. Owner 验收与交付状态

### 2026-09-30 Owner 截图反馈

- M-003 原截图：crowd8 为 3 Arrived / 5 Failed；Shiba_1 为 retry=1 / MovementTimeout，Westie_2、Westie_4、Shiba_5、Shiba_7 为 retry=1 / InvalidStart。P-007 允许拥堵时有限失败，因此失败数量本身不是判定依据。InvalidStart 已按第 2 节修复；原 `CrowdEightButtonTerminates` 未检查全程分离，新帧率测试补充了每帧角色对的连续相对距离检查。Owner 随后确认 crowd8 复测 PASS；最新 M-001～M-010 确认也包含 crossing。
- M-004：Owner 明确反馈 PASS。
- M-005 A 原截图：detour 的 Shiba_1 #3 在柜台附近以 Failed / retry=1 / MovementTimeout 结束，未满足 Arrived 要求。问题已复现并修复，四种帧率的自动到达检查通过；Owner 随后确认 detour 复测 PASS。M-005 B：blocked85 的 Shiba_1 #2 以 Failed / retry=0 / InvalidTarget 结束，截图停在窄道外，符合拒绝目标预期。Owner 最新 M-001～M-010 确认包含 M-005 A/B。`complete` 仅表示请求已结束。
- 截图初次评估只核对代码并记录问题。随后授权的复现、修复和新测试见第 2 节；Owner 对 crowd8 与 detour 的复测反馈为“两个都 pass 了”。原失败截图仍作为历史证据保留；crossing 与 blocked85 的人工 PASS 依据后续 Owner 总体确认。

controller 已组织集中 review 与修复复核：Engineering 批准功能修复、assembly 归属修复和最终输入缓存清理；QA 已核对完整 Play / 指定 Integration，以及 Edit 的精确补测；Art/UX 无开放 Critical / Important，保留上述两项 Minor。对应记录为 `.superpowers/sdd/2026-09-29-phase-11-navigation/final-{engineering,qa,art-ux}-fix-review.md`。

Owner 先澄清“M1-10不是已经通过测试了吗？”，随后确认“11也完成了”。M-001～M-011 全部记为 Owner PASS，无剩余手动验收项，也无需重复测试。M-011 的 Test Runner 仍有固定模拟帧和自动卸载限制；人工结果依据 Owner 本次确认。

Phase 11 开发与验收在现有 `codex/phase-9-order-domain` 分支完成，Roadmap 已同步。既有 Engineering / QA / Art/UX review 和后续修复证据保留，两个 Art Minor 继续列入已知限制。最新 P9–P11 review、完整回归与交付状态统一见 [合并前 Review](Phase9_Phase11_Merge_Review.md)。Owner 已授权 review 后 push 和创建 PR；尚未 merge main，未开始 Phase 12 实施。
