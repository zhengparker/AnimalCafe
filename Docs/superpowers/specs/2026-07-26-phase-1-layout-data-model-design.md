# AnimalCafe Phase 1 — Layout Data Model 设计

**状态：** 用户已确认；implementation plan 等待用户审阅  
**日期：** 2026-07-26  
**Roadmap Phase：** Phase 1 — Layout Data Model  
**开发策略：** 从整理后的 `main` 创建新的 `codex/phase1-layout-data-model` branch；不沿用旧 `codex/phase1-core-loop` 的 runtime、Scene 或 tests。

## 1. 目标

Phase 1 建立不依赖 Unity Scene GameObject 的咖啡厅布局数据基础，让后续 Grid placement、Decoration、Functional Furniture 和 Save 都能使用同一份明确的数据合同。

本阶段只回答：

```text
店铺的 Grid 如何表达？
哪些区域已解锁？
一个家具类型是什么？
玩家放置的某一件家具是什么？
Definition 和 Instance 如何通过稳定 ID 连接？
哪些输入是合法数据？
```

本阶段不回答家具是否与其他家具重叠，也不生成 Customer、Cashier、Barista、NavMesh 或咖啡经营循环。

## 2. 已确认的 Architecture

采用纯 C# Layout Domain：

```text
CafeLayout
├── GridSettings
│   └── CellSize
├── UnlockedRegions
│   └── LayoutRegion
│       ├── Region ID
│       ├── Grid origin
│       ├── Rectangular size
│       └── LayoutZoneType
└── FurnitureInstances
    └── FurnitureInstance
        ├── Stable Instance ID
        ├── Definition ID
        ├── Grid position
        └── Rotation

FurnitureDefinitionCatalog
└── FurnitureDefinition
    ├── Stable Definition ID
    ├── Display name
    ├── Rectangular footprint
    └── Allowed placement surfaces
```

Domain objects：

- 不继承 `MonoBehaviour`。
- 不继承 `ScriptableObject`。
- 不保存 `GameObject`、`Transform`、Prefab、Renderer 或 Scene reference。
- Grid values、Definition、Instance 和 Region 构造后保持 immutable。
- `CafeLayout` 只通过明确的 domain methods 修改内部 collections。
- 可以在不加载 `MainCafe` Scene 的 EditMode tests 中完整验证。
- Phase 3–4 再负责将 visual assets / Prefabs 与 Definition data 连接。

## 3. Grid Data

### 3.1 GridSettings

`GridSettings` 保存：

```text
CellSize: float
```

规则：

- Phase 1 默认 `CellSize = 1.0` world unit。
- 必须大于 `0`。
- 不能是 `NaN`、positive infinity 或 negative infinity。
- 本阶段不负责把 Grid coordinate 转成 Scene world position；转换属于后续 visual/placement integration。

### 3.2 GridPosition

`GridPosition` 保存：

```text
X: int
Y: int
```

规则：

- 使用整数 coordinate。
- 允许负数，避免将未来扩建方向限制为只能向右或向上。
- 使用 value equality；相同 `X/Y` 的两个值必须相等并产生一致 hash code。
- 不保存 world-space `Vector3`。

### 3.3 GridSize

`GridSize` 保存：

```text
Width: int
Height: int
```

规则：

- `Width >= 1`。
- `Height >= 1`。
- `0` 或负数必须被拒绝。

Phase 1 只支持 rectangular footprint。Irregular footprint 明确不属于本阶段；未来若真实家具资产证明需要 irregular shape，再扩展 footprint representation，不提前实现。

## 4. Rotation

使用明确 enum：

```text
FurnitureRotation.Degrees0
FurnitureRotation.Degrees90
FurnitureRotation.Degrees180
FurnitureRotation.Degrees270
```

规则：

- 不直接保存任意 `float` angle。
- 不接受 `45°`、`360°`、负角度或通过 cast 制造的未知 enum value。
- `0°` 与 `180°` 保持原 footprint size。
- `90°` 与 `270°` 交换 footprint 的 Width / Height。

## 5. FurnitureDefinition

`FurnitureDefinition` 描述“某种家具是什么”，不描述玩家具体放置的那一件。

字段：

```text
Id: string
DisplayName: string
Footprint: GridSize
AllowedPlacementSurfaces: PlacementSurfaceType flags
```

