# AnimalCafe Phase 4 — Core Architecture & Functional Furniture Models 设计

> 状态：Studio Owner 已完成逐节批准与 written spec final review
>
> Roadmap 正式名称：Phase 4 — Core Architecture & Functional Furniture Models
>
> 验证环境：Unity `6000.5.5f1` / URP `17.5.0` / Windows
>
> 目标兼容平台：未来 iOS

## 1. Phase 4 用简单的话说明

Phase 3 证明了 AnimalCafe 可以把 raw Model 可靠地变成符合方向、尺寸、Material、Collider 和性能规则的 Unity Prefab。Phase 4 第一次使用这条 pipeline 制作正式的店面结构与核心功能家具，并建立所有未来家具共用的 Inspector authoring 入口。

Phase 4 结束时，项目应能明确回答：

- 这件家具是什么、使用哪个 Prefab、占多少 Floor Grid；
- 它放在 Floor、Wall 还是 Furniture Surface；
- Counter 顶面有哪些可用 Surface Slots；
- Coffee Machine 的员工操作正面在哪里；
- Cash Register 的 Employee Side 与 Customer Side 在哪里；
- 初始 Floor、两面 Wall、Window 和 Entrance 的稳定空间 contract 是什么；
- 错误 Definition、Prefab、marker、occupancy 或 asset reference 为什么无效。

Phase 4 不制作 Decoration Mode、Customer AI 或完整 Coffee gameplay。它提供这些后续系统可以安全依赖的正式内容与数据 contract。

## 2. Goal 与 Player-visible Result

### 2.1 Goal

交付一个小而完整的 vertical asset set，并建立：

```text
Model / Material / Prefab
→ FurnitureDefinition Asset
→ validated mapping
→ catalogue registration
→ future Decoration and gameplay consumers
```

### 2.2 Player-visible Result

Studio Owner 可在 P4 Validation Scene 中看到：

- 与 `8 × 8` Grid 对齐的浅黄色 Floor；
- 两面固定、较深金黄色 Wall；
- Back-right Wall 下层中央附近的一个 Window；
- 前方开放边界的 Entrance 标记与 `2 × 2` Clearance overlay；
- Work Table、modular Counter、Coffee Machine、Cash Register 与 Ceramic Cup；
- Counter Surface Slot、Coffee Machine Forward、Cash Register Employee / Customer sides 和 Wall Slots 的可读 gizmos。

玩家还不能在正式 gameplay 中拖放这些内容，也没有 Customer 或 Employee 使用它们。

## 3. Confirmed Visual Direction

### 3.1 Art Direction

继续使用 Phase 3 的 `A2 + P1`：

- 圆润 functional forms；
- cream、warm wood、sage 与 honey-yellow；
- 以 matte 为主；
- 固定 `3/4 isometric-like Camera` 下优先保证 silhouette 与用途可读性。

### 3.2 Environment Palette B

- Floor visual target：浅黄色，参考 `#F8E9A8`；
- Back-left Wall visual target：较深金黄色，参考 `#D2A642`；
- Back-right Wall visual target：稍深金黄色，参考 `#C7952E`；
- Hex 是 visual target，不是绕过 URP Lighting review 的最终屏幕颜色；
- Final Material 必须在正式 Camera、Lighting 和常用 zoom 下人工检查 contrast、overexposure 与 readability。

## 4. Approved Scope

### 4.1 Included

- `8 × 8` initial Floor Grid visual surface；
- Back-left 与 Back-right 两面固定 Wall；
- 两面墙各自的 `8 columns × 2 rows` Wall Slot Grid；
- 一个默认 `1 × 1` wall-mounted Window；
- 一个固定 Entrance Portal、stable ID 与 `2 × 2` Entrance Clearance Zone；
- Work Table；
- `1 × 1` modular Counter；
- `1 × 3` Counter authoring / validation fixture；
- Coffee Machine；
- 新版 Cash Register；
- Ceramic Cup transient product visual；
- Unity `FurnitureDefinition Asset` Inspector authoring；
- Definition → runtime domain → Prefab mapping 与 catalogue registration；
- Function Type、Surface Slots、Forward 与 Cash Register sides；
- Floor、Furniture Surface 与 Wall Slot 三种独立 contracts；
- production asset validator、EditMode、PlayMode、regression 与 manual acceptance evidence；
- P4 worktree、local branch、remote branch 与 clean baseline gate。

