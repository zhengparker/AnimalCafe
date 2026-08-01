# AnimalCafe Phase 3 Beginner Guide

> 这是一份面向 Unity 和 coding 初学者的 educational note。
> 它只解释 Phase 3 的 Visual Asset Pipeline，不负责解释 Phase 4 的正式家具制作、gameplay 或 placement integration。
> 当前状态是 `In Review`：automated verification 已通过，Camera/readability manual review 和 source license/use-right confirmation 仍为 `Pending`。

## 1. 用一个简单例子说明本阶段

假设我们要把一台 Coffee Machine 放进游戏。只有“看起来像咖啡机”还不够，它还必须经历一条稳定的 pipeline：

```text
用户提供的原始 Blender source
→ 导出 FBX
→ Unity import
→ 组装 Prefab
→ Validator 检查
→ 在固定 Camera 下人工观察
```

这条 pipeline 的目的，是让以后制作的 Model 都遵守同一套 scale、pivot、forward、Material、Collider、LOD 和 performance rules。

这里有一个最容易混淆的概念：Coffee Machine 的 `Collider` 是一个大致包住物体的透明简单盒子，用于点击或碰撞判断；`Grid Occupancy` 则决定家具占用哪些地面格子。Collider 不决定家具占几格，Grid Occupancy 也不能代替 Collider。它们是两个不同系统。

Phase 3 用 Work Table、Coffee Machine 和 Ceramic Cup 三个 benchmark assets 证明这条 pipeline 可以重复执行。它们是流程样本，不是 Phase 4 的正式家具套装。

## 2. 开发前状态与本阶段目标

Phase 2 已经完成 Grid Occupancy 与 Placement Rules，但项目还没有统一的 visual asset production contract。开发前主要缺少：

- Raw source、Blender source、FBX 与 Unity Prefab 的明确职责；
- Model scale、pivot、forward 和 root Transform 标准；
- shared Material、Texture、Collider 与 LOD budget；
- 自动检查错误资产的 Validator；
- 固定 Camera 下的 readability Scene 与 manual checklist。

Phase 3 的目标是建立这些基础，并用三个真实 benchmark assets 验证。完成本阶段后，未来资产可以复用同一套规则，但还不能自动视为 Phase 4 正式内容。

## 3. Phase 3 做了什么改动

### 3.1 建立 protected original LOD0 source contract

Studio Owner 指定三份 user-resupplied `.blend` 为 authoritative original LOD0。对应 `Raw/` 与 `Blender/SM_Benchmark_*.blend` 保持 byte-identical，并以 SHA-256 检查相等。

这些 protected original LOD0 不进行自动保存、重建、retopology、decimate、normal repair 或 source Transform 修改。若原始 LOD0 出现 shape、topology、pivot、axis 或 forward 问题，应停下并请求 Studio Owner direction。

允许的例外只有：

- Coffee Machine 的独立 LOD1 derivative 可以单独简化；
- 未来由 Studio Owner 另行批准的 editable source 可以按自己的 source contract 编辑；
- benchmark 的 axis/dimension adaptation 只放在 Unity `Visual` child 或 import metadata，Prefab root 保持 identity。

### 3.2 建立 Unity asset 与 Prefab contract

三个 FBX 使用固定路径和命名导入 Unity。Prefab root 的 position/rotation 为 zero、scale 为 one，底部落在 `Y = 0`，并用 `ForwardMarker` 表示 root-local `+Z` 正面。

每个 Prefab 使用 shared Opaque URP `Lit` Material、一个 non-trigger `BoxCollider`。Coffee Machine 另外有 two-level `LODGroup`。本阶段没有给 benchmark Prefab 添加 gameplay script 或 Interaction Anchor。

### 3.3 建立只读 Validator

`BenchmarkAssetValidator` 检查真实 production Prefab 的路径、命名、Transform、bounds、floor alignment、forward、Mesh、Material、Texture、Collider、triangle budget 与 LOD。它只报告 issue，不会自动改动 production asset。

Unity menu 是：

```text
AnimalCafe > Validation > Validate Benchmark Assets
```

### 3.4 建立独立 readability Scene

Phase 3 使用独立 Scene：

```text
Assets/Scenes/Validation/AssetPipelineReadability.unity
```

它包含三个单独展示的 benchmark、一个 `1.30 m` Character Scale Reference，以及每种 Prefab 各 20 个的 batch display。它不修改 `Assets/Scenes/MainCafe.unity`。

## 4. 重要概念解释

### Pipeline

