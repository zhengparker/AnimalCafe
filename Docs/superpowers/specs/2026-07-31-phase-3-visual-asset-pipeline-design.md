# AnimalCafe Phase 3 — Visual Style & Asset Pipeline Foundation 设计

> 状态：Studio Owner 已批准（包含 `1.30 m` Character Scale Reference 与 revised furniture dimensions）
>
> Roadmap 正式名称：Phase 3 — Visual Style & Asset Pipeline Foundation
>
> 验证环境：Unity `6000.5.5f1` / URP `17.5.0` / Windows
>
> 目标兼容平台：未来 iOS

## 1. 先用一个简单例子说明 Phase 3

假设 Work Table、Coffee Machine 和 Ceramic Cup 分别由不同时间、不同工具制作：

- Table 在 Blender 里很大，进入 Unity 后只能缩小到 `0.01`；
- Coffee Machine 的正面朝向错误；
- Cup source 内有独立的 2K packed Texture，但 production budget 只允许 `512 × 512`；
- 三件物品的 pivot 高低不同；
- Prefab 用不同 Material，导致批量摆放时成本不断增加。

单独看每一件资产都可能“勉强能用”，但组合起来会出现大小、方向、材质与性能不一致的问题。后续每增加一件家具，都要重复修理。

Phase 3 的任务是先建立一条可重复的生产流水线：

```text
approved source contract
→ Blender audit / allowed cleanup
→ packed Texture repeatable export
→ FBX export
→ Unity import
→ original-color Material
→ Prefab assembly
→ automated validation
→ Camera readability manual review
```

三个 benchmark assets 不是正式的大批量家具内容。它们用于证明这套标准真的可以工作，并让后续资产使用同一套规则。

## 2. Goal 与 Player-visible Result

### 2.1 Goal

建立正式 Models、Materials、Textures 和 Unity Prefabs 共用的视觉方向、技术 contract、验证工具和可重复生产流程。

### 2.2 Player-visible Result

Phase 3 完成后，玩家可以在验证 Scene 中看到：

- 一张保留原始橙木与黑色细节的 Work Table；
- 一台保留原始浅蓝、白色与黑色分区，且正面清晰的 Coffee Machine；
- 一个保留原始柔和绿色、在固定 isometric Camera 下仍有稳定轮廓的 Ceramic Cup；
- 三件资产在近、中、远 Camera zoom 下保持统一比例与视觉风格。

Phase 3 不加入 Decoration Mode、鼠标摆放、功能设备逻辑或正式 gameplay。

## 3. Confirmed Visual Direction

### 3.1 Art Direction

采用 `A2 + P1`：

- `A2`：圆润轮廓，但保留清楚的功能细节；
- `P1`：奶油色、暖木色、鼠尾草绿，少量蜂蜜黄作为强调色；
- 整体气质是“温暖手作童话”，但不过度幼稚；
- Model 在固定 `3/4 isometric-like Camera` 下优先保证 silhouette 和用途可读性；
- 小装饰不能依赖近距离观察才能理解主体用途。

### 3.2 Surface Language

- 以 matte 表面为主；
- 木、陶瓷和金属主要通过颜色与适量 Smoothness 差异区分；
- 不使用 photorealistic、脏污密集或高频噪点 Texture；
- 不使用 baked lighting 或 baked shadow 假装 Scene 光照；
- 不在 benchmark 阶段制作自定义 Shader。

## 4. Phase Scope

### 4.1 Included

- Art direction、基础 color palette 与 shape language；
- Grid 与 Unity/DCC scale convention；
- Model dimensions、pivot、forward direction 和 rotation contract；
- Tripo provenance、raw export 与 authoritative-source editing rules；
- Blender source、FBX export 与 Unity import 规则；
- folder、file、Model、Material 和 Prefab naming；
- URP Material、Texture 和 shader baseline；
- primitive Collider 与 LOD policy；
- mobile-oriented asset budgets；
- Prefab assembly contract；
- Editor validator 与 automated tests；
- Camera readability validation Scene；
- 三个 benchmark assets：Work Table、Coffee Machine、Ceramic Cup；
- source → export → import → prefab 的可重复性验证；
- Phase 3 Beginner Guide 和 manual acceptance checklist。

### 4.2 Not Included