### 4.2 Explicitly Excluded

- Phase 5：UI Architecture & Design System；
- Phase 6：Decoration Mode、鼠标拖放、placement preview、confirm / cancel；
- Phase 7：玩家移动 Window、增加 Wall Decoration、更换 Floor / Wall appearance；
- Phase 8：完整 Layout Readiness 与 blocked-anchor gameplay；
- Phase 9–14：Order、Capacity、Navigation、Customer Queue、Employee tasks 与 Integrated Cafe Loop；
- Phase 17：Save / Load 与 migration；
- Phase 29：玩家移动 Entrance、真实 Door / Window Opening 与 Room rebuild；
- Phase 33：Exterior Spawn → Entrance route validation；
- Phase 48：正式 Entrance / Coffee / payment VFX；
- Phase 26：大量 Tables、chairs、shelves、lights、plants 与 theme furniture；
- Phase 49：全项目最终 placeholder replacement 与统一优化。

## 5. Core Architecture

### 5.1 Authority Boundary

```text
FurnitureDefinition Asset
负责：ID、Display Name、Prefab、Floor Footprint、Placement Surface、Function Type

Prefab spatial contract
负责：Model、Material、Collider、Forward、Surface Slots、Cash Register sides

Runtime domain
负责：稳定、可测试的 Definition、rotation、catalogue 与 occupancy rules
```

Function Type 决定家具会做什么；marker 或 direction 只说明功能发生在哪里。普通家具不需要无关 Interaction markers。

### 5.2 FurnitureDefinition Asset

Inspector 必须提供 beginner-readable fields：

```text
Stable ID
Display Name
Prefab
Footprint Width
Footprint Depth
Allowed Placement Surface
Furniture Function Type
```

规则：

- Footprint 是明确 gameplay data，不从 Model bounds 或 Collider 推断；
- Width / Depth 每项至少为 `1`；
- 面积继续遵守 `FurnitureDefinition.MaxFootprintCellCount = 1024`；
- Stable ID 继续使用现有 lowercase pattern；
- missing Prefab、duplicate ID、invalid surface 或 invalid Function Type 阻止 catalogue registration；
- runtime pure C# domain 不持有 `GameObject` reference；Unity adapter 保存 `Definition ID → Prefab` mapping；
- 为保护 P2 regression，现有 constructor / consumers 必须有兼容路径，不进行无关重写。

### 5.3 Function Types

P4 只定义当前需要的最小集合：

```text
None
CoffeeMachine
CashRegister
```

不提前加入 Grinder、DisplayCase、Oven 或其他未进入当前 Phase 的类型。

### 5.4 Three Independent Spatial Domains

#### Floor Grid Occupancy

- 家具通过 `FurnitureDefinition.Footprint` 占用 Floor cells；
- 初始 Floor 为 `8 × 8`；
- Entrance 内侧 `2 × 2` cells 可行走但禁止家具；
- Entrance rejection 使用明确 reason，例如 `ReservedEntranceClearance`；
- `CafeLayout` 继续是 Floor furniture mutation 的唯一 owner；
- Place / Move / Rotate / Remove 继续保持 atomic。

#### Furniture Surface Contract

- Surface Slot 是支撑家具 Prefab 的明确 local marker；
- Slot 有稳定 local ID；
- Slot 不根据 Floor Footprint 自动产生；
- P4 验证 Slot count、ID、local position 与 rotation contract；
- 真正的 mounted-object placement transaction 与 UI 由后续 Phase 完成。

#### Wall Slot Contract

- 每面墙有独立 stable Wall Surface ID；
- `Wall Slot Columns = 8`；
- `Wall Slot Rows = 2`；
- `Wall Slot Size = 1 m × 1 m`；
- Physical Wall Height 约 `3 m`；
- 下层 Slot 约覆盖 world height `0.5–1.5 m`，上层约覆盖 `1.5–2.5 m`；
- Wall item 使用固定 `Wall Footprint Width × Height`；
- P4 不允许 Wall item 自由旋转；
- Wall occupancy 与 Floor occupancy 使用不同 owner / data collection。

