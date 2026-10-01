# Phase 9–11 合并前 Review

日期：2026-09-30～2026-10-01（America/Toronto）。分支：`codex/phase-9-order-domain`；目标：`main`。

## 范围与当前状态

Owner 已授权完整 review、修复、提交并 push 当前分支，以及创建合并 PR。基线为 `c80f208`，P9 提交 `94cc48d`、P10 提交 `a0877da`；本轮同时审查尚未提交的 P11。

三个独立 reviewer 分别检查 P9/P10 domain、P11 移动核心、P11 装修集成与资源。自动回归由主 agent 串行运行。

**结论：完整 review 与修复后的完整回归通过，无开放 Critical / Important；保留 3 项非阻塞 Minor。** 本文是提交前的验证记录；Owner 已授权随后 push 当前分支并创建合并 PR。远端提交与 PR 状态以 GitHub 交付记录为准，main 合并和 P12 实施尚未执行。

## Review 记录

| 范围 | 发现 | 处理状态 |
|---|---|---|
| P9 Orders / P10 Capacity | 未发现 Critical / Important / Minor | 源码和完整回归通过；P9 Edit 132 / Play 3、P10 Edit 108 / Play 3 全部 PASS |
| P11 角色注销 | World 在终止回调之后清理旧注册，回调中的重新注册可能被误删 | 已复现并修复；先清理旧 World 注册，再发送终止回调 |
| P11 business readiness | 禁用 NavigationWorld 后，退出装修的 gate 仍可能使用旧 readiness 恢复时间 | 已复现并修复；business gate 检查 coordinator 活跃状态，startup 在暂停期间先启用再验证 |
| P11 Walk 重新启用 | inactive Animator 留下过期的 walking 状态，角色实际移动时仍显示 Hold | 真实 prefab 用例已复现；重新激活后按实际速度同步一次 Walk/Hold |
| P11 scene builder | 再次从已接线的 MainCafe 生成验证场景可能复制重复的 navigation owner | 非阻塞 Minor，保留于下方限制；当前已保存的场景各只有一套 owner |

两位 P11 reviewer 已对修复源码和新增用例独立复核，未发现新增 Critical / Important。最终完整 EditMode、PlayMode 均已完成，CLI exit 0。

本轮运行时修复涉及 `NavigationWorld.cs`、`NavigationLayoutAdapter.cs`、`NavigationValidationController.cs` 和 `NavigationWalkPresenter.cs`；新增用例位于 `NavigationMovementTests.cs` 与 `Phase11NavigationSceneTests.cs`。P9/P10 domain 源码未因本轮 review 改动。

## 验证记录

- 本轮首次完整 EditMode 在 review 发现问题后主动停止，未生成完成结果；不计作 PASS 或测试 FAIL。日志保留于 `outputs/review-p9-p11-20260930/full-edit.log`。
- `lifecycle-red.xml`：4 FAIL / 0 PASS，分别复现注销重入、移动中的 Hold，以及禁用 World 的立即退出和 Update 检测路径。均为预期行为断言失败。
- `lifecycle-green.xml`：64 PASS / 0 FAIL / 1 opt-in screenshot SKIP，CLI exit 0。覆盖 NavigationMovement、NavigationAnimation 和 Phase11NavigationScene；4 个新增用例均 PASS。该 filter 未包含 NavigationLayoutIntegration，它由后续完整 PlayMode 覆盖。
- `full-edit-final.xml`：2440 PASS / 0 FAIL / 0 SKIP，CLI exit 0；P9 132、P10 108、P11 77 项均 PASS。测试实际执行 1981.013 s，UTC 2026-10-01 03:20:48 至 03:53:49。
- `full-play.xml`：1117 PASS / 0 FAIL / 5 opt-in SKIP，1122 total，CLI exit 0。测试实际执行 417.995 s，UTC 2026-10-01 03:56:42 至 04:03:40。4 个新增生命周期用例、11 个 NavigationLayoutIntegration 用例和 P9/P10 各 3 个 PlayMode 用例均 PASS。
- 已有 Owner 验收继续有效：P9 的手动检查已记录，P10 的委托检查已由 Owner 接受，P11 M-001～M-011 全部 Owner PASS。自动化结果不替代这些人工记录。

上述 XML 和对应日志位于 `outputs/review-p9-p11-20260930/`；`results-summary.json` 保留每次计数、时间和全部非 PASS 用例。不叠加 focused 与完整 suite 的重复覆盖。

完整 PlayMode 的 5 个 SKIP 均为显式 opt-in，不计作 PASS：

| 用例 | 未启用的检查 |
|---|---|
| `P8RCashRegisterSideIndicatorTests.CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery` | `ANIMALCAFE_CASH_SIDE_CAPTURE=1` 原生截图 |
| `P8RCompactChromeTests.SpacingAudit_WhenRequested_ExportGeometry` | spacing geometry audit |
| `P8RReadinessSafeAreaTests.ReadinessChecklist_CaptureNativeExamplesWhenRequested` | 原生 Game View 截图 |
| `P8RUiEnhancementSceneTests.NativeUiExamples_WhenRequested_CaptureActualGameView` | 原生 Game View 截图 |
| `Phase11NavigationSceneTests.CaptureRepresentativeCanvasAndCamera` | `ANIMALCAFE_P11_CAPTURE=1` 原生截图 |

## 提交范围与测试副作用

- review 后冻结的 31 个源码/工具文件 SHA-256 在完整回归结束后全部一致。
- 随后的提交检查仅移除了 21 个源码/文档文件末尾多余空行；保留原字节备份，并通过 `git diff --ignore-blank-lines --exit-code` 确认非空白内容未变。该格式清理没有重跑 Unity；源码/文档的 staged whitespace 检查通过。
- EditMode 生成的 30 项旧资源改写，以及 PlayMode 的 1 项旧材质序列化改写，均已保留 after 证据并精确恢复 preflight 字节。中止测试遗留的两个确切 P8RGuard fixture 文件也已备份清理。
- MainCafe、Phase11Navigation 与 preflight 场景字节一致，ProjectSettings / Packages 未变。
- 本轮按 94 个明确文件提交 P11 实现、导入资源、测试、文档和既有集成改动；日志、截图、备份、SDD 工作记录及空目录遗留的 `Assets/Tests/PlayMode/Phase11.meta` 不进入提交。

## 边界与已知限制

- P9/P10 保持独立业务服务；P11 提供共享移动基础。正式顾客生成、排队及员工任务仍属于 P12/P13。
- Shiba/Westie 的 FBX、纹理、controller 和 prefab 随 P11 交付。两份原始 `.blend` 按批准方案保存在外部原模型目录；从源重新导出需要这些原件，不能将此 checkout 描述为可独立重建全部美术源文件。
- 既有 Art Minor 保留：堵路横幅顶部留白区可能残留上一帧画面；浅色 Westie 与浅地面反差偏弱。
- 直接打开现有 `Phase11Navigation` scene 可正常使用。当前不要连续执行 “Create Navigation Validation Scene” → “Wire Business Navigation”：再次生成可能复制已有 Navigation 接线。后续应让 builder 复用或排除旧接线，并验证重复生成后的 owner 数量；本轮未重新生成已验收场景。
- 本轮授权为 push 和创建 PR；PR 合并及后续阶段开发单独处理。