Asset 从原始 source 变成 Unity 可用 Prefab 的固定步骤。步骤越明确，越容易找到错误发生在哪一层。

### Source、FBX 与 Prefab

- `Raw source`：用户提供的原始文件，用来证明来源与原始内容。
- `Blender source`：本次 benchmark 的 authoritative LOD0 copy，与对应 Raw byte-identical。
- `FBX`：把 Mesh 从 DCC tool 带进 Unity 的交换格式。
- `Prefab`：在 Unity 中组装好的可重复使用模板，包含 Renderer、Material、Collider、ForwardMarker 与必要的 LODGroup。

### Pivot、Forward 与 Transform

`Pivot` 是物体旋转和摆放时的参考点。Phase 3 要求可见物体最低点在 `Y = 0`。

`Forward` 是物体认为的正面。Unity Prefab 用 root-local `+Z` 作为正面，并由 `ForwardMarker` 声明。

Prefab root 必须保持 identity Transform。需要调整 benchmark 尺寸或轴向时，只调整 `Visual` child/import metadata，不能用 root scale 掩盖问题，也不能改写 protected original LOD0。

### Collider 与 Grid Occupancy

`Collider` 是用于点击/碰撞的简单 3D 范围；`Grid Occupancy` 是 Layout data 中的地面格子占用。一个 Collider 可以大致包住 Model，但它不等于 Model，也不等于家具 footprint。

### Material、Texture 与 Shader

三个 benchmark 使用 Opaque 的 URP `Lit` Material。Studio Owner 要求保留 Blender original colors，因此每件家具都使用自己的 original-color Material，以及从 authoritative `.blend` packed image repeatably 导出的 `512 × 512` sRGB Base Color Texture。

生产 Material 都使用白色 tint、`Metallic = 0`、`Smoothness = 0.5`，让 Texture 原色直接显示：

- Work Table：橙色木纹与黑色细节；
- Coffee Machine：浅蓝、白色与黑色分区；
- Ceramic Cup：柔和绿色。

`CharacterScaleReference_1_30m` 不复用家具 Material。它使用专用青绿色 `#157A78` Material，方便在浅黄色背景上快速识别。Coffee Machine 的 LOD0 和 LOD1 使用同一份 Material 与 Texture，避免 LOD 切换时突然换色。

### Triangle 与 LOD

Triangle 是 Mesh 几何复杂度的基本计数。Owner-approved LOD0 上限统一为 `6,000`：`6,000` 必须通过，`6,001` 必须失败。

`LOD` 会在物体变远时切换到更简单的 Mesh。Coffee Machine LOD1 必须同时满足：

- 不超过 `2,500` triangles；
- 不超过 LOD0 的 `60%`；
- 切换时没有明显 size、position 或 Material jump。

## 5. 三个 benchmark assets 的实际数据

以下是 Unity 最终 imported/assembled 结果，不是只有目标值：

| Asset | Unity W/H/D | Triangles | Material slots | Texture references | Collider | LOD |
|---|---|---:|---:|---:|---|---|
| Work Table | `0.90 / 0.65 / 0.90 m` | LOD0 `4,790` | `1` | `1` (`512 × 512`, sRGB) | `1` enabled non-trigger `BoxCollider` | 不要求 |
| Coffee Machine | `0.65 / 0.62 / 0.50 m` | LOD0 `4,607`; LOD1 `2,073` (`45.0%`) | LOD0 `1`; LOD1 `1` | `1` (`512 × 512`, sRGB; LOD0/1 shared) | `1` enabled non-trigger `BoxCollider` | one two-level `LODGroup` |
| Ceramic Cup | `0.14 / 0.16 / 0.14 m` | LOD0 `4,768` | `1` | `1` (`512 × 512`, sRGB) | `1` enabled non-trigger `BoxCollider` | 不要求 |

每个 Renderer 的 Material slot 数与 imported Mesh 的 submesh 数一致。Coffee LOD1 是独立 derivative；三个 LOD0 仍是 Studio Owner 指定的 byte-identical original sources。

## 6. Tests 与 Validator：正常结果

Automated tests 不只检查临时假物件，也检查真实 production Prefab 和独立 readability Scene。

当前已验证结果：

| Verification | Result |
|---|---|
| Full EditMode | `302 / 302` passed |
| Full PlayMode | `50 / 50` passed |
| Focused AssetPipeline EditMode | `111 / 111` passed |
| Failed / Skipped / Inconclusive | 全部 `0` |
| Production Validator | `3 / 3` benchmark Prefabs valid；`0 issues` |