## 6. Environment Contract

### 6.1 Floor

- 一块与 `8 × 8` Grid 对齐的初始 Floor Surface；
- visual surface 与 gameplay Grid data 分离；
- Grid overlay 使用明确 offset 或 rendering strategy 避免 Z-fighting；
- Floor 不得阻挡 furniture selection、placement raycast 或 required navigation sampling；
- Phase 7 可更换 Floor Surface appearance，但不能改变 Grid occupancy。

### 6.2 Walls

- 只显示 Back-left 与 Back-right 两面固定墙；
- 靠近 Camera 的两侧不创建透明可见墙；
- Floor Layout Bounds 继续限制家具不能摆到店外；
- 每面墙视觉宽度约 `8 m`，并与相邻 Floor Grid columns 对齐；
- 两面墙各自持有 stable Surface ID 与 `8 × 2` Slot Grid；
- P4 不允许玩家建造、移动或删除 Wall。

### 6.3 Window

- 初始一个 `1 × 1` Window；
- 默认在 Back-right Wall 下层中央附近；
- Window 是 wall-mounted object，不在 P4 切割 Wall geometry；
- 可放上层或下层；
- 与挂画等 Wall Decoration 共享 Slot occupancy；
- 不得 overlap、out of bounds 或跨墙角；
- 不允许自由旋转；
- Phase 7 提供玩家移动、增加与移除；真实 wall opening 属于 Phase 29。

### 6.4 Entrance Portal

- 位于靠近 Camera 的开放边界中央；
- 使用 stable Entrance ID；
- P4 使用简单光线、emissive plane 或地面标记，不宣称为正式 VFX；
- 内侧 `2 × 2` Entrance Clearance Zone 禁止家具但允许角色行走；
- P4 固定入口位置；
- Phase 12 使用 Entrance 进行 Customer spawn / exit；
- Phase 29 负责未来 relocation 与 layout conflict；
- Phase 33 验证 Exterior Spawn → Entrance route；
- Phase 48 制作正式 VFX。

## 7. Furniture and Asset Contracts

### 7.1 Work Table

- 正式 visual 使用 P3 已批准 source；
- target size：约 `0.90 × 0.65 × 0.90 m`；
- Floor Footprint：`1 × 1`；
- Placement Surface：Floor；
- Function Type：None；
- 提供一个 `1 × 1` Surface Slot；
- 不自动成为永久 Counter 或 Pick-up Point。

P3 benchmark Prefab 与 validator fixture 保持不变；P4 formal Prefab 复用 approved Model / Material，并使用 production naming 与独立 Definition，以免破坏 P3 regression。

### 7.2 Counter Module

- 从 Work Table Blender source 派生；
- Studio Owner 于 2026-08-04 批准本 Counter 派生使用 controlled non-uniform scale：authoritative Work Table source 的实测 bounds 为约 `0.781529 × 0.650000 × 0.781529 m`（Unity axes），无法通过 uniform scale 同时达到目标；因此各轴受控缩放至约 `1.00 × 0.72 × 1.00 m`；
- Blender Apply Scale 后导出，Unity Prefab root scale 必须为 `1,1,1`；
- Floor Footprint：`1 × 1`；
- Placement Surface：Floor；
- Function Type：None；
- 每个 module 提供一个主要 `1 × 1` Surface Slot；
- 多个相邻 modules 保持独立 Instance，不自动 merge；
- 边缘必须通过 seam、gap 与 intersection visual review。

### 7.3 Long Counter Fixture

- `1 × 3` Floor Footprint；
- 一个 Furniture Instance；
- 三个独立 `1 × 1` Surface Slots；
- 90° / 270° rotation 后 Floor Footprint 为 `3 × 1`；
- Slots 随 Counter local transform 一起旋转；
- 本 Phase 可使用 authoring / validation fixture，不要求新增一件最终美术模型。

### 7.4 Coffee Machine