- 大量正式家具或建筑 Model；
- Phase 4 的正式 functional furniture set；
- 角色 Model、Rig 或 Animation；
- Decoration Mode、mouse placement 或 placement preview；
- gameplay function、Interaction Anchors、Surface Slot 或 pathfinding；
- 完整 UI 页面；
- 自定义 Shader、VFX 或正式 lighting pass；
- 保存家具布局；
- 修改 Phase 1/2 Layout 或 Grid rules；
- 固定所有未来资产必须使用 Tripo。

## 5. Scale 与 Model Dimensions

### 5.1 Grid Contract

沿用 Phase 1 已批准的标准：

```text
1 Grid cell = 1 Unity world unit = 1 meter
```

Phase 3 不改变 `GridSettings.CellSize`，也不增加新的 world-space placement system。

### 5.2 Benchmark Target Dimensions

| Asset | Target size（宽 × 深 × 高） | Validation tolerance | Placement meaning |
|---|---|---:|---|
| Work Table | `0.90 × 0.90 × 0.65 m` | 约 `±5%` | `1 × 1` employee-work-surface benchmark |
| Coffee Machine | `0.65 × 0.50 × 0.62 m` | 约 `±10%` | furniture-surface prop benchmark |
| Ceramic Cup | `0.14 × 0.14 × 0.16 m` | 约 `±10%` | small-prop readability benchmark |

这些数值是视觉和验证目标，不要求 Model 与小数点完全相同。硬性要求是：

- Work Table 完整位于 `1 × 1` cell 内；
- Coffee Machine 完整位于 Work Table 顶面内，并保留明显桌面余量；
- Cup 不得大到接近 Coffee Machine 的主要操作部件；
- 三者组合后的比例必须由 Studio Owner 在 Camera validation Scene 中确认；
- Unity Prefab root scale 必须为 `(1, 1, 1)`，不能依赖 Transform scale 修正 source size。

### 5.3 Character-to-Furniture Scale Reference

P3 使用 `1.30 m` 高的标准角色作为家具比例基线。当前约 `1.0 m` 高的柴犬 Model 不会在 P3 中被自动缩放；角色建模任务需要在独立 approval 下将 Mesh、Armature 和 Animation 一起调整，并重新验证 Rig、Animation、Collider 与 Camera framing。

Validation Scene 必须包含一个 `1.30 m` 高的 `CharacterScaleReference`。它可以是没有 gameplay、Rig 或 Animation 的简单 silhouette/reference object，其用途只是持续检查：

- `0.65 m` Work Table 大约位于标准角色腰部附近；
- Work Table 上的 `0.62 m` Coffee Machine 顶部总高度约为 `1.27 m`；
- Coffee Machine 正面操作区域对标准角色可读且可触及；
- Ceramic Cup 不会相对角色手掌显得像水桶；
- 家具尺寸不能只在独立展示时看起来合理。

`1.30 m` 是 P3 的 visual scale reference，不在本 Phase 决定 NavMesh Agent、通道宽度、Interaction Anchor 或移动规则。未来相关 Phase 必须使用实际角色 bounds 重新验证这些数据。

## 6. Axis、Pivot 与 Transform Contract

### 6.1 Final Unity Contract

- Unity 使用 `Y Up`；
- Model 正面统一朝 Unity `+Z`；
- Prefab root position 为 `(0, 0, 0)`；
- Prefab root rotation 为 `(0, 0, 0)`；
- Prefab root scale 为 `(1, 1, 1)`；
- 三个 benchmark assets 的 pivot 均为底面中心；
- Model 最低可见点与主要 Collider 最低点不得低于 `Y = 0`；
- 不允许用 Prefab root 的 rotation 或 scale 隐藏 FBX export 错误。

底面中心作为 asset pivot，便于同一个 Model 被 Scene、surface placement 和未来 preview 重用。后续 placement renderer 负责把 Phase 2 的 Grid anchor 换算到 rotated footprint center；Phase 3 不提前实现该 renderer。

### 6.2 Blender 与 FBX Axis

- Blender 使用 `Z Up`；
- Blender source 中物体正面朝 `-Y`；
- 只有 Coffee Machine 的独立 LOD1 derivative 或未来另行批准的 editable source，才可在 export 前应用 Rotation 与 Scale；受保护的 original LOD0 保持不修改；
- FBX export 使用 `Forward -Z`、`Up Y`；
- export 只包含 selected production objects；
- import 后以 Unity `+Z` forward validation 为最终判断标准；
- 对独立 editable source，如果方向不正确，可返回 Blender/source 或 export settings 修正，不在 Prefab root 补偿。受保护的 original LOD0 若出现 axis 或 forward 异常，按 §7.3 停下并请求 Studio Owner direction；允许的 assembly adaptation 仅在 Unity `Visual` child/import metadata，Prefab root 保持 identity。