Validator 正常 GREEN 时，Console 应出现：

```text
Benchmark asset validation passed: 0 issues.
```

这些 automated results 能证明规则、引用和 Scene contract 正常，但不能代替 Studio Owner 对视觉比例、readability、LOD jump 和 license/use-right 的人工判断。

## 7. Bug 与 Edge Case Tests

Phase 3 tests 会故意建立错误 fixture，确认 Validator 真的能把问题找出来。重要 cases 包括：

- 错误 folder、filename、prefix 或 asset path；
- Prefab root position/rotation/scale 不是 identity；
- visible bounds 超出 tolerance，或物体高于/低于地面；
- `ForwardMarker` 缺失、方向错误或带 Renderer；
- missing Mesh、missing Material、null Material slot 或 missing script；
- Material slot 数与 Mesh submesh 数不一致；
- 非 URP `Lit`、透明 Material 或 broken serialized Texture reference；
- `512 × 512` Texture 通过、`1024 × 1024` Texture 失败；
- 三种 LOD0 都验证 `6,000` pass / `6,001` fail；
- Coffee 缺少 LODGroup、缺少 LOD1、LOD1 超过 `2,500`、超过 LOD0 `60%` 或重复使用无意义 Mesh；
- `MeshCollider`、trigger Collider、Collider 数量过多或 bounds 明显超出 Model；
- batch validation 遇到第一个错误后仍继续报告其他资产；
- readability Scene 重复生成不会增加重复 roots，且不会修改 `MainCafe.unity`；
- PlayMode 临时修改 Build Settings 后会恢复原始 scene list，即使发生 exception 也会 cleanup。

RED 表示 test 故意证明错误会被发现；GREEN 表示最小规则完成后正确 fixture 与真实 production asset 通过。不能为了让测试变绿而放宽已经批准的 budget。

## 8. Phase 3 Files

### Documentation 与 provenance

```text
Docs/VisualAssetPipeline_Beginner_Guide.md
Docs/superpowers/specs/2026-07-31-phase-3-visual-asset-pipeline-design.md
Docs/superpowers/plans/2026-07-31-visual-asset-pipeline.md
ArtSource/VisualPipeline/Benchmarks/AssetProvenance.md
```

### Authoritative sources 与 tools

```text
ArtSource/VisualPipeline/Benchmarks/Raw/WorkTable/SM_Benchmark_WorkTable_01_user_resupplied_original.blend
ArtSource/VisualPipeline/Benchmarks/Raw/CoffeeMachine/SM_Benchmark_CoffeeMachine_01_user_resupplied_original.blend
ArtSource/VisualPipeline/Benchmarks/Raw/CeramicCup/SM_Benchmark_CeramicCup_01_user_resupplied_original.blend
ArtSource/VisualPipeline/Benchmarks/Blender/SM_Benchmark_WorkTable_01.blend
ArtSource/VisualPipeline/Benchmarks/Blender/SM_Benchmark_CoffeeMachine_01.blend
ArtSource/VisualPipeline/Benchmarks/Blender/SM_Benchmark_CeramicCup_01.blend
ArtSource/VisualPipeline/Benchmarks/Tools/CreateBenchmarkSources.py
ArtSource/VisualPipeline/Benchmarks/Tools/AuditBenchmarkAssets.py
ArtSource/VisualPipeline/Benchmarks/Tools/ExportBenchmarkTextures.py
```

### Unity Models、Materials 与 Prefabs

```text
Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_WorkTable_01.fbx
Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CoffeeMachine_01.fbx
Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CeramicCup_01.fbx
Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_WorkTableOriginal_01.mat
Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CoffeeMachineOriginal_01.mat
Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CeramicCupOriginal_01.mat
Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CharacterReferenceAccent_01.mat
Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_WorkTable_BaseColor_01.png
Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_CoffeeMachine_BaseColor_01.png
Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_CeramicCup_BaseColor_01.png
Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_WorkTable_01.prefab
Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_CoffeeMachine_01.prefab
Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_CeramicCup_01.prefab
```

### Validator、Scene 与 tests