- 使用 P3 approved source；
- target size：约 `0.65 × 0.62 × 0.50 m`；
- Placement Surface：FurnitureSurface；
- Surface Footprint：一个主要 Slot；
- Function Type：CoffeeMachine；
- Unity `+Z` 正面是 Employee Interaction Side；
- 不要求 Customer Side；
- 可按 `90°` 旋转；
- P4 验证方向 contract，完整 blocked-anchor / readiness 属于 Phase 8。

### 7.5 Cash Register

- raw source：`Blender Model Item/vintage computer monitor 3d model.glb`；
- 旧 `pos terminal 3d model.glb` 不再作为 P4 候选，但不删除；
- approved uniform target size：约 `0.43 × 0.45 × 0.26 m`；
- Placement Surface：FurnitureSurface；
- Surface Footprint：一个主要 Slot；
- Function Type：CashRegister；
- 必须提供互相相反的 Employee Side 与 Customer Side；
- Customer Side 决定第一位顾客 Interaction Position 与 Queue 初始 outward direction；
- 后续 Queue 可按确定规则转弯，P4 不实现 NPC Queue；
- 可按 `90°` 旋转，两侧必须一起旋转并保持相反。

Raw inspection evidence：

- GLB size 约 `0.89 MB`；
- 主模型约 `5,766 triangles`；
- 一个主要 Material；
- Base Color source 为 `2048 × 2048`；
- raw GLB 还包含 Cube、Camera 与 Light。

Production requirements：

- 清除 Cube、Camera、Light；
- target Base Color `512 × 512`，maximum `1024 × 1024`；
- LOD0 triangle maximum `6,000`；
- bottom-center pivot；
- Unity `+Z` contract；
- URP Lit / Opaque；
- root position zero、rotation identity、scale one；
- collider 不明显超出 visual bounds，也不阻挡 sides。

### 7.6 Ceramic Cup

- 使用 P3 approved source；
- target size：约 `0.14 × 0.16 × 0.14 m`；
- 作为 Coffee preparation 与 Pick-up 的 transient product visual；
- 不创建 FurnitureDefinition Asset；
- 不进入 Furniture catalogue；
- 不作为永久 furniture 占用整个 Surface Slot；
- Phase 14 及后续 Coffee gameplay 决定出现、移动与消失 timing。

## 8. Data Flow

### 8.1 Furniture

```text
approved Blender source
→ FBX / production Texture
→ Unity Model import
→ production Material
→ production Prefab + spatial contract
→ FurnitureDefinition Asset
→ validator
→ catalogue registration
→ runtime FurnitureDefinition
```

### 8.2 Scene Structure

```text
P4 environment definitions / authoring
→ validation scene fixture
→ stable Floor / Wall / Entrance references
→ future P6 / P7 / P8 consumers
```

### 8.3 Failure Behavior

Validation failure must：

- identify exact Asset / Prefab / field / marker；
- use a searchable issue code and beginner-readable message；
- not register invalid content；
- not modify MainCafe or formal Layout；
- not overwrite raw source；
- leave failed Move / Rotate / Place transaction unchanged；
- never silently infer replacement values。

## 9. Error and Validation Contracts

Validator must reject or clearly report：

- null / empty / whitespace / malformed / duplicate ID；
- missing Display Name or Prefab；
- zero、negative、too-large Footprint；
- invalid Placement Surface or Function Type；
- Definition / Prefab mapping mismatch；
- missing、duplicate、inactive-descendant 或 out-of-bounds markers；
- wrong Surface Slot count or duplicate local Slot ID；
- missing / conflicting Coffee Machine Forward；
- missing、duplicate、same-side、90-degree 或 non-cardinal Cash Register sides；
- Entrance size / bounds / stable-ID / Collider errors；
- invalid Wall dimensions、Wall Footprint、overlap、out-of-bounds、cross-corner 或 forbidden rotation；
- missing Model、Material、Texture、Collider、script 或 reference；
- wrong root transform、pivot、forward、bounds、triangle / texture budget；
- unexpected visible Cube、Camera、Light or other raw-export objects；
- Floor / Wall / Surface occupancy authority mixing；
- validation fixture leaking into MainCafe or persisted Scene data。

## 10. TDD Strategy

Every code behavior follows：

```text
approved behavior
→ focused failing test
→ confirm correct RED reason
→ minimal implementation
→ confirm focused GREEN
→ refactor
→ focused + full EditMode / PlayMode regression
```