## 7. Tripo → Blender Source Policy

### 7.1 Tripo Role

Tripo 用于生成造型初稿。Tripo raw export 不是 production-ready asset，不能未经检查直接成为 Unity Prefab。

### 7.2 Provenance

每个生成资产必须记录：

- 生成日期；
- 使用工具为 Tripo；
- prompt 或用户自有参考资料说明；
- 原始导出文件名；
- 由用户确认的使用权或 license 状态；
- 是否包含第三方品牌、logo 或受保护角色。

不允许把来源不明的第三方 Model、品牌 logo 或受保护角色带入 production asset。

### 7.3 Studio Owner Original-LOD0 Override (2026-08-01)

The Studio Owner selected the three user-re-supplied original Blender files as
the authoritative byte-identical benchmark LOD0 sources. Their protected
source contract is:

- copy `Raw/<Kind>/*_user_resupplied_original.blend` byte-for-byte to the
  authoritative `Blender/SM_Benchmark_*.blend` path and record SHA-256
  equality before export;
- preserve byte equality after export and accept original topology, normals and
  non-manifold warnings for these benchmark LOD0 assets;
- Coffee Machine LOD1 remains the only independently simplified derivative;
- use Prefab child/import metadata for necessary Unity axis/dimension
  adaptation, while the Prefab root remains identity;
- all three benchmark LOD0 validator/test boundaries are `6,000` triangles
  pass and `6,001` triangles fail; Coffee LOD1 remains `<= 2,500` and
  `<= 60%` of LOD0;
- a protected LOD0 shape, topology, pivot or forward issue stops for Studio
  Owner direction. Blender editing is limited to Coffee LOD1 or a future
  separately approved editable source.

### 7.4 Source Editing Policy for Independently Approved Sources

本节只适用于 Coffee Machine 的独立 LOD1 derivative 或未来另行批准的 editable source；不适用于上述受保护的 original LOD0。进入 production export 前可检查：

- 多余或隐藏 mesh；
- floating geometry；
- 重叠面、内部无用面与 non-manifold geometry；
- flipped 或不一致 normals；
- 不合理的 topology density；
- 重复 Material slots；
- baked lighting、baked shadow 与高频 Texture noise；
- 不正确的 scale、pivot 和 forward；
- 不符合 silhouette/readability 的微小装饰。

如果 Tripo output 的清理成本过高，可以重新生成或手工重建。不得为了保留生成结果而突破技术 budget。

## 8. Naming 与 Folder Contract

### 8.1 Naming Rules

- 使用 ASCII；
- 使用 PascalCase；
- 不使用空格；
- 使用两位数字 variant suffix；
- `SM_` 表示 static mesh；
- `PF_` 表示 Unity Prefab；
- `M_` 表示 Unity Material；
- `T_` 表示 Texture。

### 8.2 File Structure

```text
ArtSource/VisualPipeline/Benchmarks/
├─ Raw/
│  ├─ WorkTable/
│  ├─ CoffeeMachine/
│  └─ CeramicCup/
├─ Blender/
│  ├─ SM_Benchmark_WorkTable_01.blend
│  ├─ SM_Benchmark_CoffeeMachine_01.blend
│  └─ SM_Benchmark_CeramicCup_01.blend
├─ Tools/
│  └─ ExportBenchmarkTextures.py
└─ AssetProvenance.md

Assets/Art/VisualPipeline/Benchmarks/
├─ Models/
│  ├─ SM_Benchmark_WorkTable_01.fbx
│  ├─ SM_Benchmark_CoffeeMachine_01.fbx
│  └─ SM_Benchmark_CeramicCup_01.fbx
├─ Materials/
│  ├─ M_Benchmark_WorkTableOriginal_01.mat
│  ├─ M_Benchmark_CoffeeMachineOriginal_01.mat
│  ├─ M_Benchmark_CeramicCupOriginal_01.mat
│  └─ M_Benchmark_CharacterReferenceAccent_01.mat
├─ Prefabs/
│  ├─ PF_Benchmark_WorkTable_01.prefab
│  ├─ PF_Benchmark_CoffeeMachine_01.prefab
│  └─ PF_Benchmark_CeramicCup_01.prefab
└─ Textures/
   ├─ T_Benchmark_WorkTable_BaseColor_01.png
   ├─ T_Benchmark_CoffeeMachine_BaseColor_01.png
   └─ T_Benchmark_CeramicCup_BaseColor_01.png
```

