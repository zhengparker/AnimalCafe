# P8R spacing 专项回归 — 2026-09-26

## 范围与结论

检查当前 `codex/p8r-existing-feature-enhancement` 的本地工作树（HEAD e6f5817 + 已确认的未提交 UI 间距改动）。本轮只审查并增加测量用例，没有修改产品布局规则。未 commit / push。

范围是 P8R UI 专项，不是整个项目全量回归，也不是 Android/iOS 真机验收。

## 验证证据

- P8R EditMode：380 / 380 PASS。
- P8R PlayMode：151 个，143 PASS / 5 FAIL / 3 SKIP。**整体未全绿**。
- 额外布局测量：1 PASS；六种屏幕配置 × 四个 tab × 三种状态 = 72 组合、594 条测量记录。
- 截图：1080×1920 实际 Unity Game View，Normal、Decor、四个 tab、收起、preview、tooltip、store 提示。
- 主证据：`outputs/p8r-ui-enhancement/spacing-audit-{edit,play,measure,native}.xml` 与 `spacing-audit-geometry.csv`。

屏幕配置（logical units）：360×640（有/无 Safe Area）、320×569、800×360、768×1024、360×800。展开、TabsOnly、CompactPreview 均测量。不可见 panel 的 rect 仅用于比较状态几何，不代表收起后仍显示面板。

## 已一致：建议保持

| 项目 | 实测 | 说明 |
|---|---:|---|
| 标题行 → 卡片 | 6 | 54 个可见 category 测量全部一致 |
| 上组卡片 → 下组标题行 | 18 | 30 个相邻 category 测量全部一致 |
| 标题距 panel 左边 | 12 | 所测可见 category 均一致 |
| 单行 header 的四个 tab 间距 | 4 | 保持既有同排规则 |
| 单行 header 的最后 tab → 收起按钮 | 4 | 普通竖屏、横屏、平板一致 |
| Floor 两个范围按钮间可见间距 | 约 4.016 | 额外 1/64 是防浮点重叠，不属于视觉缺陷 |

## 待调整清单（按优先级）

### S1 — 长屏手机会切回旧的两行 header

- 实测 360×800：tab 顶部距 panel 顶部 63，收起按钮为 11；相差 52。
- 四个 tab 均出现，三个面板状态保持该差异。
- 原因：`DecorationCatalogueView.cs` 的 `minimalHeader` 限制 logical 高度 <= 700，超过后 header 从 56 变为 108，toggle 被放在上一行。
- 这不是按钮二维重叠；负的横向 gap 是它们不在同一行，不能当作碰撞。
- 建议：同排与否由可用宽度、48 点击目标和必要工具决定，不应只由屏幕高度切回旧布局；长屏也保留四个 tab + toggle 同排。

### S2 — 横屏 / 平板顶部留白与左右不一致

- 360×640：tab 顶部 / 右侧 toggle 边距均为 12。
- 800×360、768×1024：顶部为 7，右侧为 12。
- 原因：宽屏 header 高52，按钮点击根48、可见底板34，另有 +4 的独立定位常量；与紧凑竖屏使用的公式不同。
- 建议：普通 profile 采用可见外观上边距12，必要时同步增大 header 的布局预留；不能直接下移而挤压下方标题。极短横屏若空间不足，应使用明确的 compact profile。
- 宽屏 tab 左边距124/160包含 Catalogue / Pickup / Return 控件的预留，并非左右 padding 错误，不能强行改成12。

### S3 — 极窄屏 header 与内容区未使用同一套外层 profile

- 320×569，Safe Area 宽288：panel 宽272；tab 左右8、上方12，但分类标题仍距左12。
- 原因：五个48点击区 + 四个4间距 = 256，仅剩16给左右边距。因此左右8是必要的触控适配，不应强行保留12并缩小按钮。
- 建议：明确 compact outer profile，例如顶部/左右/内容起点统一8；普通屏保持12。category内部6/18不变。

### S4 — Footer 外层规则尚未归入共用 profile（次优先级）

- `footerPadding` 在紧凑竖屏仍为4，内容区为12；这是容器可用宽度，不能直接当成按钮可见边距。
- 现有 Floor 范围按钮居中，实测左右相等，所以尚不能把它判为当前可见错位。
- 竖屏范围按钮底部可见留白16；Pickup 按现有代码为8根偏移 + (48-34)/2 = 15（静态推导）。1单位差异优先级低。
- 建议：集中定义 footer padding / bottom inset / 同组按钮间距，明确宽屏侧栏例外。保留按钮居中，不强制让短按钮撑满 panel。