### 5.1 Definition ID

Definition ID 是人工维护的稳定 identifier，例如：

```text
furniture.counter.basic
furniture.coffee_machine.basic
decor.plant.small
```

规则：

- 必须非 null、非空、非 whitespace。
- 允许小写字母、数字、`.`、`_` 和 `-`。
- 不允许空格、路径分隔符或 Unity asset path。
- ID comparison 使用 ordinal，不能受系统语言影响。
- Catalog 内不允许 duplicate Definition ID。

### 5.2 DisplayName

- 必须非 null、非空、非 whitespace。
- 仅用于人类可读信息，不作为 lookup key。
- 修改显示名称不能改变 Definition ID。

### 5.3 PlacementSurfaceType

使用 flags enum，为未来 placement validation 保留明确扩展点：

```text
Floor
Wall
FurnitureSurface
```

规则：

- Definition 至少允许一种 surface。
- `None` 被拒绝。
- 未定义的 flag bits 被拒绝。
- Phase 1 只保存 compatibility data，不判断目标 Scene surface 是否真实存在。

### 5.4 不包含的 Definition 内容

Phase 1 Definition 不保存：

- Prefab 或 Model reference。
- Material、Icon 或 Animation。
- Price、unlock level 或 economy values。
- Cash Register、Coffee Machine 等 gameplay capability。
- Interaction Anchors。
- Collider 或 NavMesh data。
- Localized text table。

这些内容在后续对应 Phase 单独设计。

## 6. FurnitureInstance

`FurnitureInstance` 描述玩家布局中的一件具体家具。

字段：

```text
InstanceId: string
DefinitionId: string
Position: GridPosition
Rotation: FurnitureRotation
```

规则：

- `InstanceId` 与 `DefinitionId` 是不同概念，不能互换。
- 新 Instance ID 使用 GUID 的 lowercase `N` format：

```text
32 hexadecimal characters
example: 7f17d8fa59f64be0a6689666ce4a28d2
```

- 连续创建的 instances 必须拥有不同 ID。
- 显式恢复已有 Instance 时，ID 必须符合相同格式。
- `DefinitionId` 必须能在提供的 `FurnitureDefinitionCatalog` 中找到。
- Instance 不重复保存 Definition footprint 或 DisplayName，避免两份数据发生漂移。
- Instance 不保存 Scene object reference。

## 7. Stable ID

新增小型 `StableId` utility：

```text
StableId.NewFurnitureInstanceId()
StableId.IsValidFurnitureInstanceId(string)
```

规则：

- generator 使用 `Guid.NewGuid().ToString("N")`。
- validation 只接受 32-character GUID `N` format。
- Definition ID 不使用 GUID validator。
- 本阶段不建立通用 ID framework，不为未来未知 domain 预先制造 abstraction。

## 8. LayoutRegion 与 Zone

`LayoutRegion` 表示一个已解锁的 rectangular Grid 区域。

字段：

```text
Id: string
Origin: GridPosition
Size: GridSize
ZoneType: LayoutZoneType
```

`LayoutZoneType`：

```text
Interior
Exterior
```

规则：

- Region ID 必须非空，并在同一 `CafeLayout` 中唯一。
- Zone 必须是已定义 enum value。
- Region size 必须合法。
- Region origin 可以是负坐标。
- Phase 1 允许保存多个 regions。
- Region overlap、zone conflict、连通性和 placement permission 属于 Phase 2，不在本阶段判断。
- “未来 special area”不通过任意 string 提前实现；需要时再扩展 enum 和 migration。

## 9. FurnitureDefinitionCatalog

Catalog 职责：

- 保存一组 validated definitions。
- 按 Definition ID 查询。
- 拒绝 duplicate IDs。
- 提供：

```text
bool TryGet(string definitionId, out FurnitureDefinition definition)
FurnitureDefinition GetRequired(string definitionId)
```

行为：

- `TryGet` 对未知合法 ID 返回 `false`，不记录 Console error。
- `GetRequired` 对未知 ID 抛出包含该 ID 的 `KeyNotFoundException`。
- 输入 null、empty 或 whitespace 时明确拒绝。
- Catalog 构造后不受调用者原始 collection 后续修改影响。
- 对外只暴露 read-only view。

