# Unity CI 失败核对与本地修复（2026-10-13）

## 本次证据

- [原始 CI run 38335550438](https://github.com/zhengparker/AnimalCafe/actions/runs/38335550438)，仅跟进 Owner 手动启动的这一轮；没有重跑云端 CI。
- 原始 source commit：`cf78005ea23c7d5dfe549026e01629edad48ec65`。
- Unity 登录及 license 激活成功。完整 EditMode：2485 PASS / 4 FAIL / 0 SKIP；完整 PlayMode：1183 PASS / 37 FAIL / 5 SKIP。共 41 项失败。
- artifacts `unity-all-1` 的 ZIP SHA256：`91779da1337dfacd80f53fbc27c3c0067f2e88e3fed31dd8995efb63db5203dc`，下载后核对一致。归档含两套 XML、两套 logs 及 COMBINE_RESULTS log。
- 实时日志截断并不代表测试没有继续：最终两套 tests 均已完成。以完整 artifacts 为准。
- 修复位置：项目本地 `.worktrees/phase-12-customer-queue`，branch `codex/phase-12-customer-queue`。本轮未 commit/push/merge，不改账号凭据。

## 处理范围

| 类别 | 原 CI 失败数 | 修复与边界 |
|---|---:|---|
| MainCafe Setup / 时间 UI 旧预期 | 13 | 需要营业速度的 fixture 建立真实完整布局；只在 UI 专项 fixture 隔离随机顾客流程。暂停显示开始、运行显示暂停。P022 迁移为真实空店尊重 Setup gate。 |
| 外来 UI 干扰独立世界 fixture | 23 | 测试 scope 暂停进入 scope 前已有的 GraphicRaycaster，退出后恢复；fixture 自己创建的 UI 仍保持生产触点优先级。 |
| Input System controls 生命周期 | 1 | 在替换 InputSystem registry 前卸载旧场景并释放 UI action cache；迁移 fixture 在卸载后同样释放缓存，不忽略异常。 |
| 短 UI 动画的慢帧依赖 | 1 | UiTransitionRunner 可接受确定性 frame clock；默认仍为真实 unscaledDeltaTime。Catalogue 与其它 views 使用同一 clock。测试保留 250ms 慢帧及全部中间态/owner 断言。 |
| Linux 路径文件名解析 | 1 | 提取文件名时将反斜杠规范为 slash；原始 assetPath 的严格路径校验保留。 |
| glyph GPU 缩图采样 | 2 | 测试明确指定显示尺寸对应的 mip 层，仍经 GPU readback，覆盖率及误差阈值原样保留。Linux llvmpipe 根因尚未在本地复现；此调整是采样环节的候选修复，不能宣称云端已通过。 |

## 本地验证

- 原 CI 失败方法本地 baseline：EditMode 3 PASS / 1 FAIL；PlayMode 25 PASS / 12 FAIL。28 项只在原 CI 环境/顺序出现，未虚报为本地复现。
- Setup 修复后：EditMode 4 PASS；原失败 PlayMode 参数用例 37 PASS。P022 的新名是 `P022_MainCafePassiveAndZeroActorsRespectSetupGate`。
- 受控外来 Canvas 与 250ms 慢帧：先取得 0 PASS / 2 FAIL 的 RED；真实 UI 注册及 clock 修复后 2 PASS / 0 FAIL。
- Input 场景切换序列：14 PASS / 0 FAIL。
- glyph 原测试在本地 Direct3D 和硬件 OpenGL 均通过；明确 mip 采样后本地 OpenGL 2 PASS / 0 FAIL。不能用 NVIDIA OpenGL 代替 Linux llvmpipe 验收。
- 完整 Windows 本地 EditMode：2489 PASS / 0 FAIL / 0 SKIP，2110.875 秒。结果：`outputs/ci-review-38335550438/local-full-edit.xml`。
- 完整 Windows 本地 PlayMode：1221 PASS / 0 FAIL / 5 SKIP，805.787 秒；总计 1226 项（新增 1 项真实 UI 隔离回归）。结果：`outputs/ci-review-38335550438/local-full-play.xml`。
- 原 CI 的 41 项失败（含 P022 更名映射）逐项核对，均在完整 Windows 回归中 PASS；映射证据：`outputs/ci-review-38335550438/failed-case-local-verification.json`。其中 glyph 两项仍仅证明 Windows 通过。
- P12 专项在完整 PlayMode 内：runtime identity 2 PASS、flow 36 PASS、真实 scene 45 PASS，合计 83 PASS / 0 FAIL。
- 原 CI 的 5 项 SKIP 均为显式开启的截图 / 几何导出用例，本轮没有将 SKIP 当作 PASS。
- 本轮代码 / test shader / meta 的逐文件 SHA256 清单：`outputs/ci-review-38335550438/local-repair-source-manifest.json`；该清单 SHA256 为 `B16AB0C3655326500CDDC17C9FC3AA795BE305029E104EFA5FF3875391218A08`。
- 修复后的云端 CI / Linux llvmpipe：NOT_RUN，遵守 Owner 暂不重跑的要求。

### 修改文件与用途

- 生产 / Editor 逻辑 3 个文件：`BenchmarkAssetValidator.cs` 修正跨平台 basename 解析；`UiTransitionRunner.cs` 及 `DecorationCatalogueView.cs` 允许使用统一可控 clock，默认仍为真实 unscaled time。
- 14 个已有测试文件：更新真实 Setup 前提和按钮语义，清理场景 UI input 生命周期，为独立世界 fixture 隔离外来 raycaster，稳定动画和 GPU 采样验证。
- 新增 `StandaloneUiRaycastScope.cs`（及 meta）仅供独立测试 fixture；新增 `GlyphMipReadback.shader`（及 meta）仅供 glyph GPU readback 测试。
- 本报告记录原始失败、修复范围、定向 RED/GREEN、完整回归及平台限制。没有修改 MainCafe / P12 scene、顾客队列规则或 CI workflow。

## 逐项核对

发布准备：Owner 随后授权用本轮修复重跑一次云端 CI。提交前仅规范新 shader meta 的 3 行尾空格，GUID 与所有可执行源码保持本地回归版本；下方 NOT_RUN 记录指本地修复收尾时的状态，后续云端结果单独记录。

下表使用原 CI 用例名，保留所有参数化失败；没有删除或 Ignore 这些用例。

| 原失败用例 | 类别 |
|---|---|
| `EditMode.AssetPipeline.BenchmarkAssetValidatorContractTests.ValidatePrefab_PathWithBackslashesReportsInvalidAssetPath` | Linux 路径 |
| `EditMode.P8R.P8RCompleteUiTests.HudSpeedAndDecorationLock_RefreshPngStatesFromEvents` | Setup / 时间 UI |
| `EditMode.P8R.P8RGlyphMinificationTests.ImportedGlyphs_ShrinkWithoutLosingSubpixelStrokeCoverage(32)` | GPU mip 采样（Linux 待复核） |
| `EditMode.P8R.P8RGlyphMinificationTests.ImportedGlyphs_ShrinkWithoutLosingSubpixelStrokeCoverage(64)` | GPU mip 采样（Linux 待复核） |
| `PlayMode.EditorSceneLoading.P8RCompleteFlowTests.HudAndExit_KeepExactPriorSpeedAndOnlyDiscardCurrentPreview` | Setup / 时间 UI |
| `PlayMode.EditorSceneLoading.P8RInputRecoveryPlayModeTests.DeviceRemovalDuringSceneDrag_AllowsFreshTouchAfterReconnect` | Input 生命周期 |
| `PlayMode.EditorSceneLoading.P8RReferenceLayoutTests.Hud_TwoTimeControlsShareTopLeftRow_AndRestoreExactPreviousSpeed` | Setup / 时间 UI |
| `PlayMode.EditorSceneLoading.P8RReferenceLayoutTests.Polish_HudControlsStayReadableAcrossSpeedAndDecorationChanges` | Setup / 时间 UI |
| `PlayMode.EditorSceneLoading.P8RReferenceLayoutTests.TimeOptical_TwoIconsStayReadableAfterEveryStateRefresh` | Setup / 时间 UI |
| `PlayMode.Phase11Integration.Phase11NavigationSceneTests.P022_MainCafePassiveAndZeroActorsKeepsLegacyFast` | Setup / 时间 UI |
| `PlayMode.Phase5MainCafeMigrationPlayModeTests.VirtualMouse_ClicksPauseResumeAndSpeedToggle` | Setup / 时间 UI |
| `PlayMode.Phase5MainCafeMigrationRound1PlayModeTests.LoadPauseInputUnloadAndReload_KeepsOneCleanMainCafeRuntime` | Setup / 时间 UI |
| `PlayMode.Phase5MainCafeMigrationRound1PlayModeTests.PauseResumeAndSpeedToggle_EmitOneOrderedEventPerRealUiClick` | Setup / 时间 UI |
| `PlayMode.Phase6DecorationRealTouchTests.CompositeOrder_MainCafeDecorationTouchThenPhase5ProductionTouchRestoresRuntimeIsolation` | Setup / 时间 UI |
| `PlayMode.Phase6DecorationRealTouchTests.CompositeOrder_Phase5ProductionTouchThenMainCafeDecorationTouchRestoresRuntimeIsolation` | Setup / 时间 UI |
| `PlayMode.Phase6DecorationRealTouchTests.MainCafeRealTouch_HudEnterExitOwnsPauseGridCatalogueAndRestoresCleanly` | Setup / 时间 UI |
| `PlayMode.Phase6DecorationRealTouchTests.MainCafeRealTouch_TwoPointerEndedCanceledOrdersAndControllerDisableLeaveNoStaleOwner` | Setup / 时间 UI |
| `PlayMode.Phase6DecorationUiPlayModeTests.GeneratedProductionViews_ReducedMotionPauseAndRapidReverseFinishOneStableRaycastOwner` | 慢帧动画 clock |
| `PlayMode.Phase7WallMountedTouchPlayModeTests.ControllerClassifier_ProducesTypedFloorGridAndWallSurfaceHitsForTheActiveMode` | 外来 UI 隔离 |
| `PlayMode.Phase7WallMountedTouchPlayModeTests.ControllerClassifier_ProducesTypedWallMountedHitBeforeUnderlyingWallSlot` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(CashRegister,"equipment.cash-register",False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(CashRegister,"equipment.cash-register",True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(CoffeeMachine,"equipment.coffee-machine",False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(CoffeeMachine,"equipment.coffee-machine",True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(PickUpPoint,null,False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_FunctionalDragOutsideFloor_ClampsAllEdgesAndReturnsToSlot(PickUpPoint,null,True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorCancelRestoresSourceAndClearsFallback(CashRegister,"equipment.cash-register")` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorCancelRestoresSourceAndClearsFallback(CoffeeMachine,"equipment.coffee-machine")` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(CashRegister,"equipment.cash-register",False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(CashRegister,"equipment.cash-register",True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(CoffeeMachine,"equipment.coffee-machine",False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_MountedPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(CoffeeMachine,"equipment.coffee-machine",True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_PickUpPreview_FloorCancelRestoresSourceAndClearsFallback` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_PickUpPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(False)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_PickUpPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(True)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_SurfaceDrag_TableCornerBeyondSnapRadius_StaysAtSlotHeight(Degrees0,2.5f,0.5f)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_SurfaceDrag_TableCornerBeyondSnapRadius_StaysAtSlotHeight(Degrees90,0.5f,2.5f)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.Controller_SurfaceDragAcrossUi_PreservesSlotHeightAndResumesOnTable` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.TabSwitch_ControllerConsumesOldAllUpBeforeAcceptingNextTouch(Ended)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.TabSwitch_ControllerConsumesOldAllUpBeforeAcceptingNextTouch(Canceled)` | 外来 UI 隔离 |
| `PlayMode.Phase8FunctionalSurfaceInteractionPlayModeTests.TabSwitch_ControllerConsumesOldAllUpBeforeAcceptingNextTouch(None)` | 外来 UI 隔离 |

## 验收限制

完整 Windows 本地回归不能替代 Linux CI、Player 或手机验收。原 M1～M8 的 Owner 验收记录保留；本轮不是重新验收顾客动线。EditMode 重建的 31 个资源在逐项备份并校验 SHA256 后还原；PlayMode 的 1 个材质序列化变化也已备份并还原。备份及路径清单保存在测试输出中。测试结束时修复源码 hash 与运行前一致，MainCafe / P12 validation scene / EditorBuildSettings 与测试前 hash 一致。所有测试产物留在 `outputs/ci-review-38335550438/`，不作为新增产品功能。本轮 commit / push / merge / 云端重跑均为 NOT_RUN。
