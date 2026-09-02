# Phase 7 Manual Test Result Sheet

> 状态：`PASS — STUDIO OWNER MANUAL ACCEPTANCE COMPLETE`。Phase 7 已完成 automated regression 与 Studio Owner hands-on review；下一步仅为 feature branch commit、push 与 merge PR。
>
> 2026-08-27 automated gate：graphics EditMode `1436/1436`、graphics PlayMode `590/590`，failed/skipped/inconclusive `0`；273-file final working-copy audit drift `0`。本表所有人工结果仍须由 Studio Owner 亲自填写。
>
> 2026-08-27 Task 17 stabilization gate：full EditMode `1438/1438`、full PlayMode `599/599`，failed/skipped/inconclusive `0`；Phase 7 focused EditMode 分片合计 `299/299`（domain `228/228`、validator `28/28`、MainCafe migration `30/30`、asset builder `13/13`）。本轮 273-file audit 的非语义 serializer churn 已精确还原，只保留已批准的 Phase 7 Catalogue prefab 差异。
>
> 2026-08-27 manual-review finding fix：Wall-mounted Base Wall contact RED `0/2` + production prefab RED `0/5`；GREEN `2/2` + `5/5`；direct regressions Wall Mounted `47/47`、Surface `38/38`、MainCafe `16/16`；fresh full PlayMode `600/600`，failed/skipped/inconclusive `0`。Studio Owner 仍需在两面墙逐件检查是否无悬浮与明显穿模。
>
> 2026-08-28 Task 19 interaction amendment：focused PlayMode `102/102`、focused EditMode `44/44`、P7 Scene `54/54`、Phase 6 Scene/RealTouch `27/27` 全绿。完整 PlayMode `600/607`，7 个失败均为 Unity Input System 跨套件 `statePtr` 顺序污染；受影响的 Task 9 `2/2` 与 Phase 5 `5/5` 已在干净 Editor 隔离复跑全绿。人工仍需验证手感、排版和 tooltip 行为。

> 2026-08-28 Task 20 UI/Bug self-audit：5 张 mounted thumbnail 已改为真实 prefab 的统一暖色游戏内墙面预览并移除黑底；AssetBuilder `15/15`、clean Validator `1/1`、UI `52/52`、Surface `38/38`、Wall Mounted `50/50`。Production MainCafe 首轮 `15/16` 找到 inner margin 只有 `9.237625 px`，修复后 focused `1/1`、完整 MainCafe `16/16`；所有 GREEN 的 failed/skipped/inconclusive 为 `0`。视觉 acceptance 仍留给 Studio Owner。

> 2026-08-28 manual-review action amendment：Furniture / Wall Decor 恢复跟随 Preview 的 compact icon actions；Floor / Wall 保留 Sheet footer 大号文字按钮。RED 为 UI `50/55`、MainCafe focused `0/1`；GREEN 为 UI `55/55`、MainCafe focused `1/1`，direct regressions MainCafe `20/20`、Wall Mounted `50/50`、Phase 6 MainCafe `9/9`、Phase 6 RealTouch `18/18`、Wall footprint matrix `108/108`。一次过宽 filter 的 PlayMode run 为 `327/328`，唯一 real-Mouse drag failure 在干净进程隔离复跑 `1/1`；不把首次失败隐藏。MT-018 已改为 MainCafe production `2×1/1×2` 手测，`2×2/3×2` 由 automated matrix 覆盖。

> 2026-08-29 M26/M29 amendment：Exit Modal 改为 full-screen dim blocker + 独立暖色圆角 card，不再作为 Bottom Sheet/Catalogue controls 浮在 sheet 上；进入 Floor Mode 后全部 confirmed Furniture 无需 Preview 即约 `35%` 淡化，离开对应 lifecycle 后精确恢复，且不修改 Collider/occupancy/Layout。RED：M26 `0/1`、M29 `0/1`；focused GREEN `3/3`。Direct GREEN：MainCafe `23/23`、UI `55/55`、Surface `40/40`、Wall Mounted/Touch `51/51`、AssetBuilder `16/16`、final Migration `30/30`。Migration 首轮 `29/30` 的唯一失败为旧 test 仍按旧 Bottom Sheet hierarchy 统计 Modal buttons，更新 contract 后 focused `1/1`、final `30/30`。Manual visual acceptance 仍待 Studio Owner。