## 10. CafeLayout

`CafeLayout` 保存：

```text
GridSettings
UnlockedRegions
FurnitureInstances
```

构造和操作合同：

```text
CafeLayout(
    GridSettings gridSettings,
    FurnitureDefinitionCatalog definitionCatalog)

void AddRegion(LayoutRegion region)
void AddFurnitureInstance(FurnitureInstance instance)
bool TryGetFurnitureInstance(
    string instanceId,
    out FurnitureInstance instance)
```

职责：

- 验证 Region ID 唯一。
- 验证 Furniture Instance ID 唯一。
- 添加 Instance 前验证其 Definition ID 存在于 Catalog。
- 提供按 Instance ID 查询。
- 对外暴露 read-only collections。
- Constructor 必须保留 Catalog 的稳定引用；Catalog 自身构造后 immutable。

`FurnitureInstance` creation contract：

```text
FurnitureInstance.CreateNew(
    string definitionId,
    GridPosition position,
    FurnitureRotation rotation)

FurnitureInstance.Restore(
    string instanceId,
    string definitionId,
    GridPosition position,
    FurnitureRotation rotation)
```

- `CreateNew` 使用 `StableId.NewFurnitureInstanceId()`。
- `Restore` 用于未来从持久数据重建对象，但 Phase 1 不实现 Save file。
- 两个入口使用相同 validation；`Restore` 不生成或替换传入 ID。

明确不负责：

- Furniture overlap。
- Unlocked cell containment。
- Move / rotate / remove transaction。
- Surface compatibility execution。
- Scene spawning。
- Save file serialization。

这些规则从 Phase 2 开始逐步加入。

## 11. Error Handling

Phase 1 是 domain programming boundary，invalid construction 使用明确 exceptions：

| 情况 | 行为 |
|---|---|
| null required object | `ArgumentNullException` |
| 空或非法 ID / name | `ArgumentException` |
| 非法 size / cell size / enum | `ArgumentOutOfRangeException` |
| duplicate Definition / Region / Instance ID | `ArgumentException`，message 包含 duplicate ID |
| unknown Definition lookup | `TryGet = false`；`GetRequired` 抛 `KeyNotFoundException` |
| 添加引用未知 Definition 的 Instance | `ArgumentException`，message 包含 Definition ID |

Domain 不通过 `Debug.LogError` 代替失败，也不静默修正 invalid data。

## 12. Phase 0 Scene Cleanup

新的 Phase 1 branch 从整理后的 `main` 创建，因此不会带入旧 Phase 1 的：

- Customer、Cashier、Barista。
- Order、Capacity、Queue。
- L 型柜台。
- NavMesh。
- 灰色或黄色旧 Phase 1 floor。
- Phase 1 status UI。

Phase 0 cleanup 删除：

- `MainCafe/Phase0_Demo`。
- `Selectable_Blue`。
- `Selectable_Green`。
- `Time_Test_Mover`。
- `Assets/Materials/Phase0Blue.mat`。
- `Assets/Materials/Phase0Green.mat`。
- `Assets/Materials/Phase0Orange.mat`。
- `Assets/Scripts/Testing/TimeTestMover.cs`。
- 对应 `.meta` files 和清空后的 `Testing` folder metadata。

`Phase0SceneSetup` 改为：

- 继续生成/维护 `Phase0_Runtime`。
- 继续生成/维护 `Phase0_TimeControls`。
- 继续确保 `EventSystem`。
- 不再创建 demo objects 或 demo materials。
- 如果旧 `Phase0_Demo` 存在，setup 会明确删除，使 setup 具有 cleanup migration 行为。

保留：

- Main Camera。
- `CafeCameraController`。
- `MouseCameraInput`。
- `SceneInteractionController`。
- `ColorSelectable` runtime capability。
- `GameTimeService`。
- Pause / `1x` / `2x` UI。
- `GameEventBus`。

当前 `main` 的 `MainCafe` 没有正式 floor object。Phase 1 是纯 data phase，因此不新增 floor。旧 Phase 1 branch 中的灰色地板不会进入新 branch。

## 13. File Structure

计划新增：