## 五个失败用例：旧预期需要迁移，不能直接标 PASS

| 失败用例 | 旧预期 / 当前方案 |
|---|---|
| P8RCompleteFlowTests.ReachableHudAndSurfaceControls_Stay48LogicalAndDoNotOverlap | 仍要求 actions → range → handle → tabs 纵向排列；当前方案折叠后隐藏 ranges，toggle 与 tabs 同排 |
| P8RMobileLayoutIntegrationTests.PhoneAndTablet_HudCatalogueAndFloorActionsKeep48LogicalTargetsWithoutOverlap | 在 Normal 断言完整 checklist；helper 还要求无 panel、正文至少14；当前方案 Normal 是摘要，Decor 是半透明panel、12正文 |
| P8RReferenceLayoutTests.MobileLandscape_ReturnToEditingSharesHeaderWithoutTakingCardSpace | 要求折叠后 tabs 重居中；用户已要求保持原位置与宽度 |
| P8RReferenceLayoutTests.MobilePhone_WaitingGridDoesNotFillTheSceneWithEmptyCatalogueSpace | 固定旧 viewport 下限115.9；当前标题24+间距6+卡片84=114 |
| P8RReferenceLayoutTests.ReferenceFlows_ActualPortraitAndLandscape_CaptureReviewGallery | 退出Decor后仍断言 checklist 展开；当前只允许Normal摘要 |

这些是从失败调用点与当前已确认设计核对出的冲突。尚未更新并重跑这五项，因此正式状态仍为 FAIL，而不是把它们忽略或豁免。3个SKIP是需显式启用的native截图用例。

## 下一步建议

1. 先统一目录外层 header：S1 → S2 → S3，小步修改并截取1080×1920示例。
2. 再整理 footer 的profile，保留短横屏侧栏和48点击目标。
3. 更新上述旧测试为当前设计，补充长屏和compact profile断言，再重跑P8R全套。

本轮新增的 `SpacingAudit_WhenRequested_ExportGeometry` 是显式启用的测量测试（ANIMALCAFE_SPACING_AUDIT=1），默认跳过，避免把一次性报告写入普通测试流程。运行时布局未因审查而改变。

## 收尾记录

本轮实际Game View截图测试1/1 PASS，目录：outputs/p8r-ui-enhancement/native-20260926-202800。

EditMode执行后，M_WallProjection_Invalid.mat出现3行序列化变化（两处尾空格及浮点精度）。测试前该文件干净；自动审批两次拒绝git restore，文件暂保留，待用户明确授权。git diff --check只报告该材质的两处尾空格。其余UI改动未push。

## 后续处理（用户批准S1、S2）

已取消长屏700高度分界；宽屏header预留增至57，上方留白12，Pickup/Return同排下移5保持对齐。新增header测试覆盖5种profile、4个tab、3种状态，连同紧凑目录回归9/9通过。S3/S4未改，旧全套的5项失败测试仍待迁移，不宣称全套已绿。用户选择保留info 48点击范围，因此category间距仍18，标题到卡片6。

用户随后明确批准还原 M_WallProjection_Invalid.mat，本次已执行并验证该文件无diff；前述审批阻塞已解除。

## 后续处理 S3

用户确认先做第3项。窄屏header/内容共用8 outer inset，普通屏保持12；9项相关回归通过。S4保留待确认。新增1080x1920窄Safe Area原生截图示例。

## 后续处理 S4

用户批准第4项。Footer共用12/8外层profile；Pickup及Floor/Wall按钮可见底部留白一致，按钮组居中、同排可见间距约4、点击范围至少48。修正Apply/Cancel测量字重与实际显示字重不一致造成的额外空白。短横屏侧栏与滚动适配保留。

footer-profile-play.xml：12 PASS、0 FAIL；footer-profile-native.xml：1 PASS、0 FAIL。1080×1920实际Game View截图位于native-20260926-214246，已检查Furniture/Pickup和Floor预览Footer。旧全套5项FAIL仍待迁移，不宣称全套已绿；本轮未push。
