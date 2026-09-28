# PR #8 合并前 review / Pre-merge review

Base: main `4f5b9639d6c5687f10d9b1bc410c408c7540b067`。Initial reviewed head: `72dc81e3feddd666aaf7e81126907c12a9d66fbc`。

## 范围与静态结论

Review 覆盖整个 PR 的运行时代码、Editor builder、测试与文档。玩法/Editor 和 UI 由两个独立只读 reviewer 复核，未发现可确认的产品代码合并阻塞问题。重点检查家具及 CR/CM/Pickup 边界、取消/确认、occupied Store、鼠标 gesture ownership、资源定向保存、PrefabStage 防护、时间暂停所有权、HUD 状态恢复、checklist/tooltip、不同屏幕及折叠状态布局。

五项旧 UI 测试预期需要迁移：折叠后的范围隐藏及同排 tabs、Normal summary/Decor checklist、tabs 保持原横向位置、114-unit 单 category viewport、退出 Decor 后只保留 summary。迁移保留触控尺寸、遮挡、raycast 和状态断言。

## 验证记录

- 全量 EditMode 初次：2123 total，1768 PASS、0 FAIL、355 SKIP。早期测试留下 dirty `AssetPipelineReadability` Scene，触发后续测试的 caller-work 保护；这些 SKIP 不能记为 PASS，须在干净进程补跑。
- EditMode 隔离补跑：366/366 PASS、0 FAIL、0 SKIP；按完整 test fullname 去重，与首轮共同覆盖全部2123项，无未验证项目。证据：`pr8-full-edit.xml`、`pr8-edit-isolated.xml`。
- 首轮全量 PlayMode：1040 total，1022 PASS、14 FAIL、4 SKIP。除已知 UI 断言外，Phase6–8 测试仍引用旧按钮节点；三个 Touch 用例依赖默认640×480下不再可触的坐标。改用公开 toggle 引用、实际 raycast/classifier 选点、明确1080×1920手机profile，并为独立owner-disable分支重建场景。真实input、点击次数、pointer ownership、domain与layout断言保留；Normal事件测试加强为转换恰好一次。独立复核确认未通过弱化断言掩盖产品问题。
- 14项失败补跑：13/14 PASS，最后一项在补充明确手机profile后1/1 PASS。
- 最终全量PlayMode：1040 total，1036 PASS、0 FAIL、4 SKIP（`pr8-final-full-play.xml`）。四项是需显式启用的Cash Register截图、spacing测量、readiness截图及UI enhancement截图；不是功能用例失败。
- Native截图：独立启用后1/1 PASS、0 FAIL，证据 `pr8-final-native.xml`。1080×1920截图位于 `outputs/p8r-ui-enhancement/native-20260928-131817/`；人工检查1x、2x及Decor floor footer画面，状态标识、summary/checklist和底部控件无可见重叠。
- 测试开始时 tracked 工作树干净；资源构建测试产生的资产/验证场景序列化差异已恢复，不属于交付修改。
- 原始 XML/log 保留在本地 `outputs/p8r-review-fixes/`，截图保留在上述 native 目录，不纳入 Git。

## 验证边界

Editor fixtures 和原生 Game View 不等于 Android/iOS 真机验收。程序滚动到项目后执行 pointer 操作，不独立证明每个项目都能通过实体触屏手势到达；另有滚动路由测试。极短横屏 tooltip 可读性及复杂无效布局下的实际视觉效果仍需设备验收。

全量PlayMode日志包含旧readiness fixture的中文缺字警告；当前P8R英文UI截图已检查，中文本地化视觉验收不在本次范围内。

本 PR 不启动 Phase 9。

## 2026-09-28 合并后收尾 / Phase 9 readiness

**结论：READY FOR PHASE 9 DESIGN；Phase 9 implementation 尚未批准。**

- PR #8 已合并，main merge commit为 `0417d627a3e355206fb4a6310c41690d6a9fdec2`，已验证分支head为 `f50e8708cb04e910ffee74d7fa194effd1e024b1`。两者Git tree完全相同；本地main已fast-forward到merge commit。本次只更新收尾文档，依照项目流程不重复已经通过的Unity全量回归。
- 本次review未发现阻止纯逻辑Order Domain设计的已确认Critical/Important。已检查Roadmap、Game Design、readiness只读报告、现有layout IDs与time service边界；没有现成OrderService需要迁移。
- Phase 9范围仍为唯一Order ID、单一OrderService状态所有权、FIFO waiting queue、create/claim/transition/complete/fail和重复操作保护。Customer/NPC、NavMesh、capacity/reservation、付款、库存、Save和完整经营循环属于后续阶段，不因Game Design全局描述而提前实现。
- Phase 9 design需先确定合法state transition表、claim ownership、重复请求的返回语义、ID递增/溢出及作用域、failure与queue一致性。现有家具StableId是GUID，不能直接当作Roadmap要求的递增Order ID。上述是新阶段正常设计工作，不是本轮遗漏的产品修复。
- Owner已通过连续UI反馈逐项批准修改，并明确授权full review、merge及本地收尾。没有单独记录完整manual checklist或实体Android/iOS PASS；保留前述视觉与设备限制。此readiness结论允许设计准备，不代替Owner对Phase 9 design和implementation plan的批准。

### 本地证据与清理

- 主项目：`E:/Unity/Project/AnimalCafe`。原有 `.gitignore`、`AnimalCafe.slnx`及Roadmap在fast-forward前备份，fast-forward后逐字节校验未改变。备份位于 `outputs/p8r-closeout-20260928/main-before-fast-forward/`；Roadmap随后仅在本次收尾范围内更新，其已确认Existing Feature Enhancement方向保留。其他未跟踪资料不动。
- 原worktree全部outputs（3578文件、701116318 bytes）、Logs（7文件）和UserSettings（5文件）移动到 `outputs/p8r-closeout-20260928/worktree-evidence/`，移动前后文件数和总字节一致。最终测试证据位于该目录下 `outputs/p8r-review-fixes/`；原生截图位于 `outputs/p8r-ui-enhancement/native-20260928-131817/`。最终四份XML归档后重新读取，counts与上文一致。原始log只留本机，不上传。
- 已移除 `.worktrees/p8r-existing-feature-enhancement` 与本地 `codex/p8r-existing-feature-enhancement`。Git移除因Windows长路径留下部分目录；确认worktree登记已解除、证据已归档及精确目标路径后，清除剩余缓存/已提交文件。没有删除远端branch，也没有清理其他worktree。