`Raw` 和 `Blender` source 放在 Unity `Assets/` 外，避免 Unity 自动导入 `.blend` 或非 production raw files。只有 production FBX、Material、Prefab 与需要的 Texture 进入 `Assets/`。

## 9. Material、Texture 与 Shader Contract

### 9.1 Shader

- 只使用 URP `Lit`；
- Surface Type 使用 Opaque；
- benchmark 不使用 Shader Graph 或 custom Shader；
- benchmark 不使用 transparent Material、实时反射或昂贵的特殊效果；
- Material 必须与当前 Windows URP pipeline 兼容。

### 9.2 Original-color Materials

| Asset | Maximum Material slots |
|---|---:|
| Work Table | `2` |
| Coffee Machine | `3` |
| Ceramic Cup | `1` |

- 每个 benchmark 只有一个与单一 submesh 对齐的 original-color `.mat`；
- Work Table、Coffee Machine 与 Ceramic Cup 分别使用独立 Material，不能互换或自动复制；
- Character Scale Reference 使用独立的青绿色 accent Material，不复用任何 furniture Material；
- Studio Owner original-color override（2026-08-01）要求这三个 benchmark 恢复 Blender source 中 packed Base Color，取代它们原本的纯色 P1 palette override；P1 仍是后续正式资产的视觉方向；
- Coffee Machine 的 LOD0 与 LOD1 必须引用同一个 original-color Material 和 Base Color Texture；
- benchmark 不实现 gameplay state color；
- 未来 gameplay 状态不能只靠红色/绿色区分，还需要 shape、icon 或其他反馈。

### 9.3 Textures

- Blender read-only audit 必须确认每个 source 的 Base Color 是否由 packed image 驱动；如果是，不能用猜测的纯色代替；
- `ExportBenchmarkTextures.py` 从 authoritative `.blend` 的 packed image repeatably 输出 production PNG，不保存或改写 source `.blend`；
- packed source image 为 `2048 × 2048` 时，production output 等比例缩小到 `512 × 512`；
- 单张 Texture 最大 `512 × 512`；
- 禁止 2K/4K benchmark Texture；
- production Texture 使用 sRGB、project-relative asset reference，并由白色 Material tint 直接显示原始 Base Color；
- Texture references 必须完整且使用 project-relative asset references；
- 不允许 machine-specific absolute path。

## 10. Mesh、LOD 与 Mobile-oriented Budget

| Asset | LOD0 triangle maximum | LOD1 triangle maximum | Required LOD |
|---|---:|---:|---|
| Work Table | `6,000` | 不要求 | LOD0 |
| Coffee Machine | `6,000` | `2,500` | LOD0 + LOD1 |
| Ceramic Cup | `6,000` | 不要求 | LOD0 |

Additional rules：

- triangle counts 以 Unity imported mesh statistics 为准；
- Coffee Machine 的 LOD1 必须保留主体 silhouette 与正面方向；
- Coffee Machine 的 LOD1 triangle count 必须同时不超过 `2,500`，且不超过其 LOD0 triangle count 的 `60%`；
- LOD 切换不得出现明显 scale、position 或 pivot jump；
- small decorative details 应优先删除，而不是用 Texture 或 geometry 强行保留；
- benchmark performance Scene 同时显示 `20` 个 Work Table、`20` 个 Coffee Machine、`20` 个 Ceramic Cup，共 `60` 个实例；
- 通过共享 Material 和 SRP Batcher-compatible URP Lit 建立 baseline；
- P3 不承诺固定 FPS，因为目标 mobile device、完整 Scene 和未来角色负载尚未确定；
- P3 记录 batch Scene 的 Windows baseline，未来 mobile/device profiling 再制定 frame-time budget。

## 11. Collider Contract

Collider 可以理解为套在 Model 外面的简单“透明盒子”。它的目标是大致包住物体，方便以后进行点击和碰撞判断，不需要沿着按钮、把手、桌腿或圆角精确贴合。

Collider 不等于 Grid Occupancy：Grid Occupancy 判断家具占用了哪些地面格子；Collider 负责 3D 空间中的 Raycast、点击或物理接触。Phase 3 不使用 Collider 取代 Phase 2 已完成的 Grid rules。