> 2026-08-29 M31/M32 follow-up（后续规则覆盖旧 thumbnail 段落）：Wall Decor Confirm 后保持 Compact，并允许下一 tap 立即重选；Wall blocker distance 使用 rotated Wall 的真实 plane；footprint 位于最外层 Wall/Wainscoting/trim 再向外 `1 mm`，ghost 与 confirmed item 仍贴 Base Wall；五张 thumbnail 改为 mounted-angle object-only transparent PNG。Focused GREEN：M31 `1/1`、M32 `1/1`、双墙 footprint `2/2`。Direct GREEN：Wall Mounted/Touch `52/52`、Surface/Fade `41/41`、AssetBuilder `16/16`、Validator drift `19/19`；所有 final XML 的 failed/skipped/inconclusive 为 `0`。Studio Owner 仍需手测 M31/M32 的交互与视觉。

> 2026-08-29 final closeout：Studio Owner 已确认 MT-001–MT-034 `34/34 PASS`，决定为 `GO`。Final compatibility regression：Phase 6 migration `127/127`；fresh full EditMode `1443/1443`；fresh full PlayMode `625/625`；failed/skipped/inconclusive 均为 `0`。

## 测试信息

- 日期：`2026-08-29`
- Unity：`6000.5.5f1`
- Branch / build：`codex/phase-7-interior-walls`（Studio Owner manual-review build；pre-commit）
- 测试者：`Studio Owner`
- Scene：`Assets/Scenes/MainCafe.unity`（MT-001–MT-034 全部使用 production Scene）
- Automated evidence：`TestResults/Phase7Amendment/Task19/green-focused-play-final2.xml`、`green-focused-edit-final.xml`、`green-p7-scene-final2.xml`、`green-phase6-scene-final.xml`、`full-play-final.xml`、`green-task9-isolated.xml`、`green-phase5-isolated.xml`

Studio Owner 已按 M1–M34 分批完成 production `MainCafe` hands-on review；以下结果均为直接人工确认。

