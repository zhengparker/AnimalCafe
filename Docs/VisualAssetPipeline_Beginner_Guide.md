# Phase 3 Visual Asset Pipeline Beginner Guide

> 状态：`In Review`。自动化验证已经记录；Camera manual review 和 asset license/use-right confirmation 仍由 Studio Owner 完成。

先记住一个容易混淆的例子：咖啡机的 **Collider** 是一个大致包住咖啡机的透明简单盒子，方便点击和碰撞判断；**Grid Occupancy** 则是以后摆放家具时决定它占用哪些地面格子的规则。它们不是同一件事：Collider 不能决定家具占几格，Grid Occupancy 也不能代替点击范围。

## 1. 用三个家具解释这条 pipeline

这次只用三个 benchmark assets 验证流程：Work Table、Coffee Machine、Ceramic Cup。它们不是 Phase 4 的正式家具套装，也没有接入 gameplay 或摆放系统。

流程是：保留可信原始 Model → 导出 FBX → Unity import → 组装 Prefab → Validator 检查 → 在独立 Camera Scene 观察。

三个物体分别帮助检查不同风险：桌子检查大件尺寸和桌面空间；咖啡机检查正面方向、Collider 和 LOD；杯子检查小物体在远近 Camera 下是否还能辨认。

## 2. Tripo、Blender、FBX、Unity 和 Prefab 分别做什么

- **Tripo / Raw source**：本次用户重新提供的原始 `.blend` 是三件 benchmark 的 authoritative LOD0 source。
- **Blender**：保留与 Raw byte-identical 的 `Blender/SM_Benchmark_*.blend`，导出 LOD0；只允许 Coffee Machine 的 LOD1 使用独立简化副本。
- **FBX**：把 Model 带进 Unity 的交换格式。
- **Unity**：读取 FBX、引用共享 Material，并配置 Prefab、Collider、ForwardMarker 与 Coffee 的 LODGroup。
- **Prefab**：可重复放进 Scene 的已配置物件模板。

本次 Owner override 不允许自动保存、清理、重建、retopology、decimate 或 apply transform 到原始 LOD0。需要的轴向/尺寸适配只在 Unity 的 `Visual` child 或 import metadata 上完成，Prefab root 仍保持 identity Transform。

## 3. Grid 尺寸和 Model 尺寸

Model 尺寸是可见物体在 Unity 世界中的宽/高/深（W/H/D），用于看比例、Collider 和 Camera 可读性。本次最终尺寸为：

| Asset | Unity W/H/D |
|---|---|
| Work Table | `0.90 / 0.65 / 0.90 m` |
| Coffee Machine | `0.65 / 0.62 / 0.50 m` |
| Ceramic Cup | `0.14 / 0.16 / 0.14 m` |

Grid Occupancy 是 Layout 系统未来根据 `FurnitureDefinition` 和 `GridSize` 计算的地面格子占用。本 Phase 没有把这三个 benchmark 接到正式 placement，也没有定义它们的正式 footprint；以后做 placement 时，要单独把视觉 Model 尺寸和 Grid footprint 对照确认。

## 4. Pivot、Forward 和 Transform

**Pivot** 是旋转和摆放时使用的参考点；本流程要求物件底部对齐地面（最低点 `Y = 0`）。**Forward** 是物件“正面朝哪边”；Prefab root 上的 `ForwardMarker` 用 root-local `+Z` 表示 Unity 中的可见正面。

Prefab root 的 position/rotation 为 zero、scale 为 one。不要用 root scale 偷偷补尺寸；本次需要的尺度与轴向调整位于可见 `Visual` child，因而不会改写 authoritative Blender source。

## 5. Naming 与 Folder

生产 Model 位于 `Assets/Art/VisualPipeline/Benchmarks/Models/`，共享 Material 位于 `Assets/Art/VisualPipeline/Benchmarks/Materials/`，Prefab 位于相应 `Prefabs/` folder。Raw 与 Blender source 留在 `ArtSource/VisualPipeline/Benchmarks/`，不直接拖进 `Assets/`。

Validator 依赖固定命名和路径来找到三个 Prefab：`PF_Benchmark_WorkTable_01`、`PF_Benchmark_CoffeeMachine_01`、`PF_Benchmark_CeramicCup_01`。随意改名或移动文件会让验证变红（RED）。

## 6. Material、Texture 和 Shader

这三个 Prefab 使用共享、Opaque 的 URP `Lit` Material，不使用 custom Shader 或透明 Material。实际结果：Work Table 有 1 个 unique Material/slot，Coffee Machine 的 LOD0 有 2 个 unique slots、LOD1 有 1 个，Ceramic Cup 有 1 个；三个 benchmark 都有 0 个 Texture references。

规则仍允许单张 Texture 最大 `512 × 512`，但“允许”不等于“本次一定使用”。如果将来加 Texture，必须检查它没有超过这个上限，也没有出现 missing reference 或粉红色 Material。