Asset production that cannot be meaningfully unit-tested follows：

```text
approved asset contract
→ failing validator or missing-asset fixture
→ production source cleanup / export / Prefab assembly
→ validator GREEN
→ Camera / visual manual acceptance
```

No implementation task may combine unrelated contracts merely to reduce the number of RED cycles。

## 11. Automated Normal Cases

The approved normal matrix contains `N01–N69`：

### 11.1 Definition and Catalogue (`N01–N10`)

- legal `1 × 1`、`1 × 3`、`2 × 3` authoring converts correctly；
- Stable ID、Display Name、Prefab、Surface、Function Type are preserved；
- None、CoffeeMachine、CashRegister are recognized；
- multiple valid Definitions register and resolve correct Prefabs。

### 11.2 Footprint and Rotation (`N11–N16`)

- `1 × 1` remains unchanged；
- `1 × 3` and `2 × 3` swap Width / Depth at 90° / 270°；
- 0° / 180° preserve authoring orientation；
- conversion / rotation never mutates the source Asset。

### 11.3 Counter Slots (`N17–N23`)

- `1 × 1` Counter has one stable Slot；
- `1 × 3` Counter has three stable Slots；
- IDs are unique；
- move / rotation preserves local relation；
- adjacent modules remain separate Instances；
- complete long Counter remains one Instance。

### 11.4 Coffee Machine (`N24–N28`)

- correct Function Type、Forward and FurnitureSurface contract；
- Forward rotates in all four orientations；
- no Customer Side is required。

### 11.5 Cash Register (`N29–N36`)

- correct Function Type、Employee Side、Customer Side；
- sides remain opposite under four rotations；
- Queue initial direction points outward from Customer Side；
- new production Prefab maps correctly。

### 11.6 Entrance and Floor (`N37–N43`)

- initial `8 × 8` layout；
- exact `2 × 2` Clearance；
- walkable query allowed and furniture query rejected；
- outside placements unaffected；
- stable Entrance ID；
- Floor visual and Grid data remain separate。

### 11.7 Wall and Window (`N44–N54`)

- two independent `8 × 2` Wall Surfaces；
- stable unique Surface IDs；
- Window allowed upper or lower；
- default Window correct；
- `1 × 2` and `2 × 1` items occupy expected Slots；
- remove releases Slots；
- Floor / Wall occupancy stay independent。

### 11.8 Formal Mapping and Scene (`N55–N69`)

- formal Work Table、Counter、Coffee Machine、Cash Register、Cup visual、Window and environment references resolve；
- catalogue count matches approved content；
- validation scene initializes；
- palette B Materials resolve；
- colliders pass smoke checks；
- Entrance remains open；
- MainCafe isolation and Console smoke pass。

## 12. Automated Invalid, Boundary and Regression Cases

The approved negative matrix contains `B01–B112`：

### 12.1 Definition (`B01–B16`)

- null / blank / malformed / duplicate IDs；
- missing name / Prefab；
- zero / negative / oversized Footprint and overflow safety；
- None / unknown surfaces and unknown Function Type；
- null catalogue entry；
- failed conversion cannot partially register。

### 12.2 Footprint (`B17–B22`)

- invalid rotation rejection；
- failed rotation preserves state；
- four rotations return to origin；
- large legal rotation avoids overflow；
- Asset / runtime mismatch reports；
- Model bounds never silently rewrite Footprint。

### 12.3 Counter (`B23–B33`)

- missing / wrong count / duplicate Slot IDs；
- bottom or out-of-bounds Slot；
- wrong subtree and inactive child still inspected；
- independent modules cannot auto-merge；
- long Counter cannot auto-split。

### 12.4 Coffee Machine (`B34–B40`)

- missing / duplicate / invalid Forward；
- wrong Floor surface；
- unexpected Customer Side；
- rotation mismatch；
- model cannot fit one Surface Slot。

### 12.5 Cash Register (`B41–B53`)

- missing / duplicate sides；
- same、90-degree 或 non-cardinal sides；
- rotation loses opposition；
- Queue points inward；
- wrong Floor surface；
- old model mapping；
- non-uniform scale or cannot fit Slot。