- 只使用 primitive Collider，例如 `BoxCollider` 或 `CapsuleCollider`；
- benchmark 禁止 `MeshCollider`；
- Collider 不追逐倒角、按钮、把手或装饰细节；
- Collider 不得明显超出可见 Model；
- Collider 最低点不得低于 `Y = 0`；
- benchmark Collider 使用 `isTrigger = false`。

| Asset | Collider maximum |
|---|---:|
| Work Table | `3` 个 primitive Colliders |
| Coffee Machine | `2` 个 primitive Colliders |
| Ceramic Cup | `1` 个 primitive Collider |

Cup 的 Collider 只用于验证小物件 Prefab contract。未来如果正式 Cup 只作为不可选视觉摆件，可以在对应 approved feature spec 中移除 Collider。

## 12. Prefab Assembly Contract

每个 Prefab root：

- 名称与 Prefab filename 一致；
- Transform 使用 identity；
- root 不直接保存 source/export correction；
- visual child 只负责 rendering；
- Collider components 位于 root 或明确命名的 collider child；
- Coffee Machine 使用 `LODGroup`；
- 没有 missing Mesh、Material、Texture 或 Script；
- 没有 duplicated embedded Material；
- 没有 gameplay logic、Interaction Anchor 或 placement code；
- 不修改 MainCafe production Scene。

## 13. Camera Readability Contract

使用独立 Phase 3 validation Scene，不在 `MainCafe.unity` 中长期摆放 benchmark fixtures。

### 13.1 Camera Conditions

- 使用项目已确认的 fixed `3/4 isometric-like Camera`；
- Camera Clear Flags 使用 `SolidColor`，background 固定为浅黄色 `#F2E6B8`；
- validation Scene 的 Main Camera 单独使用 `UniversalAdditionalCameraData`，antialiasing 为 `SMAA High`，并开启该 Camera 的 `Post Processing`，确保 SMAA 真正执行；不得修改 global URP 或 Quality settings；
- `SingleAssetDisplay` 使用两张相同 Work Table：Coffee Machine 单独居中放在左桌，Ceramic Cup 单独居中放在右桌；Character Scale Reference 放在 local `(1.75, 0, 2.00)`，在 Landscape `1920 × 1080` 与 Portrait `1170 × 2532`、orthographic size `4` 下都须完整位于 `0.01` viewport safe margin 内，并与左右 station 的 Camera-projected bounds 保持 non-overlap；
- `BatchDisplay` validation root 放在 local `(-30, 0, 30)`；其任何 Renderer bounds 都不得进入上述 Landscape 或 Portrait Camera viewport；
- validation Scene 当前以 orthographic size `4` 作为 P3 近距离 proxy；在固定 `1920 × 1080` 的 `1x` 或 `Fit` Game view 下检查 clarity/Material，并要求全部 `SingleAssetDisplay` renderer bounds 保持在真实 Camera viewport `0.01` safe margin 内；size `7`、`12` 是中/远距离 proxy samples，不能解释为正式 zoom presets 或锁定最终 base framing；Game view `6x` 只放大 rendered pixels，不是验收标准；
- asset silhouette、Material、Texture 与 aliasing 必须预留正式连续 `1.0x`–`3.0x` zoom envelope 的可读性；P3 不实现正式 zoom input/controller，也不批准 exact gameplay base orthographic size。正式 Portrait-first framing、smooth mouse-wheel zoom 与 future iPhone pinch 分别由后续 Camera/mobile phases 完成；
- Windows reference resolution：`1920 × 1080`；
- mobile portrait framing reference：`1170 × 2532`；
- mobile reference 只验证 no-hover visual framing 和 readability，不代表 Phase 3 已完成 iOS adaptation。

### 13.2 Acceptance

- zoom `4`：能识别主要功能细节、Material 区别和 forward；
- zoom `7`：能立即区分 Table、Coffee Machine 和 Cup；
- zoom `12`：必须认出 Table 与 Coffee Machine；Cup 只要求稳定轮廓，不要求看清小细节；
- Coffee Machine 的操作面不能被主体轮廓隐藏；
- LOD 切换无明显 popping、scale jump 或 Material change；
- 浅黄色背景本身不是 failure；只有它造成 clipping、washout 或可读性下降时才失败；
- 青绿色 Character Scale Reference 必须明显区别于背景与三件家具；
- furniture original colors 应分别可辨认为橙木/黑、浅蓝/白/黑、柔和绿，不能再显示成统一的旧 palette；
- 不能只凭 Scene view 近距离观察宣布 readability 通过。