```text
Assets/Scripts/Layout/
├── CafeLayout.cs
├── FurnitureDefinition.cs
├── FurnitureDefinitionCatalog.cs
├── FurnitureInstance.cs
├── FurnitureRotation.cs
├── GridPosition.cs
├── GridSettings.cs
├── GridSize.cs
├── LayoutRegion.cs
├── LayoutZoneType.cs
├── PlacementSurfaceType.cs
└── StableId.cs

Assets/Tests/EditMode/
├── AnimalCafe.EditModeTests.asmdef
├── GridValueTests.cs
├── FurnitureDefinitionTests.cs
├── FurnitureInstanceTests.cs
├── FurnitureDefinitionCatalogTests.cs
├── CafeLayoutTests.cs
└── Phase0SceneCleanupTests.cs
```

计划修改：

```text
Assets/Editor/Phase0SceneSetup.cs
Assets/Scenes/MainCafe.unity
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
Docs/Phase0_Beginner_Guide.md
Docs/AnimalCafe_Development_Roadmap.md
```

计划删除第 12 节列出的 Phase 0 demo-only files。

继续使用现有 `AnimalCafe.Runtime` assembly；Phase 1 不创建额外 runtime assembly。新增 EditMode test assembly 引用 `AnimalCafe.Runtime`、`AnimalCafe.Editor` 和 Unity Test Framework，使纯 Layout tests 与 Scene setup migration tests 都能在 Editor 中运行。

## 14. Automated Test Plan

所有 production behavior 必须先写 failing test，再写最小实现。

### 14.1 Grid Value Tests

| ID | Test Case | Expected |
|---|---|---|
| G01 | `GridPosition(2, 3)` 保存坐标 | X=2，Y=3 |
| G02 | 两个相同 GridPosition equality | equal 且 hash 相同 |
| G03 | 不同坐标 equality | not equal |
| G04 | GridPosition 接受负数 | 正常创建 |
| G05 | `GridSize(1, 1)` | 正常创建 |
| G06 | rectangular `GridSize(2, 3)` | 正常创建 |
| G07 | Width = 0 | `ArgumentOutOfRangeException` |
| G08 | Height = 0 | `ArgumentOutOfRangeException` |
| G09 | Width < 0 | `ArgumentOutOfRangeException` |
| G10 | Height < 0 | `ArgumentOutOfRangeException` |
| G11 | CellSize = 1.0 | 正常创建 |
| G12 | CellSize = 0 | `ArgumentOutOfRangeException` |
| G13 | CellSize < 0 | `ArgumentOutOfRangeException` |
| G14 | CellSize = NaN | `ArgumentOutOfRangeException` |
| G15 | CellSize = ±Infinity | `ArgumentOutOfRangeException` |

### 14.2 Rotation Tests

| ID | Test Case | Expected |
|---|---|---|
| R01 | `0°` rotated size | Width/Height 不变 |
| R02 | `90°` rotated size | Width/Height 交换 |
| R03 | `180°` rotated size | Width/Height 不变 |
| R04 | `270°` rotated size | Width/Height 交换 |
| R05 | cast 未定义 enum value | `ArgumentOutOfRangeException` |
| R06 | rectangular footprint 连续旋转四次 | 回到原始 size |

### 14.3 FurnitureDefinition Tests