### 12.6 Entrance (`B54–B64`)

- wrong size、out of bounds、missing / duplicate ID；
- single or multi-cell furniture intersects any Clearance cell；
- failed Place / Move / Rotate stays atomic；
- Clearance remains walkable；
- Entrance Collider cannot block it。

### 12.7 Wall (`B65–B82`)

- invalid columns / rows / Slot size / duplicate Surface ID；
- invalid Wall Footprint；
- Window or art out of column / row bounds；
- overlap、partial overlap、cross-corner；
- forbidden rotation；
- failed move preserves origin；
- repeated remove fails safely；
- Floor / Wall occupancy independence；
- stable coordinates after rebuild。

### 12.8 Asset Technical (`B83–B102`)

- missing Model、Material、Texture、Collider、script、reference；
- incompatible Shader；
- texture / triangle budget；
- pivot、forward、root transform；
- collider outside bounds or blocking Slot；
- inactive descendants still inspected；
- extra Cube、Camera、Light rejected；
- imported source cannot be overwritten by Prefab authoring。

### 12.9 Scene and Existing Regression (`B103–B112`)

- Floor / Grid offset and Z-fighting gate；
- Floor blocks placement raycast；
- missing Wall、wrong Window、wrong Entrance；
- fixture leakage、PlayMode residue、Console errors；
- full P2 placement atomicity and P3 validator regression remain GREEN。

## 13. Studio Owner Manual Acceptance

The approved manual matrix contains `M01–M88` and is grouped into one beginner-readable runbook during implementation planning。

### 13.1 Pre-development and Rights (`M01–M05`)

- confirm new Cash Register commercial-use rights；
- open correct P4 worktree；
- clean baseline；
- unchanged main checkout；
- clean Console。

### 13.2 Inspector (`M06–M14`)

- inspect Work Table、Counter、Coffee Machine、Cash Register、Window Definitions；
- verify Width / Depth usability and validation messages；
- verify Cup is absent from Furniture catalogue；
- verify Window has fixed footprint and no rotation control。

### 13.3 Environment (`M15–M31`)

- `8 × 8` Floor and Grid alignment；
- no Z-fighting；
- palette B；
- exactly two visible walls、clean corner and open front；
- exact Entrance and `2 × 2` overlay；
- correct rejection reason、walkability and Collider behavior。

### 13.4 Counter (`M32–M40`)

- Work Table and Counter scale；
- two / three modules combine visually without merging；
- one Slot per module；
- one long Instance with three Slots；
- rotation and Collider review。

### 13.5 Coffee Machine (`M41–M46`)

- fits Counter Slot；
- `+Z` employee side；
- four rotations；
- combined height、Collider and Camera readability。

### 13.6 Cash Register (`M47–M58`)

- new vintage model；
- approved target size and Counter fit；
- Employee / Customer sides and four rotations；
- outward Queue direction；
- `A2 + P1` readability、Texture quality、LOD and Collider。

### 13.7 Wall and Window (`M59–M72`)

- each wall shows `8 × 2` Slots；
- lower / upper height；
- default Window location、fit and no Z-fighting；
- `1 × 2`、`2 × 1`、overlap、corner and no-rotation fixtures；
- independent Floor / Wall occupancy gizmos。

### 13.8 Presentation and Regression (`M73–M88`)

- palette / hierarchy / multi-asset readability；
- Pause、1x、2x and resolutions；
- clean Console；
- MainCafe startup、Camera、selection、PlayMode cleanup；
- P3 validator still passes。

Each manual item records `Passed`、`Failed`、`Blocked` or justified `Not Applicable`。Automated GREEN alone cannot complete P4。

## 14. Branch and Clean Baseline Gate

No P4 implementation begins before：

```text
Local branch: codex/phase-4
Worktree: E:\Unity\Project\AnimalCafe\.worktrees\phase-4
Remote branch: origin/codex/phase-4
```

Required sequence：

