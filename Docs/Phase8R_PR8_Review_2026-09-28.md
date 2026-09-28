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