## 14. Validation Architecture

采用“明确规范 + Editor validator + EditMode tests + PlayMode/readability fixture”的轻量方案。

### 14.1 Why This Approach

- 只写 checklist 无法阻止后续资产 drift；
- 强制自动修复的 `AssetPostprocessor` 对初学者不透明，也可能静默修改 import settings；
- validator 只报告明确问题，不自动修改 source asset；
- Model 作者可以根据报告回到 Tripo、Blender、FBX export 或 Prefab assembly 的正确层级修复。

### 14.2 Validator Responsibilities

Validator 检查：

- asset path 与 filename；
- imported scale 和 bounds；
- pivot floor alignment；
- forward marker/fixture orientation；
- Prefab identity Transform；
- Mesh triangle count；
- Material slots、Shader 与 shared references；
- Texture size；
- Collider type、count、trigger 和 bounds；
- LOD count、triangle reduction 与 renderer assignment；
- missing references；
- prohibited `MeshCollider`、transparent Material 或 custom Shader。

Validator 不判断：

- Model 是否“漂亮”；
- Tripo prompt 是否有创意；
- 角色或品牌权利的法律结论；
- 最终 gameplay balance；
- 主观 Camera readability。

这些内容由 Art review、provenance checklist 和 Studio Owner manual acceptance 处理。

## 15. TDD 与 Validation Strategy

Implementation plan 必须遵循：

```text
approved behavior
→ failing validator test
→ confirm correct RED
→ minimal validator implementation
→ confirm GREEN
→ import/assemble benchmark fixture
→ observe intended asset failure
→ fix the correct source layer
→ focused GREEN
→ full EditMode/PlayMode regression
→ manual Camera review
```

### 15.1 Automated Coverage

Normal cases：

- correctly named asset passes；
- dimensions inside tolerance pass；
- root identity Transform passes；
- Unity `+Z` forward fixture passes；
- shared URP Lit Material passes；
- primitive Colliders inside budget pass；
- Coffee Machine LOD0/LOD1 passes；
- triangle and Texture budgets pass；
- all references are present；
- batch validation returns no issues for approved benchmark Prefabs。

Bug/edge cases：

- wrong prefix、space 或 non-ASCII filename fails；
- root scale used to repair import fails；
- below-floor bounds fails；
- wrong forward fixture fails；
- dimensions outside tolerance fail；
- Coffee Machine exceeds Work Table surface fails；
- duplicated or excessive Material slots fail；
- non-URP/custom/transparent Shader fails；
- Texture above `512 × 512` fails；
- excessive triangles fail；
- missing LOD1、invalid LOD renderer 或 no meaningful reduction fails；
- `MeshCollider`、excessive Collider count 或 trigger Collider fails；
- missing Mesh/Material/Texture fails；
- validator processes multiple assets and reports every issue without silently stopping at the first one。

### 15.2 Honest Tests

- tests 必须先使用故意错误的 fixtures 观察正确 RED；
- tests 验证真实 imported asset / Prefab behavior，不只 grep source text；
- expected dimensions、counts 和 issue codes 使用 hand-checked literals；
- 不通过 mock Unity importer 来证明真实 import behavior；
- 每个 test name 说明它能捕获的具体 production break。

## 16. Manual Validation

Studio Owner 在 Unity 中完成：

1. 打开独立 Phase 3 validation Scene；
2. 确认浅黄色 `#F2E6B8` background、scene-only `SMAA High`、Camera `Post Processing` 已开启，以及明显对比的青绿色 Character Scale Reference；
3. 确认 Table 的橙木/黑、Machine 的浅蓝/白/黑、Cup 的柔和绿与 Blender original colors 一致；
4. 在 orthographic size `4` 查看细节、Material 与 forward；
5. 在 size `7` 查看三件资产的默认 gameplay readability；
6. 在 size `12` 确认 Table 与 Coffee Machine 仍可辨认；
7. 切换 Coffee Machine LOD，观察是否出现明显跳动或 Texture/Material change；
8. 检查 `1.30 m` Character Scale Reference、Work Table、Coffee Machine 与 Cup 的整体比例；
9. 检查左桌只放 Coffee Machine、右桌只放 Ceramic Cup，两件物品都位于各自桌面中央且互不遮挡；Coffee Machine 四周仍有桌面余量；
10. 检查 Windows `1920 × 1080` framing；
11. 检查 mobile portrait reference framing；
12. 运行 `60`-instance batch Scene，确认无 pink Material、missing reference、异常 Collider 或明显 Console error；
13. 在 Project view 中确认 raw/Blender files 没有进入 Unity `Assets/`。

