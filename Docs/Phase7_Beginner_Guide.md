# Phase 7 Beginner Guide — Interior Walls & Surface Customization

> 当前状态：Phase 7 implementation、final automated regression 与 Studio Owner manual acceptance 已完成。2026-08-29 evidence：EditMode `1443/1443`、PlayMode `625/625`、MT-001–MT-034 `34/34 PASS`；目前等待 merge PR review，不代表已经 merge 到 `main`。

## 2026-08-26 Manual review adjustments

- Catalogue 卡片使用约 `128 px` 的暖色圆角卡片，卡片间距 `8 px`，每一行仍然独立 horizontal scroll。
- Floor / Wall 素材：绿色勾表示目前已使用；有色 outline 表示尚未 Confirm 的 Preview；Cancel 后 Preview outline 必须清除。
- Wainscoting 只使用 Wall shader/texture 显示在墙面下半部；不会额外生成 geometry、Collider 或 NavMesh blockage。
- Wall Decor 选中后同时显示真实 Prefab ghost 与绿色/红色墙面 footprint；拖动时两者一起刷新，Confirm 生成正式实例，Cancel 清除 ghost；Wall Decor 不提供 Rotate。
- Floor / Wall 的 Cancel、Confirm 使用 Bottom Sheet 内的大号文字按钮；Furniture / Wall Decor 使用跟随 Preview 的小圆 icon buttons（`×`、`✓`，Furniture 另有 `R`）。
- MainCafe 开局不再显示临时预放 Window；两种 Window 仍保留在 Wall Decor catalogue，玩家可自行放置。

## 1. 打开正确的 Unity Project

1. 打开 Unity Hub。
2. 选择 `E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls`。
3. 确认 Unity version 是 `6000.5.5f1`。
4. 打开 `Assets/Scenes/MainCafe.unity`，不要打开 main checkout 中的同名 Scene。
5. 等待 import 和 compile 完成；Console 不应出现 unexpected Error / Exception。

Timeline package 的 immutable-package warning 属于 Unity package 自身提示；不要修改 `Packages/com.unity.timeline` 中的文件。若出现其他 warning，请截图并记录完整文字。

## 2. 进入 Decoration Mode

运行 Play Mode 后点击 `Decor`。Bottom Sheet 默认进入 `Furniture`，左上方有四个 Mode Tabs：

- `Furniture`：放置、移动、旋转或 Store 家具。
- `Floor`：选择 `Whole Room` 或 `Single Grid`，可以旋转 Floor pattern、`Undo Last`、`Apply All`。
- `Wall`：分别选择 `Wallpaper`、`Paint`、`Wainscoting`；`None` 表示不用 Wainscoting。
- `Wall Decor`：放置、移动或 Store Painting、Wood Shelf、Monitor、Window；不能 Rotate。

被选中的 Tab 应在最前方并向上突出。横向滑动浏览同一 category 的 items；纵向滑动浏览不同 categories。

## 3. 正式 Phase 7 素材

Wall Decor catalogue 应显示五个正式模型：

- Painting `1x2`
- Wood Shelf `2x1`
- Monitor `1x1`
- Window `1x1`
- Window `1x2`

这五张 Catalogue thumbnail 应显示真实游戏 prefab 安装在统一暖色墙面上的样子，并能看到轻微 `3/4` 角度、地脚线和接触感；不应出现 Blender 式黑色背景。

Production `MainCafe` 不应出现 `TEST_ONLY_*` placeholder。技术 Validation Scene 中可保留明确标记的 test-only fixtures，但它不进入 Build Settings。

## 4. 当前 persistence 规则

Phase 7 只有 **session-only persistence**：同一次 Play session 内 Confirm 的结果有效。`MainCafe` 不包含开局 Window seed；Window 只存在于 catalogue，必须由玩家 Preview 后 Confirm 才会出现在墙上。重新载入 Scene 会清除本次 session 放置的 Window。完整 Save/Load 属于以后 Phase 17。

## 5. Manual Play Mode checklist

每项记录 `PASS / FAIL / BLOCKED`、日期、Unity version、观察和 Console 状态。

1. Furniture：原有四件家具、Entrance、Game Time 全部存在且可用。
2. 四个 Tabs：切换后只有当前 Mode 接收 Scene input；active Tab 始终在最前方。
3. Floor：分别测试 `Whole Room`、`Single Grid`、Rotate、Undo Last、Apply All、Cancel、Confirm。
4. Wall：测试 Wallpaper、Paint、Wainscoting 和 `None`；每次选择一整面墙，在同一 Preview 中组合 Base + Wainscoting，再用一次 Confirm 或 Cancel 完成；Wall 不提供 Apply All。
5. Texture seams：查看 Wallpaper、Floor、Wainscoting 横向拼接和墙高填充。
6. Wall Decor：依次放置五个正式模型；检查 valid/invalid projection、green check/red cross，以及跟随ghost的小圆`×/✓`；Wall Decor无`R`。
7. Wall boundary：在MainCafe用Wood Shelf `2×1`测试水平越界、Window `1×2`测试垂直越界；invalid时Confirm disabled。`2×2/3×2`精确规则由AT-011/AT-012覆盖。
8. Cross-wall drag：把已有 item 从一面墙移到另一面墙后 Confirm；invalid placement 不得改变 confirmed layout。
9. Store：Dismiss 后 item 保留，Confirm 后 item 消失；重新载入 Scene 后，本次 session Confirm 的 Window 不会恢复。
10. Fade：墙挡住 selected wall item 时应淡化；退出、取消、切换 target 后 Material/opacity 恢复。
11. Responsive：检查普通 portrait、窄 portrait、landscape；Confirm/Cancel 可见、可点击、不重叠 safe area。
12. Exit discard：有未确认 Preview 时退出，选择 Continue 应保持编辑；选择 Discard 应恢复进入 Decoration Mode 前状态。
13. 最终 Console：无 unexpected Error / Exception / unexplained Warning；退出后 Game Time 正常恢复。

## 6. Manual review evidence

技术截图位于：

- `outputs/phase7-task9/`
- `outputs/phase7-task10/`
- `outputs/phase7-task11/`（Task 11 生成后）

截图只能证明画面被正确生成，不能替代 Studio Owner 对模型比例、材质、纹理接缝、fade opacity 和 Bottom Sheet tuning 的视觉验收。

## 7. 如何报告 bug

请提供：

1. 对应 checklist 编号和当前 Mode。
2. 从进入 Play Mode 开始的最短复现步骤。
3. Expected Result 与 Actual Result。
4. Scene、screen orientation/resolution、所选 item。
5. 截图或短视频。
6. Console 完整 Error/Warning 与 stack trace；不要只写“不能用”。