```text
Assets/Editor/AssetPipeline/BenchmarkAssetRules.cs
Assets/Editor/AssetPipeline/BenchmarkAssetValidator.cs
Assets/Editor/AssetPipeline/BenchmarkAssetValidationMenu.cs
Assets/Editor/AssetPipeline/AssetPipelineReadabilitySceneSetup.cs
Assets/Scenes/Validation/AssetPipelineReadability.unity
Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetRenderingBudgetTests.cs
Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorContractTests.cs
Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorReviewTests.cs
Assets/Tests/EditMode/AssetPipeline/AssetPipelineReadabilitySceneSetupTests.cs
Assets/Tests/PlayMode/AssetReadability/AssetPipelineReadabilityTests.cs
Assets/Tests/PlayMode/AssetReadability/AssetPipelineReadabilityBuildSettingsScopeTests.cs
```

## 9. Unity Manual Test

这部分必须由 Studio Owner 在 Unity 中亲自完成。建议每做完一项，就在下方 checklist 的 Result 栏填写 `PASS` 或记录具体问题。

### 9.1 打开正确项目与运行 Validator

1. 打开 Unity Hub。
2. 选择这个精确 project folder：

   ```text
   E:\Unity\Project\AnimalCafe\.worktrees\phase-3
   ```

3. 确认 Editor version 是 Unity `6000.5.5f1`。如果 Unity 提示用其他版本升级，先停止，不要转换项目。
4. 等待 Unity import 完成，确认右下角没有正在进行的 compile/import。
5. 打开 `Window > General > Console`，点击 `Clear`。
6. 从顶部 menu 选择 `AnimalCafe > Validation > Validate Benchmark Assets`。
7. PASS 条件：Console 出现绿色 `Benchmark asset validation passed: 0 issues.`，没有红色 error；这代表 `3 / 3` benchmark Prefabs valid。

### 9.2 打开 readability Scene 并确认 Hierarchy

1. 在 Project window 打开：

   ```text
   Assets/Scenes/Validation/AssetPipelineReadability.unity
   ```

2. Hierarchy 应只有一个 Scene root：`AssetReadabilityRoot`。
3. 展开后应看到：

   ```text
   AssetReadabilityRoot
   ├─ CameraRoot
   │  └─ Main Camera
   ├─ SingleAssetDisplay
   │  ├─ PF_Benchmark_WorkTable_01
   │  ├─ PF_Benchmark_CoffeeMachine_01
   │  ├─ PF_Benchmark_WorkTable_01
   │  ├─ PF_Benchmark_CeramicCup_01
   │  └─ CharacterScaleReference_1_30m
   └─ BatchDisplay
      ├─ WorkTables_20
      ├─ Machines_20
      └─ Cups_20
   ```

4. PASS 条件：上述 roots 各一个，没有 duplicate、missing script 或空的 Prefab reference。

### 9.3 在 Play Mode 检查 Camera、比例与 LOD

1. 点击 Unity 顶部 Play button。确认按钮变蓝后再改临时测试值。
2. 先观察 Game view background。它应是浅黄色 `#F2E6B8`；这个颜色本身是设计结果，不算 failure。只有它让物体被裁切、颜色 washed out 或难以分辨时才记录 failure。
3. 选中 `Main Camera`，确认 Camera 使用 `Solid Color`；在附加的 Universal camera data 中确认 antialiasing 是 `SMAA`、Quality 是 `High`，并确认 Camera 的 `Post Processing` 已勾选。少了这个勾选，虽然 Inspector 写着 SMAA，锯齿处理也不会真正执行。这些都只是本 Scene 的设置，不应要求修改 global URP/Quality settings。
4. 确认 `CharacterScaleReference_1_30m` 使用明显的青绿色，并位于右桌更右侧（Scene local `X = 2.50`）。PASS 条件不只是 world position 分开：从 Game Camera 看，它也不能与右桌或 Cup 的轮廓重叠。
5. 检查 original colors：Work Table 应是橙色木纹/黑色细节，Coffee Machine 应有浅蓝/白/黑分区，Ceramic Cup 应是柔和绿色。若仍是旧的统一纯色 palette，应记录 failure。
6. 在 Inspector 找到 Camera component 的 `Orthographic Size`，依次输入 `4`、`7`、`12`：
   - size `4`：能看清主要功能细节、Material 差异与物体正面；
   - size `7`：能立即区分 Work Table、Coffee Machine 与 Ceramic Cup；
   - size `12`：Work Table 与 Coffee Machine 仍可辨认，Cup silhouette 保持稳定。