## 17. Expected Files for Implementation

预计创建或修改：

```text
ArtSource/VisualPipeline/Benchmarks/...
Assets/Art/VisualPipeline/Benchmarks/...
Assets/Editor/AssetPipeline/BenchmarkAssetValidator.cs
Assets/Editor/AssetPipeline/BenchmarkAssetValidationIssue.cs
Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorTests.cs
Assets/Tests/PlayMode/AssetReadability/AssetPipelineReadabilityTests.cs
Assets/Scenes/Validation/AssetPipelineReadability.unity
Docs/Phase3_Beginner_Guide.md
Docs/AnimalCafe_Development_Roadmap.md
```

Implementation plan 可以在保持相同责任边界的前提下调整 exact helper filenames，但不得把 Editor validation code 放入 runtime assembly，也不得把 benchmark fixture 写入 `MainCafe.unity`。

## 18. Risks 与 Likely Bugs

### 18.1 Source / Import

- Tripo output topology 过密或包含 floating geometry；
- Blender export scale 与 Unity import scale 不一致；
- Blender `-Y` front 导入后没有成为 Unity `+Z`；
- source correction 被错误放在 Prefab Transform；
- `.blend` 或 raw file 被放进 Unity `Assets/` 并产生不可控 import。

### 18.2 Visual

- Coffee Machine 虽然变大，但遮挡 Cup 或未来 Interaction area；
- matte Material 全部过于相似，木、陶瓷和金属难以区分；
- small details 在 isometric Camera 中消失；
- zoom `12` 下 Coffee Machine 与普通箱体无法区分；
- 色板在实际 Scene lighting 下偏暗或偏黄。

### 18.3 Performance / Prefab

- Tripo mesh 超出 triangle budget；
- Coffee Machine LOD1 只是复制 LOD0；
- Material 被 embedded/duplicated；
- Texture 超出 budget；
- MeshCollider 或过细 Collider 增加成本；
- Prefab missing references 在单件检查时未被发现；
- batch Scene 与单件 Scene 使用不同 Material，导致 baseline 不真实。

## 19. Dependencies 与 Approval Gates

Dependencies：

- Phase 1 Layout Data Model `Completed`；
- Phase 2 Grid Occupancy & Placement Rules `Completed`；
- Unity `6000.5.5f1` 与 URP `17.5.0`；
- 用户可使用 Tripo 与 Blender；
- Studio Owner 已选择 `A2 + P1` visual direction。

Approval gates：

1. Studio Owner 复核并批准本 written spec；
2. 创建并复核详细 TDD/validation implementation plan；
3. 才能开始 validator code 或 benchmark asset production；
4. 每个 TDD task 必须保留 correct RED 与 fresh GREEN evidence；
5. automated validation 全绿后，由 Studio Owner 完成 Camera/manual acceptance；
6. 未经批准不把 Roadmap Phase 3 标记为 `Completed`；
7. Codex 不 commit、push、merge 或删除 branch/worktree。

## 20. Department Closeout Requirements

### Art Director

- visual direction 一致；
- silhouette、scale、Material 与 Camera readability 通过；
- authoritative source preservation、Unity child/import adaptation 与 production contract 可重复；
- benchmark assets 适合作为后续资产参考，而不是正式内容量承诺。

### Technical Director

- folder、import、Prefab、Material、Collider、LOD 和 validator architecture 安全；
- Editor code 不进入 runtime；
- 没有改变 P1/P2 contracts；
- performance baseline 可重复。

### QA Director

- validator tests 先观察正确 RED；
- focused 与 full regression 有 fresh evidence；
- normal、invalid、boundary 和 missing-reference cases 有覆盖；
- manual Camera/readability checklist 由 Studio Owner 实际执行。

### Executive Producer

- Phase 3 included/excluded scope 清楚；
- benchmark production 与大量正式资产 production 分离；
- Phase 4、UI、Decoration Mode 和 gameplay 未提前开始；
- written spec、TDD plan、automated validation 与 manual acceptance gates 均被遵守。