## 7. Collider 是透明的简单包围盒

Collider 不是 Model 的精确复制品，也不是 Grid 占用数据。它是接近物体外形的简单、透明碰撞体，便于点击/碰撞判定并控制性能。

本次每个 benchmark Prefab 都有 1 个 enabled、non-trigger `BoxCollider`：Table `.90/.65/.90`、Coffee `.65/.62/.50`、Cup `.14/.16/.14`。不使用 `MeshCollider`；Coffee 的两级 LOD 也不会各自增加一套 Collider。

## 8. Triangle、LOD 与 Mobile Budget

Triangle 越多，GPU 需要处理的几何越多。Phase 3 的 Owner-approved LOD0 上限统一是 `6,000`：`6,000` 必须通过，`6,001` 必须被 Validator 拒绝。

实际 imported triangle counts 是：Work Table `4,790`，Coffee Machine LOD0 `4,607`，Coffee Machine LOD1 `2,073`（LOD0 的 `45.0%`），Ceramic Cup `4,768`。Coffee 的 LOD1 必须同时不超过 `2,500` triangles 和 LOD0 的 `60%`，并在切换时没有明显位置、大小或 Material 跳动。

## 9. Validator 的 RED 与 GREEN

**RED** 不是坏事：故意让规则失败，证明 test 确实能发现问题。例如把 triangle 从 `6,000` 改成 `6,001`，应收到 `TriangleBudgetExceeded`。

**GREEN** 表示真实 Prefab 通过同一套规则。本次 production batch validator 的结果是 `3 / 3` 个 benchmark Prefab valid、`0 issues`。Validator 会检查路径、root Transform、可见 bounds、ForwardMarker、Material/Texture、Collider、Coffee LOD 和 missing references；它不会替你自动修复资产。

## 10. Camera Readability Manual Test

打开独立的 `Assets/Scenes/Validation/AssetPipelineReadability.unity`，不要修改 `MainCafe.unity`。Studio Owner 需要亲自检查：

1. orthographic size `4`：主要细节、材质差异和正面是否可读；
2. size `7`：桌子、咖啡机、杯子能否立刻区分；
3. size `12`：桌子和咖啡机是否仍可辨认，杯子 silhouette 是否稳定；
4. `1.30 m` Character Scale Reference 是否让三者比例容易理解；
5. Coffee Machine 放在 Work Table 上是否仍有桌面余量；
6. Coffee LOD switch 是否没有明显 size、position 或 Material jump；
7. Game view `1920 × 1080` 和 portrait `1170 × 2532` 是否都没有遮住物件；
8. batch display 是否没有粉红 Material、missing Mesh、异常 Collider 或 Console error。

这项 manual acceptance 目前是 **Pending Studio Owner review**；自动化 bounds test 不能替代视觉判断。

## 11. Phase 3 没有做什么

Phase 3 只验证 visual asset pipeline。它没有开始 Phase 4，没有正式功能家具套装，没有 runtime gameplay、正式 placement / Grid Occupancy integration、Interaction Anchor、角色 Rig、完整 UI 或大批量资产生产。

## 12. Beginner Glossary

| Term | 简单解释 |
|---|---|
| Asset | 游戏中可重复使用的 Model、Material、Texture 等资源。 |
| Pipeline | 资源从 source 到游戏内可用 Prefab 的固定步骤。 |
| Prefab | 已配置好、可以反复放进 Scene 的模板。 |
| FBX | DCC 工具与 Unity 之间常用的 Model 交换格式。 |
| Pivot | 物体旋转、摆放时的参考点。 |
| Forward | 物体认为的正面方向。 |
| Collider | 用于点击/碰撞的简单形状，不等于 Model 或 Grid。 |
| Grid Occupancy | 家具占用哪些地面格子的 Layout 数据。 |
| Triangle | Model 的几何面数单位。 |
| LOD | 远处使用更简单 Model 的等级系统。 |
| Validator | 只读检查器；报告问题，不自动改资产。 |
| RED / GREEN | 先看到会失败的正确 test，再看到修复后的通过结果。 |

## 13. 完成证据和下一步

已记录的实际资产事实包括尺寸、triangle counts、Material slots、0 Texture references、每个 Prefab 1 个 `BoxCollider` 和 Coffee 的 two-level `LODGroup`。当前已验证的自动化结果是：EditMode `285 / 285`、PlayMode `48 / 48` 全部 passed，failed/skipped/inconclusive 均为 `0`；production validator 为 `3 / 3` benchmark Prefabs valid、`0 issues`。

下一步不是启动 Phase 4，而是 Studio Owner 完成第 10 节的 Camera review，并明确确认三份用户提供 source 的 license/use-right。完成两项 pending gate 前，Roadmap 保持 `In Review`。