| ID | Test Case | Expected |
|---|---|---|
| D01 | 合法 ID、name、size、Floor surface | 创建成功 |
| D02 | 同时允许 Floor + Wall | flags 被完整保存 |
| D03 | Definition ID 为 null | `ArgumentNullException` |
| D04 | Definition ID 为空/whitespace | `ArgumentException` |
| D05 | Definition ID 含空格 | `ArgumentException` |
| D06 | Definition ID 含 `/`、`\` | `ArgumentException` |
| D07 | Definition ID 含大写或其他非法字符 | `ArgumentException` |
| D08 | DisplayName 为 null | `ArgumentNullException` |
| D09 | DisplayName 为空/whitespace | `ArgumentException` |
| D10 | PlacementSurface = None | `ArgumentOutOfRangeException` |
| D11 | PlacementSurface 含未知 flag bit | `ArgumentOutOfRangeException` |
| D12 | 修改 DisplayName 不影响 ID | lookup identity 不变 |
| D13 | Definition 不含 UnityEngine.Object fields | reflection scan 结果为 0 |

### 14.4 Stable ID 与 FurnitureInstance Tests

| ID | Test Case | Expected |
|---|---|---|
| I01 | generator 创建 ID | 32 位 lowercase GUID N format |
| I02 | 连续创建 1,000 IDs | 全部唯一 |
| I03 | validator 接受合法 N format | true |
| I04 | validator 拒绝 null/empty | false |
| I05 | validator 拒绝带 hyphen GUID | false |
| I06 | validator 拒绝非 GUID 32 字符串 | false |
| I07 | 合法 Instance data | 保存全部字段 |
| I08 | Instance ID null/empty/invalid | 明确 exception |
| I09 | Definition ID null/empty/invalid | 明确 exception |
| I10 | invalid rotation enum | `ArgumentOutOfRangeException` |
| I11 | 两个相同 Definition 的 instances | Definition ID 相同、Instance ID 不同 |
| I12 | Instance 不复制 Definition footprint/name | reflection/data contract 不包含重复字段 |
| I13 | Instance 不含 UnityEngine.Object fields | reflection scan 结果为 0 |

### 14.5 LayoutRegion Tests

| ID | Test Case | Expected |
|---|---|---|
| Z01 | Interior region | 创建成功 |
| Z02 | Exterior region | 创建成功 |
| Z03 | negative origin | 创建成功 |
| Z04 | Region ID null/empty | 明确 exception |
| Z05 | invalid size | `GridSize` 拒绝 |
| Z06 | invalid Zone enum | `ArgumentOutOfRangeException` |
| Z07 | 两个 regions 可相邻 | data 接受 |
| Z08 | overlapping regions | Phase 1 只保存，不执行 occupancy rejection |

### 14.6 Definition Catalog Tests

| ID | Test Case | Expected |
|---|---|---|
| C01 | `TryGet` 已知 ID | true + exact object |
| C02 | `TryGet` 未知合法 ID | false + null |
| C03 | `GetRequired` 已知 ID | exact object |
| C04 | `GetRequired` 未知 ID | `KeyNotFoundException`，message 含 ID |
| C05 | duplicate Definition ID | `ArgumentException`，message 含 ID |
| C06 | null definitions collection | `ArgumentNullException` |
| C07 | collection 中包含 null | `ArgumentException` |
| C08 | 构造后修改原 collection | Catalog 内容不变化 |
| C09 | 对外 definitions collection | 不能通过 cast 修改内部状态 |
| C10 | lookup 使用 ordinal | 不受 culture 改变影响 |

### 14.7 CafeLayout Tests

| ID | Test Case | Expected |
|---|---|---|
| L01 | 创建空 layout | settings/empty collections 正确 |
| L02 | 添加 Interior 和 Exterior regions | 保存成功 |
| L03 | duplicate Region ID | 拒绝，message 含 ID |
| L04 | 添加引用已知 Definition 的 Instance | 成功 |
| L05 | 添加引用未知 Definition 的 Instance | 拒绝，message 含 Definition ID |
| L06 | duplicate Instance ID | 拒绝，message 含 ID |
| L07 | 按 Instance ID 查询已知 object | 成功 |
| L08 | 查询未知合法 Instance ID | false |
| L09 | 添加 invalid / duplicate object 失败 | Layout 原有 collections 不变化 |
| L10 | exposed regions/instances | read-only |
| L11 | 两个 instances 占用相同 position | Phase 1 data 接受；Phase 2 才判断 overlap |
| L12 | Instance 位于 unlocked region 外 | Phase 1 data 接受；Phase 2 才判断 placement |
| L13 | Layout domain 不加载 MainCafe | test 正常完成 |

## 15. Bug Regression Test Plan

这些 tests 专门防止旧设计中已经暴露或新 Roadmap 明确预警的问题。

| ID | Bug | Regression Test |
|---|---|---|
| B01 | Definition ID 与 Instance ID 淇用 | 用 Instance GUID 查询 Definition 必须失败；添加未知 Definition 的 Instance 必须拒绝 |
| B02 | Duplicate stable IDs | Catalog、Region 和 Instance 分别拒绝 duplicate ID |
| B03 | Scene 成为唯一真相 | Layout tests 在未加载任何 Scene 时全部通过 |
| B04 | Scene reference 泄漏进 domain | reflection 检查 public/private fields 不派生自 `UnityEngine.Object` |
| B05 | 任意 rotation 被静默接受 | cast invalid enum 必须抛 exception |
| B06 | 旋转后 rectangular footprint 错位 | 90/270 交换 width/height；0/180 不交换 |
| B07 | Invalid footprint 进入后续 placement | zero/negative size 在构造边界立即拒绝 |
| B08 | Caller 修改输入 collection 后污染 Catalog | Catalog constructor defensive copy test |
| B09 | 对外 collection 可被修改 | read-only exposure tests |
| B10 | Unknown Definition 被静默保留 | add instance 明确失败且 error 含 Definition ID |
| B11 | System culture 改变 ID lookup | Turkish/其他 culture 下 ordinal lookup 一致 |
| B12 | Phase 0 setup 再次生成 demo cubes | setup 两次后 Scene 仍无 `Phase0_Demo` |
| B13 | 删除 cubes 导致 selection runtime 被误删 | fixture-based `ColorSelectable` tests 继续通过 |
| B14 | 删除 Time Test Mover 导致 Game Time 回归 | service/UI tests 验证 Pause/1x/2x，不依赖 mover |
| B15 | Scene cleanup 误改 Camera | Camera transform、orthographic size、settings 与 16-test baseline 一致 |

## 16. Phase 0 Cleanup 与 Integration Tests

### 16.1 Edit/Setup Tests

| ID | Test Case | Expected |
|---|---|---|
| P01 | Scene 中预先存在旧 `Phase0_Demo` 后运行 setup | demo root 被删除 |
| P02 | setup 连续运行两次 | 只有一个 runtime root、time controls 和 EventSystem |
| P03 | setup 后检查 demo materials | 不再被创建或引用 |
| P04 | setup 后 Camera settings | 与 Phase 0 accepted baseline 一致 |
| P05 | setup 缺少 Main Camera | fail clearly，不产生半配置 Scene |

### 16.2 MainCafe PlayMode Smoke Tests

| ID | Test Case | Expected |
|---|---|---|
| P06 | 加载 `MainCafe` | 成功，无 Console error |
| P07 | 查找 `Phase0_Demo` | null |
| P08 | 查找三个 demo cubes | 全部 null |
| P09 | 查找 `Phase0_Runtime` | 存在且 required components 完整 |
| P10 | 查找 Time Controls | Pause/1x/2x buttons 完整 |
| P11 | 查找 EventSystem | 存在且使用 Input System module |
| P12 | Scene 中旧 Phase 1 roots/agents/counter/NavMesh | 全部不存在 |

### 16.3 Phase 0 Regression Suite

原 16 个 Phase 0 tests 继续覆盖：

- Camera pan。
- Camera zoom。
- Camera bounds。
- Mouse input。
- tap vs drag。
- selection switching / clearing。
- `ColorSelectable` visual feedback。
- Pause / `1x` / `2x`。
- Event Bus。
- MainCafe load smoke。

其中原先依赖 Scene demo cubes 或 `TimeTestMover` 的 tests 改为在 test 内创建 fixture，并在 test 结束后销毁。正式 Scene 不再承担 automated test fixture 职责。

## 17. Automated Verification Gate

Phase 1 automated gate：

1. 新 EditMode Layout tests 全部通过。
2. 修改后的 Phase 0 PlayMode tests 全部通过。
3. `MainCafe` smoke tests 全部通过。
4. failed = `0`。
5. skipped = `0`。
6. inconclusive = `0`。
7. Console 没有 unexpected error。
8. tests 不产生 tracked Scene、material 或 config drift。
9. `git diff --check` 通过。
10. 没有提交 `.slnx`、Temp、Logs、Library 或 test result artifacts。

## 18. Manual Test Plan

Phase 1 是纯 data model，没有 furniture placement UI，因此 manual test 主要确认正式 Scene 清洁、Phase 0 基础未回归，以及测试结果可重复。

### 18.1 开始前

1. 确认打开的是新 worktree `codex/phase1-layout-data-model`。
2. 确认 Unity version 为 `6000.5.5f1`。
3. 打开 `Assets/Scenes/MainCafe.unity`。
4. Console 执行 Clear。

### 18.2 Edit Mode Test Runner

1. 打开 `Window → General → Test Runner`。
2. 选择 EditMode。
3. 运行全部 tests。
4. 确认全部绿色，没有 Failed、Skipped 或 Inconclusive。
5. 展开 Layout test groups，确认 Grid、Definition、Instance、Catalog、Layout cases 均存在。

### 18.3 Scene 清洁检查

在 Hierarchy 确认：

- 有 `Main Camera`。
- 有 `Phase0_Runtime`。
- 有 `Phase0_TimeControls`。
- 有 `EventSystem`。
- 没有 `Phase0_Demo`。
- 没有 `Selectable_Blue`。
- 没有 `Selectable_Green`。
- 没有 `Time_Test_Mover`。
- 没有旧 Customer、Cashier、Barista、Counter、NavMesh 或 Phase 1 status UI。
- 没有灰色/黄色旧 Phase 1 floor。

### 18.4 Play Mode 基础检查

1. 进入 Play Mode。
2. 确认 Game View 中没有 demo cubes。
3. 确认没有旧咖啡厅柜台、员工或顾客。
4. 点击 `Pause`，确认游戏速度状态进入 paused。
5. 点击 `1x` 和 `2x`，确认三个 buttons 均可正常接收点击，没有 Console error。
6. 使用鼠标滚轮确认 Camera zoom 仍响应。
7. 使用 drag 确认 Camera pan input 仍响应；空 Scene 中可同时观察 Main Camera Inspector transform。
8. 退出 Play Mode。
9. 确认 Console 没有红色 error。

由于正式 Scene 不再包含 time mover，`1x` 与 `2x` 的精确 time scale 功能由 automated regression tests 验证；manual gate 只确认 controls 仍存在、可点击且不报错。

Selection 的 manual visual demo 不再保留在正式 Scene；selection capability 由 test 内创建的 fixture regression tests 验证。未来正式家具进入 Scene 后，再恢复针对真实家具的 manual selection acceptance。

### 18.5 PlayMode Test Runner

1. 在 Test Runner 选择 PlayMode。
2. 运行全部 tests。
3. 确认所有 Phase 0 regression 与 MainCafe smoke tests 为绿色。
4. 再次检查 Console 无红色 error。

### 18.6 用户验收问题

用户最终明确确认：

- 正式 Scene 已清除所有 Phase 0 demo objects。
- 旧 Phase 1 咖啡循环和临时地板没有进入新 branch。
- Camera 与 Pause/1x/2x 仍正常。
- EditMode 与 PlayMode tests 全部通过。
- Console clean。

## 19. Branch、Merge 与 Cleanup Workflow

在 spec 和 implementation plan 都获批准后：

1. 用户先整理并 commit 当前 `main` 中已批准的 Design/Roadmap 文档。
2. 从该 `main` 创建：

```text
codex/phase1-layout-data-model
```

3. 创建新的 isolated worktree。
4. 删除本地旧 `phase1-core-loop` worktree。
5. 删除本地旧 `codex/phase1-core-loop` branch。
6. 暂时保留 GitHub remote `origin/codex/phase1-core-loop` 作为备份。
7. 按 TDD implementation plan 开发新 Phase 1。
8. Automated gate 全部通过后停止。
9. 用户执行第 18 节 manual tests。
10. 只有用户明确批准后，merge 新 branch 到 `main`。
11. 验证 merged `main`。
12. 删除新 Phase 1 worktree 和已 merge local branch。
13. 用户确认不再需要旧实现后，再删除 remote old Phase 1 branch。

任何 branch/worktree 删除前必须先确认：

- 对应 commits 已存在于预期 remote 或已 merge branch。
- 删除目标是确切的 worktree/branch，不是 project root 或 `main`。
- worktree 不包含未提交的用户文件。

## 20. Completion Gate

Phase 1 只有在以下条件全部满足后才算完成：

- 本 spec 与 implementation plan 经用户批准。
- Layout Data Model 按本文合同实现。
- 所有 normal、invalid-data 和 bug regression tests 通过。
- Phase 0 demo-only files 被清除。
- Phase 0 正式能力 regression tests 通过。
- MainCafe 没有旧 Phase 1 内容或临时地板。
- Automated verification non-pass counts 全为 `0`。
- 用户完成 manual test plan 并明确批准。
- 新 branch merge 到 `main` 后再次验证。
- Roadmap 记录准确的完成 evidence。