7. 比较 `CharacterScaleReference_1_30m`：PASS 条件是它确实提供 `1.30 m` 角色比例参考，桌子、咖啡机和杯子的相对大小容易理解，没有明显“杯子像家具”或“机器像玩具”的比例错误。
8. 观察两张桌子：PASS 条件是左桌只放 Coffee Machine，右桌只放 Ceramic Cup；两件物品都位于各自桌面中央，彼此没有前后遮挡。Coffee Machine 完整位于桌面范围内，四周仍能看到明显桌面余量。
9. 选中 Coffee Machine，确认有一个 two-level `LODGroup`。通过 Camera zoom 或 LOD preview 观察 LOD0/LOD1 切换：PASS 条件是没有明显 size、position、pivot、silhouette、Material 或 Texture jump。

### 9.4 检查 Game view resolution 与 batch display

1. 在 Game view resolution 下拉菜单选择或添加 `1920 × 1080`。PASS 条件：三个 single-display objects 与 `1.30 m` reference 可读，没有被画面边缘遮住。
2. 再选择或添加 portrait `1170 × 2532`。PASS 条件：物体仍在可观察范围内，没有因为窄屏完全消失或互相遮住。
3. 展开 `BatchDisplay`，确认 `WorkTables_20`、`Machines_20`、`Cups_20` 每组各有 `20` 个 benchmark Prefab，总数正好 `60`。
4. 观察 batch：PASS 条件是物体之间没有 overlap，没有粉红 Material、missing Mesh、异常大小或明显跳位。
5. 在 Scene view 打开 `Gizmos`，逐组选中一些 Prefab 查看 Collider：PASS 条件是每个物体只有一个大致包住可见 Model 的 `BoxCollider`，没有异常巨大、落到地面下方或变成 `MeshCollider`。

### 9.5 Console、退出与 license statement

1. 返回 Console。PASS 条件：没有 unexpected red error、missing reference、missing script 或 repeated exception。
2. 点击蓝色 Play button 退出 Play Mode。
3. 不要保存 Play Mode 中实验性的 Camera size、Transform、LOD preview 或其他临时变化。如果 Unity 询问是否保存实验性 Scene 修改，选择不保存，并在不确定时停止记录问题。
4. Studio Owner 单独确认 source license/use-right。可以使用这句话记录：

   ```text
   我确认对三份 user-provided benchmark source 拥有用于 AnimalCafe 开发与发布所需的使用权：是 / 否 / 需要进一步确认
   ```

这不是法律判断模板；如果选择“否”或“需要进一步确认”，license gate 继续保持 `Pending`。

### 9.6 Manual checklist

| # | Item | Action | PASS condition | Result |
|---:|---|---|---|---|
| 1 | Project | 用 Unity `6000.5.5f1` 打开精确 phase-3 worktree | 没有升级或打开错误 checkout | |
| 2 | Validator | 运行 menu validator | `3 / 3` valid、`0 issues`、无红色 error | |
| 3 | Scene/Hierarchy | 打开 exact readability Scene 并展开 roots | hierarchy 与上方结构完全一致 | |
| 4 | Background | 检查 SolidColor `#F2E6B8` | 浅黄色存在；无 clipping、washout 或 readability loss | |
| 5 | SMAA | 检查 Main Camera additional data | scene-only `SMAA High` 且 `Post Processing` 已勾选；未要求改 global settings | |
| 6 | Reference contrast | 观察青绿色 `1.30 m` reference | 与背景和家具明显区分 | |
| 7 | Original colors | 对照三件家具 | Table 橙木/黑、Machine 浅蓝/白/黑、Cup 柔和绿 | |
| 8 | Camera size 4 | Play Mode 设置 size `4` | 细节、Material、正面可读 | |
| 9 | Camera size 7 | 设置 size `7` | 三个 assets 可立即区分 | |
| 10 | Camera size 12 | 设置 size `12` | Table/Machine 可辨认；Cup silhouette 稳定 | |
| 11 | Character scale | 比较 `1.30 m` reference | 三件 assets 的相对比例合理 | |
| 12 | Two tabletop stations | 观察两张桌子 | 左桌 Machine、右桌 Cup；各自居中、互不遮挡，Machine 四周仍有桌面余量 | |
| 13 | Coffee LOD | 检查 two-level LODGroup 与切换 | 无 size/position/pivot/Material/Texture jump | |
| 14 | Landscape | Game view `1920 × 1080` | 物件可读且未被遮住 | |
| 15 | Portrait | Game view `1170 × 2532` | 物件仍可观察且未互相遮住 | |
| 16 | Batch 60 | 检查三组各 20 | 正好 60、无 overlap/pink/missing/异常 Collider | |
| 17 | Console | 检查完整 manual run | 无 unexpected error 或 missing reference | |
| 18 | Play Mode cleanup | 退出且不保存实验性改动 | 没有把临时 Transform 写入 Scene | |
| 19 | License | 记录 license/use-right statement | 明确填写“是”，否则保持 Pending | |