| ID | 精确步骤 | Expected | Status | Date | Build | Observation | Evidence |
|---|---|---|---|---|---|---|---|
| MT-001 | Play MainCafe；点 Decor；依次点四 Tabs | 默认 Furniture；四 Tabs 可点；Time Pause；无 Error | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-002 | 比较 active/inactive Tab 高度、阴影和前后；点中心/边缘；收起 Sheet 再点 | folder tabs 从 Sheet 左上伸出；active 在最前且无遮挡；四个 touch target 易点 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-003 | Furniture 点墙/地；Floor 点墙饰/家具；Wall 点家具；Wall Decor 点地/家具 | 不支持对象不切 Mode、不打断 Preview | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-004 | 无 Preview 切 Expanded/Compact/Tabs Only；分别创建 Floor/Wall Surface Preview，再下拉并观察动画；hover/停留 Confirm、Cancel | Floor/Wall Preview 开始及更换样式时 Catalogue 保持 Expanded；玩家手动下拉时不能低于 Compact；Compact保留Tabs+footer；`0.16s`同步移动、无gap；Confirm/Cancel为加宽单行按钮且不显示tooltip | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-005 | 横滑 item row；竖滑 category；斜滑；从 UI 滑到 Scene | 单一方向获得 scroll ownership；Camera/Scene 不响应 UI 手势 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-006 | 观察 Using A/Preview B；截 grayscale；检查 Wainscoting None | 绿色勾、彩色框、crossed-circle 不靠颜色也能辨认；卡片无状态文字 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-007 | Floor fresh session；检查两行footer；默认 Whole Room；选 surface；Confirm；切 Tab 返回 | 第1行只有Whole Room/Single Grid；第2行为Undo/Rotate/Apply All/Cancel/Confirm且无重叠；Whole Room时前三个utility为灰色disabled；64格一起变化；不显示Using check；同session记住range | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-008 | 切Single Grid并检查footer；点一格看highlight；选surface；连续点数格并换花纹；Cancel后重做并Confirm | Single Grid时Undo/Rotate/Apply All恢复enabled；当前格highlight；所有Preview格边缘有绿色勾；armed style延续且可换；结束后markers清除 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-009 | 至少三格 Preview；Rotate；铺新格；重选旧格 Rotate；Undo Last | 90° 仅影响当前/后续；Undo 仅撤销最近 step | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-010 | 记录正式地面；Apply All；Undo；Cancel | Apply All 可一次 Undo；Cancel 恢复 transaction 前状态 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-011 | Floor Preview 时切 range/Tab；再分别 Confirm、Cancel 后重试 | active 时禁止切换且无 silent save/discard；完成后可切 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-012 | Wall点Back-left看Sheet footer；未修改点Back-right；选Base后再点另一墙 | footer立即出现、Confirm先disabled；无修改可换墙；修改后锁定；Wall无Apply All | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-013 | 同一墙Preview内更换Base和Wainscoting；选None；Cancel；重做后Confirm | 一次transaction自由组合；Cancel整墙恢复；Confirm两层一起提交；挂件/Window保留 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-014 | 改Wall两个layers；逐项选回原组合；再改两个layers并Confirm | 改动时Confirm enabled；完整回原组合后disabled；两个Current checks在Confirm后一起更新 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-015 | 未修改时换墙；产生Wall Preview后再换墙；点unsupported object；Cancel | 未修改可换target；修改后不可接管；unsupported无影响；Cancel恢复并清理highlight/outline | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-016 | 近看连续Floor、Wallpaper横向repeat、Wainscoting顶/底/板缝/阴影 | 无mapping跳变、宽边、纵向误重复；Wainscoting无交叉网格且不投出围栏式独立阴影 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-017 | 进入Wall Decor但不选item，在空白Scene drag；再依次选择两墙的3个Wall Decor和2个Window，按住ghost drag、Confirm一件并立刻再点它 | 空白Scene drag只平移Camera；按住active ghost drag只移动物品且Camera不动；每个真实prefab ghost垂直地面、平行墙面、背面紧贴Base Wall（约`1mm`防闪烁间距），不因Wainscoting/腰线整体悬空并对齐footprint；Confirm后Sheet保持Compact且下一tap立刻可重选；无明显穿模；小圆`×/✓`跟随ghost；无Rotate/Store | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-018 | 保持 MainCafe Play；在Wall Decor分别用Wood Shelf `2×1`和Window `1×2`，在两墙内部、水平/垂直边界、墙角及会越界的Slot放置 | 完整 footprint 在`8×2`内才valid；任一Slot越界即红叉且Confirm disabled；`2×2/3×2`精确规则由AT-011/AT-012覆盖，不要求test-only Catalogue | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-019 | 对已有 Decor 和 Window 分别制造 overlap | 两类都阻挡；红色叉、英文提示 `Wall space already occupied`；Confirm disabled | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-020 | active Decor/Window 从 Back-left 拖到 Back-right；慢过 corner；有效处 Confirm | 可跨墙移动；corner 显示具体 invalid；最终只占一面墙 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-021 | existing item 开始移动；观察原位；移动后 Cancel；再移动 Confirm | 移动中原位模型隐藏且只有一个ghost；Cancel 恢复同一模型与原 Slot；Confirm 复用同一模型、释放旧 Slot并占新 Slot；Confirm/Cancel跟随ghost；无 Rotate | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-022 | existing item 点 Store；先 dismiss；再 confirm；重放同款 | modal blocking；dismiss 不变；confirm 移除并释放；同款可无限重放 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-023 | 制造 valid、overlap、out-of-bounds、cross-corner；把projection拖过Wainscoting/rail/baseboard并用grayscale查看 | footprint始终在最外层墙饰前，绿色/红色不忽深忽浅；ghost仍贴Base Wall；green+勾与red+叉+具体英文原因清楚；invalid不能Confirm | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-024 | 选择被不同真实Furniture/Wall Decor遮挡的两面rotated Wall target；换target；Cancel；退出Mode | 以真实Wall plane为界只fade camera-to-target blocker；target清楚；所有lifecycle完全恢复 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-025 | Preview 时退出并观察Modal→Continue；再退出→Discard；重新进入 | full-screen dim blocker + 独立暖色圆角card；Continue/Discard都在card内而非Catalogue上；Continue保留编辑；Discard不提交并退出；无pending change | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-026 | 依次测 1080x1920、720x1280+safe inset、1080x2400+safe inset、1920x1080+safe inset；每个切 Tabs/scroll/Compact/Confirm/Cancel | 无 clipping/overlap；active Tab 在前；关键按钮在 safe area 内且可点 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-027 | 在非Furniture Tab退出；重进两次；混合四 Mode Confirm/Cancel/Store；resume time | 每次重进都同时显示Furniture active Tab和Furniture content且可点击；无 duplicate UI/Grid/registry、stuck Pause/input/fade；confirmed session state 保留 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-028 | 不看实现说明，独立浏览 category；每 Mode 完成一次 change；解释 Using/Preview/Invalid/Store | 初学者无需猜测即可完成并正确解释；卡住位置必须记录 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-029 | 进入Floor但先不选地砖，观察Furniture；再用三种Floor全铺/混铺，检查0/90/180/270、近远seams、scale、swatch；Cancel/切Tab/退出后再观察 | Floor内全部confirmed Furniture约35%淡化，Floor/Grid清楚，Wall/Wall Decor/Window不淡化；离开后精确恢复；Studio Owner接受三种Floor的seamless、比例、方向、swatch | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-030 | 两墙逐一Preview/Confirm Paint/Wallpaper；检查full-height/no vertical repeat；逐一检查Wainscoting腰线/地线/腰高/normal/shadow；选None；比较swatches | Studio Owner接受墙面seams、mapping、rail/baseboard、scale、无crosshatch/围栏影、None和swatches | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-031 | 先检查Catalogue的5张mounted thumbnails只有物品、无墙/地面/黑底/checkerboard；再在两面墙分别放置/移动/Store Painting 1x2、Wood Shelf 2x1、Monitor 1x1、Window 1x1、Window 1x2；检查贴墙距离、footprint、pivot、depth、角色比例 | 5张图保留Back-left mounted轻微3/4视角且为genuine transparent object-only cutout；Studio Owner接受全部3 Decor+2 Windows；背面无可见悬浮、与Wainscoting/腰线交界无明显穿模；无placeholder | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-032 | 用真实counter/桌子等不同blocker测试两面rotated Wall的fade；切四Tabs/三snap states；观察Sheet height/card/drag与固定`0.16s`；四responsive presets重复 | Studio Owner接受真实blocker fade/recovery、UI尺寸；Tabs与Surface footer无gap并同步移动；transaction不丢 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-033 | 正式素材下重跑MT-023/024/026/032；检查模型/Surface是否遮projection、Tabs、Surface footer、target | Studio Owner接受正式framing后的projection、fade、Tabs、Compact footer、target readability | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |
| MT-034 | 清空Console；进入/退出Decor并切换全部Tabs两轮；resume Game Time；检查 Console、Window、Entrance、Phase 6 Furniture | 无 unexpected Error/Exception/unexplained Warning，尤其无 NotoSansSC `Ellipsis` glyph warning；旧系统仍可用 | PASS | 2026-08-29 | `codex/phase-7-interior-walls` | Studio Owner hands-on review completed；无 blocker。 | Studio Owner confirmation in Codex task |

## Studio Owner decision

- 决定：`GO`
- 必须修改的问题：`无`
- 可延后 polish：`无 Phase 7 blocker；后续只在新反馈出现时单独记录。`
- 视觉接受备注（Floor / Wall / Wall-mounted / fade / Bottom Sheet）：`全部接受；MT-001–MT-034 全部 PASS。`