1. inspect main / origin-main / status / worktrees；
2. preserve user-owned `.gitignore` and `AnimalCafe.slnx` changes；
3. fetch origin and verify Phase 3 merged main；
4. create worktree from verified `origin/main` without switching the user's main checkout；
5. create and push `codex/phase-4` remote branch；
6. verify main checkout is unchanged；
7. run full EditMode、PlayMode and P3 production validator baseline in P4 worktree；
8. record branch name、commit hash、counts and logs；
9. only a clean baseline permits the first P4 RED test。

The Studio Owner explicitly authorizes creation of the local / remote P4 branch at this gate. This does not authorize automatic implementation commits、merge、branch deletion or worktree deletion。

## 15. Expected File and Asset Boundaries

Exact task-by-task paths are finalized in the implementation plan. Expected scope is limited to：

```text
ArtSource/Phase4/
Assets/Art/Phase4/
Assets/Scripts/Layout/
Assets/Scripts/Content/ or the existing project-equivalent authoring boundary
Assets/Editor/AssetPipeline/
Assets/Tests/EditMode/
Assets/Tests/PlayMode/
Assets/Scenes/Validation/
Docs/
```

Rules：

- no unrelated refactor；
- no direct edits to raw user GLB files；
- P3 benchmark sources and validator fixtures remain intact；
- production variants use explicit names and stable IDs；
- generated `.meta` files are included with Unity assets；
- MainCafe edits require a named task and isolation regression；
- binary asset changes require before / after validation evidence。

## 16. Risks and Mitigations

### 16.1 Main / Worktree Confusion

Mitigation：create from verified `origin/main`、record hashes、run baseline inside exact worktree、never switch the user's main checkout。

### 16.2 Contract Scope Crossing

Mitigation：P4 validates authoring and stable data; P6 / P7 own player editing; P8 owns full readiness; P11–14 own NPC gameplay。

### 16.3 Completed P2 Rework

Entrance Clearance adds one explicit reserved-region rule. Preserve constructor / transaction compatibility、add focused RED tests、run full placement regression and keep `CafeLayout` as sole Floor mutation owner。

### 16.4 P3 Asset Regression

Do not rename or overwrite benchmark fixtures. Create production variants from approved sources and rerun the P3 production validator。

### 16.5 Model / Footprint / Collider Confusion

Keep model metres、Floor Footprint、Surface Slots、Wall Footprint and Collider as separate explicit contracts。

### 16.6 Wall System Over-engineering

P4 implements only two fixed Wall Surfaces、a minimal `8 × 2` Slot contract and validation fixtures. It does not implement wall building、Room detection or Save migration。

## 17. Completion Gate

Phase 4 may be recommended for Studio Owner acceptance only when：

- approved tasks observed correct RED before implementation；
- focused tests and full EditMode / PlayMode regression are fresh GREEN；
- P3 production validator remains GREEN；
- P4 Definition / Prefab / catalogue / environment validator is GREEN；
- all required normal、invalid、boundary and regression contracts have evidence；
- manual checklist is completed with no unexplained required failure；
- Cash Register license/use-right gate is Passed；
- Camera readability、palette B、Counter seams、Window、Entrance and direction gizmos are accepted；
- Console has no P4-introduced error；
- Roadmap and beginner handoff are updated；
- Studio Owner explicitly accepts Phase 4。

Passing tests does not authorize commit、merge、branch cleanup or starting Phase 5。

## 18. Approved Design Decisions Summary

- initial Floor `8 × 8`；
- palette B；
- two visible fixed walls；
- each Wall has 8 columns × 2 rows of `1 m × 1 m` Slots；
- lower Slot starts about `0.5 m` above Floor；
- default Window on Back-right lower center area；
- Wall items may use `1 × 1`、`1 × 2`、`2 × 1` fixed footprints and do not freely rotate；
- Entrance has a walkable but furniture-blocked `2 × 2` Clearance Zone；
- one Surface Slot per `1 × 1` Counter；three Slots per complete `1 × 3` Counter；
- adjacent modular Counters do not auto-merge；
- Coffee Machine `+Z` is employee side；
- Cash Register sides are opposite；Customer Side begins Queue outward；later Queue may bend；
- newest vintage terminal GLB is the Cash Register source；
- Ceramic Cup is transient product visual, not Furniture Definition；
- Function Type decides behavior; markers decide spatial meaning；
- explicit authoring data is never inferred from Model or Collider。