## 10. Phase 3 没有做什么

Phase 3 没有开始或交付：

- Phase 4 formal asset set；
- 正式 gameplay 或 functional furniture logic；
- 正式 placement / Grid Occupancy integration；
- Interaction Anchors；
- character Rig 或 Animation；
- Decoration UI；
- 大批量 Model production；
- release 或 platform deployment。

因此 benchmark Prefab 还不能被当作完整游戏内容。它们证明的是 pipeline 和 validation baseline。

## 11. Beginner Glossary

| Term | 初学者解释 |
|---|---|
| `Asset` | 游戏中可重复使用的 Model、Material、Texture、Prefab 等资源。 |
| `Pipeline` | Asset 从 source 到游戏内 Prefab 的固定步骤。 |
| `DCC` | 制作 3D 内容的软件类别；Blender 是一种 DCC tool。 |
| `Source` | 可追踪来源并用于导出的原始制作文件。 |
| `FBX` | DCC tool 与 Unity 之间常用的 Model 交换格式。 |
| `Prefab` | 已配置好、可以反复放进 Scene 的 Unity 模板。 |
| `Pivot` | 物体旋转、摆放时使用的参考点。 |
| `Forward` | 物体约定的正面方向；本项目 Unity contract 是 `+Z`。 |
| `Identity Transform` | position/rotation 为 zero，scale 为 one。 |
| `Collider` | 用于点击或碰撞的简单 3D 范围，不等于 Model 或 Grid。 |
| `Grid Occupancy` | 家具占用哪些地面格子的 Layout data。 |
| `Material` | 决定表面颜色、金属感、粗糙/光滑表现的 Unity asset。 |
| `Texture` | 提供表面图案或细节的图片资源。 |
| `Shader` | 告诉 GPU 怎样绘制 Material 的规则。 |
| `Triangle` | Model 几何复杂度的基本计数单位。 |
| `LOD` | 物体变远时切换到更简单 Mesh 的等级系统。 |
| `Validator` | 只读检查器；报告 issue，不自动修改 production asset。 |
| `RED / GREEN` | 先证明错误会被 test 发现，再证明正确实现通过。 |
| `Regression` | 新改动意外破坏已经正常的旧功能。 |

## 12. 完成证据、Manual Result Template 与下一道 Gate

当前 verified automated evidence：

- EditMode `304 / 304` passed；
- PlayMode `52 / 52` passed；
- focused AssetPipeline EditMode `111 / 111` passed；
- failed、skipped、inconclusive 全部为 `0`；
- production validator `3 / 3` valid、`0 issues`；
- Camera-projected overlap RED `0 / 1`，修复后 focused EditMode 与 PlayMode 均为 `1 / 1` passed；
- authored Guide 的 placeholder/static scan 与 `git diff --check` 是文档 closeout gate；Unity-generated YAML 不在本次 Guide rewrite scope 内机械格式化。

当前 manual 状态：

- Camera/readability manual review：`Pending Studio Owner`；
- source license/use-right confirmation：`Pending Studio Owner`；
- Roadmap Phase 3：`In Review`；
- Phase 4：未开始。

Studio Owner 完成第 9 节后，可以复制下面模板填写：

```text
Phase 3 Manual Result
Date:
Unity version: 6000.5.5f1
Project folder: E:\Unity\Project\AnimalCafe\.worktrees\phase-3
Validator 3/3 valid, 0 issues:
Hierarchy exact:
Background #F2E6B8 has no clipping/washout/readability issue:
Main Camera scene-only SMAA High:
Teal Character Scale Reference has clear contrast:
Original colors match Blender sources:
Camera size 4:
Camera size 7:
Camera size 12:
Character 1.30 m proportions:
Coffee Machine fits on Work Table with remaining space:
Coffee LOD two-level switch has no visible Material/Texture jump:
Game view 1920 × 1080:
Game view 1170 × 2532:
Batch exactly 60, no overlap/pink/missing/abnormal Collider:
Console clean:
Experimental Play Mode transforms not saved:
License/use-right statement:
Overall result: Approved / Needs Revision
Notes:
```

下一道 gate 是 Studio Owner 提交上述 manual result 和 license statement。只有这两项通过后，才能决定是否把 Roadmap Phase 3 从 `In Review` 改为 `Completed`。本 Guide 不授权开始 Phase 4、push 或 merge。
