# AnimalCafe Development Roadmap

> 状态：Approved Roadmap
>
> 类型：Dependency-driven, milestone-based development roadmap
>
> 当前开发环境：Windows / Unity `6000.5.5f1`
>
> 目标兼容平台：iOS
>
> 更新日期：2026-08-01

## 1. 文档用途

本文档把 `AnimalCafe_Project_Design.md` 中的长期 Game Design 拆成能够单独设计、批准、开发和测试的小型 development phases。

- `AnimalCafe_Project_Design.md` 是长期游戏规则的 Source of Truth。
- 本文档定义 implementation 顺序、system dependencies、risks 和 phase gates。
- Phase 0 保持已完成状态；Phase 1–50 采用 dependency-driven 小阶段。
- 每个 Phase 只承担一个主要技术风险，并拥有独立 design、implementation plan 和 tests。
- 每个 Phase 使用独立的 `Docs/PhaseN_Beginner_Guide.md` 作为 beginner educational note；不能把多个 Phase 的教学内容混入同一个 guide。
- 每份 Phase spec 和 Beginner Guide 的开头必须先用通俗、适合中学生理解的语言说明“这个 Phase 是做什么的”，并至少提供一个具体例子，再进入 architecture、technical rules 或 test details。
- 每个 Phase 开始前需要用户批准 design 和 implementation plan。
- 未通过当前 Phase gate 时，不开始下一个 Phase。
- 每完成一个 Major Milestone，重新 review 后续 roadmap，允许根据实际 playtest 调整远期 Phase。
- Open Design Questions 不能直接作为 implementation requirements；进入相关 Phase 前必须先作决定。
- Roadmap 不使用周数或 deadline，以可验证 milestone 为进度依据。

### 1.1 Superseded Documentation

以下文件描述旧版大型 Phase 1，保留作为历史参考，但不再作为当前执行依据：

- `Docs/superpowers/specs/2026-07-25-phase-1-core-cafe-loop-design.md`
- `Docs/superpowers/plans/2026-07-25-phase-1-core-cafe-loop.md`

新 implementation 从本文档的 **Phase 1 — Layout Data Model** 开始。

---

## 2. Engineering Ownership 与 Approval

设计协作采用以下职责：

- 用户提供游戏目标、体验方向、创意偏好，并批准或否决方案。
- 软件总工程师负责提出 architecture、system boundaries、implementation order 和 tests。
- 每份方案必须说明：
  - 为什么现在做；
  - 为什么不放到更早或更晚；
  - 主要难点；
  - 推荐解决方式；
  - 风险和可能出现的 bugs；
  - automated、integration 和 manual tests。
- 如果用户想法会产生明显 scope、architecture、testing 或体验风险，软件总工程师应直接说明并提出更安全的替代方案。
- Approval 只授权已展示的 scope，不自动授权扩大范围、commit 或 push。

---

## 3. Development Strategy

AnimalCafe 使用 **Dependency-driven Small Phases + Playable Milestones**。

```text
一个小 Phase
→ 一个主要风险
→ 一份 design
→ 一份 implementation plan
→ 一组 focused tests
→ 一次 approval gate
```

多个小 Phase 共同形成一个玩家可感知的 Major Milestone。

### 3.1 为什么不是先完成整个装修系统

装修系统必须先提供最小 Layout foundation，避免 Cafe Loop 把 Cash Register、Coffee Machine 和 Pick-up 写死在 Scene 中。

但完整装修、扩建、多楼层和 Atmosphere 不能全部提前开发。没有真实 Cafe Loop 时，无法准确知道 functional furniture、NPC paths、queue 和 expansion 的实际需求，容易 over-engineering。

因此采用：

```text
最小 Layout Foundation
→ 最小 Functional Furniture
→ Core Cafe Loop
→ 根据真实 gameplay 扩展完整 Decoration 与 Store Expansion
```

### 3.2 核心 Dependency Chain

```text
Layout Data
→ Grid Rules
→ Visual Style / Asset Pipeline
→ Core Models / UI Foundation
→ Basic Decoration
→ Functional Stations
→ Orders and Capacity
→ Navigation
→ Customers and Employees
→ Integrated Cafe Loop
→ Business Day / Economy / Save
→ Application Shell / Recipe / Inventory
→ Character Models / Characters / Events
→ Interior Models / Progression / Expansion
→ Exterior Models / Exterior Gameplay
→ Signature Visual Assets / Signature Gameplay
→ Offline Progression
→ UI / VFX / Asset Optimization
→ Windows Release
→ Mobile UI / iOS Adaptation
```

---

## 4. Roadmap Overview

### 4.1 Work Type Legend

- **Unity**：主要完成 C#、Scene、Prefab、data、services 和 automated tests。
- **Model**：主要制作 Models、Rig、Animation、Materials、Textures 或 visual assets。
- **Unity + Model**：同时包含资产制作和 Unity import、Prefab、Collider、Anchor、LOD 与 Scene 验证。
- **Unity + UI**：Gameplay logic 与对应 feature UI 同时完成。
- **UI/UX + Unity**：主要进行 UI architecture、visual design、navigation、usability 和 Unity integration。
- **Online + Unity**：涉及 account、Cloud data、network failure 和 Unity client integration。
- **Platform + Unity**：涉及目标平台 input、lifecycle、build、performance 和 Unity integration。

所有 Model phases 最终仍需要在 Unity 中进行 import 和 technical validation；`Model` 表示主要生产工作发生在 DCC / visual asset workflow，而不是表示完全不打开 Unity。

| Phase | Development Part | Work Type | Major Milestone |
|---:|---|---|---|
| 0 | Project Foundation — Completed | Unity | Foundation Prototype |
| 1 | Layout Data Model | Unity | Layout Foundation |
| 2 | Grid Occupancy & Placement Rules | Unity | Layout Foundation |
| 3 | Visual Style & Asset Pipeline Foundation | Unity + Model | Visual Foundation |
| 4 | Core Architecture & Functional Furniture Models | Unity + Model | Visual Foundation |
| 5 | UI Architecture & Design System | UI/UX + Unity | Visual Foundation |
| 6 | Basic Decoration Mode | Unity + UI | Layout Foundation |
| 7 | Functional Furniture & Layout Readiness | Unity | Layout Foundation |
| 8 | Order Domain | Unity | Core Cafe Loop |
| 9 | Capacity & Reservation | Unity | Core Cafe Loop |
| 10 | Navigation & Movement Recovery | Unity | Core Cafe Loop |
| 11 | Customer Spawn & Counter Queue | Unity | Core Cafe Loop |
| 12 | Employee Task Flow | Unity | Core Cafe Loop |
| 13 | Integrated Takeout Cafe Loop | Unity + UI | Core Cafe Loop Prototype |
| 14 | Business Day Lifecycle | Unity + UI | Management MVP |
| 15 | Economy & Daily Report | Unity + UI | Management MVP |
| 16 | Save Foundation | Unity | Management MVP |
| 17 | Application Shell & Loading | UI/UX + Unity | Management MVP |
| 18 | Recipe & Menu | Unity + UI | Management MVP |
| 19 | Inventory, Reservation & Restocking | Unity + UI | Playable Management MVP |
| 20 | Core Character Models & Animation Pipeline | Unity + Model | Character Vertical Slice |
| 21 | Staff Identity & Work Capabilities | Unity + UI | Character Vertical Slice |
| 22 | Customer Identity & Returning Visits | Unity + UI | Character Vertical Slice |
| 23 | Traits, Mood & Relationships | Unity + UI | Character Vertical Slice |
| 24 | Events & Player Decisions | Unity + UI | Character-driven Vertical Slice |
| 25 | Interior Furniture Model Set | Unity + Model | Cafe Growth |
| 26 | Progression & Unlocks | Unity + UI | Cafe Growth |
| 27 | Store Expansion | Unity + UI | Cafe Growth |
| 28 | Exterior Model Set | Unity + Model | Exterior Decoration |
| 29 | Exterior Zone & Facade | Unity + Model | Exterior Decoration |
| 30 | Outdoor Decoration Placement | Unity + UI | Exterior Decoration |
| 31 | Entrance Route & Exterior Validation | Unity | Exterior Decoration |
| 32 | Seating & Dine-in Service | Unity + UI | Cafe Growth |
| 33 | Atmosphere & Themes | Unity + UI | Advanced Decoration |
| 34 | Special Rooms & Multi-floor | Unity + Model | Advanced Decoration |
| 35 | Coffee Bean Visual Asset Set | Model | Signature Content |
| 36 | Coffee Bean Exploration | Unity + UI | Signature Content |
| 37 | Syrup & Add-on Visual Asset Set | Model | Signature Content |
| 38 | Syrup & Add-on Gameplay | Unity + UI | Signature Content |
| 39 | Bakery Visual Asset Set | Model | Signature Content |
| 40 | Bakery Gameplay | Unity + UI | Signature Content |
| 41 | Merchandise Visual Asset Set | Model | Signature Content |
| 42 | Merchandise Gameplay | Unity + UI | Signature Content |
| 43 | Offline Progression | Unity + UI | Feature-complete Alpha |
| 44 | Online Identity & Cloud Save — Optional | Online + Unity | Optional Online Services |
| 45 | UI/UX Integration & Accessibility | UI/UX + Unity | Release Preparation |
| 46 | VFX Production & Integration | Unity + Model | Release Preparation |
| 47 | Final Model Replacement & Asset Optimization | Unity + Model | Release Preparation |
| 48 | Windows Release Preparation | Platform + Unity | Windows Release Candidate |
| 49 | Mobile UI Adaptation | UI/UX + Unity | Mobile Release Preparation |
| 50 | iOS Adaptation | Platform + Unity | iOS Release Candidate |

---

## 5. Global Phase Gates

每个 Phase 的具体 tests 不同，但必须满足以下共同 gate。

### 5.1 Design Gate

- Goal、Included 和 Not Included 明确。
- File responsibilities 和 system interfaces 明确。
- Open Design Questions 已解决，或明确不在本 Phase 处理。
- 难点、风险、常见 bugs 和 recovery strategy 已记录。
- 用户批准 design 后才写 implementation plan。

### 5.2 Implementation Gate

- 使用小步 implementation，优先采用 failing test → minimal implementation → passing test。
- 每个 task 完成后运行 focused tests。
- 不把多个未验证 subsystem 同时接入 Scene。
- 缺失 dependency 时 fail clearly，不允许静默产生错误状态。
- Runtime data 不以 UI 或临时 Scene reference 作为唯一可信来源。

### 5.3 Verification Gate

- 当前 Phase focused tests 全部通过。
- 所有旧 Phase regression tests 全部通过。
- Integration tests 覆盖本 Phase 与直接 dependencies。
- 必要的人工 Play Mode checklist 完成。
- Console 没有未处理 error。
- 如果修改持久数据，Save / Load、invalid data 和 migration tests 通过。
- Roadmap 只在全部 gate 通过后记录 `Completed` evidence。

### 5.4 Feature UI 与 Functional Feedback Rule

- 每个 gameplay Phase 同时交付完成该功能所需的可用 UI，不把所有页面推迟到 Phase 45。
- Feature UI 必须复用 Phase 5 的 Design System 和 navigation rules。
- 每个功能同时交付用于理解状态的 functional feedback，例如 placement validity、Order ready、Coins change、Unlock 或 Error reason。
- Phase 45 负责全局 UI/UX integration、onboarding 和 accessibility；Phase 46 负责正式 VFX，不负责第一次补齐缺失的核心反馈。

---

# Milestone A — Visual & Layout Foundation

## Phase 0 — Project Foundation

> 状态：Completed
>
> 完成日期：2026-07-25
>
> 验证环境：Unity `6000.5.5f1` / Windows
>
> 原始验收结果（2026-07-25）：16 / 16 Play Mode tests passed；`MainCafe` Scene load 与 Console error scan passed；Camera pan、zoom、bounds、isometric baseline、selection feedback、Pause、`1x` 和 `2x` 已完成人工验收。
>
> Completed-phase hardening evidence（2026-07-30）：Game Time owner hardening 后 baseline 为 21 / 21 PlayMode；merge 前 review hardening fresh full EditMode `116 / 116`、fresh full PlayMode `31 / 31` passed，failed、skipped、inconclusive 均为 `0`。新增 Scene-owned root canonicalization、disabled / inactive / destroyed selection cleanup、真实 Input System mouse integration coverage，以及 single-warning、可恢复的 missing-material handling。鼠标在 UGUI 控件上按下并松开时，pointer release 不会再穿透到世界中的 selection。
>
> Completed-phase hardening manual evidence（2026-07-30）：用户确认 `MainCafe` Hierarchy 中 `Phase0_Runtime`、`Phase0_TimeControls`、`EventSystem` 各一个；Camera pan / zoom、Pause / `1x` / `2x` 正常；Console clean；Test Runner EditMode `116 / 116`、PlayMode `31 / 31` 全部通过。

### Goal

提供所有后续 Scene interaction 和 runtime systems 共用的基础。

### Completed Scope

- 固定斜俯视 Camera、pan、zoom 和 bounds。
- Mouse camera input，并保留未来 touch input boundary。
- Scene selection 与 visual feedback。
- Pause、`1x` 和 `2x` Game Time。
- Event Bus、runtime assembly 和基础 Play Mode tests。
- `Phase0_Runtime`、`Phase0_TimeControls` 与 `EventSystem` inactive / duplicate root repair。
- Disabled、inactive 或 destroyed selectable 的安全 selection cleanup。
- 真实 mouse press / drag / release、same-frame cache 与 Pause input regression。

### Regression Requirement

原始 16-test acceptance evidence 保留为历史记录；当前 regression baseline 是 full EditMode `116` tests 与 full PlayMode `31` tests。后续 Phase 必须保持所有现有 tests 通过，并在 test count 改变时记录 fresh totals。

---

## Phase 1 — Layout Data Model

> 状态：Completed
>
> 验证环境：Unity `6000.5.5f1` / Windows
>
> Automated evidence（2026-07-27）：review fix 后 fresh final EditMode `116 / 116` passed；fresh final PlayMode `18 / 18` passed；两轮均为 failed `0`、skipped `0`、inconclusive `0`。Layout Domain source boundary scan、旧 Phase 1 runtime scan、orphan metadata scan 与 `git diff --check` 通过；Unity test drift 已恢复，generated worktree `.slnx` 已删除。
>
> Manual evidence（2026-07-27）：用户完成 spec §18 Scene、PlayMode 和 Test Runner checks；Scene 无 demo cubes、旧模型或临时 floor，controls 与 mouse input 无 Console error，Console clean。用户已明确批准 merge。
>
> Merge evidence（2026-07-27）：`codex/phase1-layout-data-model` 已 fast-forward merge 到本地 `main`。merged `main` fresh EditMode `116 / 116` passed；fresh PlayMode `18 / 18` passed；failed、skipped、inconclusive 均为 `0`。

### Goal

建立不依赖 Unity Scene GameObject 的咖啡厅布局数据。

### Scope

- `CafeLayout`
- `FurnitureDefinition`
- `FurnitureInstance`
- Stable furniture instance ID
- Grid position、rotation 和 footprint
- 已解锁 Grid cells / regions
- Layout Zone type：`Interior`、`Exterior` 与未来 special area
- Placement surface compatibility 的扩展字段
- Definition lookup 与 data validation

### Why Now

后续 Decoration、Functional Furniture 和 Save 都依赖稳定 Layout data。先建立数据边界，可以避免 Scene 成为唯一真相。

### Main Difficulty 与 Solution

难点是区分家具类型和玩家放置的家具实例：

```text
FurnitureDefinition = 某种家具是什么
FurnitureInstance = 玩家放在具体位置的那一件家具
```

Definition 保存静态规则；Instance 只保存 stable ID、definition ID、位置、旋转和玩家状态。

### Risks / Likely Bugs

- Definition ID 与 Instance ID 混用。
- Scene object reference 被错误写进纯数据。
- Duplicate stable IDs。
- Invalid rotation 或 footprint 被接受。

### Tests

- Furniture instance IDs 唯一。
- Definition lookup 成功与失败行为明确。
- Position、rotation 和 footprint data 正确。
- Invalid definition、rotation 和 footprint 被拒绝。
- Layout domain tests 不需要加载 `MainCafe` Scene。

### Not Included

Grid occupancy、UI、Scene placement、functional anchors、Save file，以及实际 Exterior gameplay。

---

## Phase 2 — Grid Occupancy & Placement Rules

状态：Completed

验证环境：Unity 6000.5.5f1 / Windows

Automated evidence（2026-07-27）：fresh final EditMode 184 / 184 passed；fresh final PlayMode 18 / 18 passed；failed、skipped、inconclusive 均为 0。Placement/transaction/consistency、source boundary、旧 Phase regression 与 git diff check 已通过。

Latest-main integrated evidence（2026-07-30）：Phase 0 Scene cleanup 1 / 1、Phase 0 PlayMode 25 / 25、GridPlacementTests 67 / 67、full EditMode 184 / 184、full PlayMode 31 / 31 passed；failed、skipped、inconclusive 均为 0。Layout production source 没有 UnityEngine reference，Assets 中没有遗留 AddFurnitureInstance call，conflict marker 与 git diff check 均通过。

PR hardening Option A fresh full regression evidence（2026-07-30，状态仍为 In Review）：`Logs/Phase2PrHardeningOptionAFullEditMode.xml` 为 EditMode `191 / 191` passed；`Logs/Phase2PrHardeningOptionAFullPlayMode.xml` 为 PlayMode `35 / 35` passed；failed、skipped、inconclusive 均为 `0`。覆盖 Phase 0 与 Phase 1 regression。真实 `InputSystemUIInputModule`、Canvas、`GraphicRaycaster` 与 virtual Mouse tests 保护 UI 不穿透到 world selection；`FurnitureDefinition` footprint 超过 `1024` cells 会在 placement 前被拒绝，避免错误的巨大数据创建很长的 cell list。

PR hardening Option A manual evidence（2026-07-30）：用户在 `MainCafe` Play Mode 中创建 temporary `ColorSelectable` Cube，确认点击 `Pause`、`1x`、`2x` 后 Cube selection 一直保持；Camera pan / zoom 正常；Console clean。退出 Play Mode 后 temporary Cube 自动移除。

Manual evidence（2026-07-30）：用户确认 EditMode `184 / 184`、PlayMode `31 / 31` 全部通过；`GridPlacementTests` categories 完整；`MainCafe` 没有提前出现 Phase 2 可见 Grid、furniture、preview 或 UI；mouse pan、wheel zoom、Pause、`1x`、`2x` 正常；Console clean；Beginner Guide 可理解。

Merged-main evidence（2026-07-31）：GitHub PR #1 已 merge 到 `main`，merge commit 为 `abf6729`。在该 merge commit 的独立 worktree 中运行 fresh full regression：EditMode `191 / 191`、PlayMode `35 / 35` passed；failed、skipped、inconclusive 均为 `0`。Phase 2 的 design、implementation、review、manual acceptance、merge 与 merged-main regression gates 已全部通过，因此状态更新为 `Completed`。

### Goal

用纯规则判断家具能否放置、移动、旋转或移除。

### Scope

- Grid bounds
- Unlocked regions
- Footprint rotation
- Occupy / release
- Overlap detection
- Placement transaction
- Move / rotate / remove validation

### Why After Phase 1

Occupancy 必须操作稳定的 Layout data。先证明规则正确，Phase 3 的 visual placement 才不会混合 UI bugs 与数据 bugs。

### Main Difficulty 与 Solution

移动家具使用 transaction：

```text
保留旧状态
→ 验证完整新位置
→ 全部合法才 commit
→ 失败则保持旧状态
```

### Risks / Likely Bugs

- Grid boundary off-by-one。
- `90°` 旋转后 footprint 宽高错误。
- Move 失败后旧 cells 没有恢复。
- 同一个 Instance 重复占格或重复释放。

### Tests

- `1×1`、矩形和非正方形 footprint。
- `0° / 90° / 180° / 270°` rotation。
- Bounds、locked region 和 overlap rejection。
- Failed move 保留旧位置。
- Repeated remove / release 安全。
- Occupancy 与 Layout instances 数量一致。

### Not Included

Mouse placement、preview、Scene rendering、pathfinding。

---

## Phase 3 — Visual Style & Asset Pipeline Foundation

### Goal

建立所有正式 Models、Materials、Textures 和 Unity Prefabs 共用的视觉方向与技术标准。

### Status

`In Review` — Task 1–6 implementation and fresh automated evidence are
available, but Studio Owner manual Camera/readability acceptance and explicit
source license/use-right confirmation remain pending. Phase 3 must not be
recorded as `Completed` before both gates are resolved.

Fresh Task 7 automated evidence (2026-08-01):

- full EditMode: `285 / 285` passed; failed/skipped/inconclusive all `0`;
- full PlayMode: `48 / 48` passed; failed/skipped/inconclusive all `0`;
- production benchmark validator: `3 / 3` Prefabs valid, `0 issues`;
- `MainCafe.unity` remains unchanged; PlayMode cleanup restored Build Settings
  to its single enabled `Assets/Scenes/MainCafe.unity` entry.

Known limitation: this is a benchmark pipeline and readability baseline only.
It is not the Phase 4 formal asset set, gameplay, placement, or runtime
integration.

### Scope

- Art direction、color palette 和 shape language
- Unity / DCC tool scale convention
- Pivot、forward direction、rotation 和 naming
- Source files、FBX export 和 Unity import rules
- Material、texture、shader 和 folder conventions
- Collider、LOD 和 mobile asset budgets
- Prefab assembly 与 asset validation checklist
- Unity primitives 的 placeholder color / meaning rules

### Why After Grid Rules

Grid cell、footprint 和 rotation 已稳定，Model standards 才能使用真实空间约束；更早决定容易因 Grid 改变而返工。

### Main Difficulty 与 Solution

视觉风格和技术规格必须同时成立。先制作少量 benchmark assets，验证从 source file 到 Unity Prefab 的完整 pipeline，再批准大量生产。

### Risks / Likely Bugs

- Source scale 与 Unity scale 不一致。
- Pivot 和 forward direction 在不同 Models 中不统一。
- Materials 或 textures 使用 machine-specific paths。
- Art style 很漂亮但 footprint、Collider 或 mobile budget 不可用。

### Tests

- Benchmark asset import scale、pivot 和 forward validation。
- Material / texture references 完整。
- Naming 与 folder validator。
- Collider 和 LOD policy checks。
- Windows shader compatibility。
- Mobile budget baseline。
- Source → export → import → prefab 流程可重复。

### Not Included

大量正式 Models、角色 Rig、完整 UI 页面和 gameplay implementation。

---

## Phase 4 — Core Architecture & Functional Furniture Models

### Goal

制作第一批符合正式规格的建筑结构和核心功能家具。

### Scope

- Floor、wall、door 和 window
- Counter
- Cash Register
- Coffee Machine
- Pick-up shelf / table
- Basic cup
- Source files、Models、Materials、Colliders 和 Unity Prefabs
- Accurate footprint、surface 和 anchor markers

### Why Before Decoration

Basic Decoration 和 Functional Furniture 必须用真实或生产级尺寸验证 footprint、pivot、Surface 和 Interaction Anchors。

### Main Difficulty 与 Solution

先制作一个小而完整的 vertical asset set，不追求全游戏数量。每个 Model 同时通过 visual review 和 technical validation。

### Risks / Likely Bugs

- Model 视觉尺寸与 Grid footprint 不一致。
- Door、counter 或 machine pivot 导致 placement 偏移。
- Collider 阻挡原本可用的 Interaction Anchor。
- Prefab 修改覆盖 source import data。

### Tests

- Scale、pivot、rotation 和 footprint。
- Collider 与 visual bounds。
- Door / window wall attachment。
- Functional furniture surface 和 anchor markers。
- NavMesh obstacle smoke test。
- Missing material / texture / reference scan。
- Scene 中的 primitive 可被对应正式 Prefab 安全替换。

---

## Phase 5 — UI Architecture & Design System

### Goal

建立所有功能 UI 共用的结构、视觉语言和 input boundary。

### Scope

- UI technology 与 assembly boundary
- Canvas / panel architecture
- Reusable button、panel、text、icon 和 modal components
- Typography、color 和 spacing tokens
- Notification、tooltip 和 validation-message patterns
- Resolution scaling
- Mouse / touch-compatible interaction
- UI input blocking 和 Scene input boundary
- Safe Area、accessibility 和 localization expansion points

### Why Before Decoration UI

Decoration Mode 是第一个 UI-heavy system。先建立 reusable system，避免每个 gameplay feature 自建不兼容的 buttons、panels 和 modals。

### Main Difficulty 与 Solution

先建立小型 component library 和 interaction rules，不制作所有最终页面。Feature phases 负责使用这些 components 实现自己的 UI。

### Risks / Likely Bugs

- UI click 穿透到 Scene。
- Modal 打开后底层 UI 仍可操作。
- Text overflow 或不同 resolution 下重叠。
- Mouse 与未来 touch rules 冲突。

### Tests

- Multi-resolution scaling。
- Modal input blocking。
- UI / Scene click separation。
- Pause 时 UI 仍可操作。
- Text overflow 和 long-label fixtures。
- Minimum touch-target boundary。
- Theme component visual consistency。

### Not Included

Title Screen、完整 feature pages、正式 icons、tutorial 和 mobile-specific layout。

---

## Phase 6 — Basic Decoration Mode

### Goal

让玩家在 Scene 中安全地放置和调整普通家具。

### Scope

- 进入 Decoration Mode 自动 Pause。
- Furniture catalogue placeholder。
- Placement preview。
- Select、move、rotate、confirm、cancel 和 store。
- Valid / invalid visual feedback。
- Layout data 与 Scene representation 同步。

### Why After Phase 2

UI 只调用已测试的 placement rules，不自己决定合法性。

### Main Difficulty 与 Solution

Preview 使用临时 placement state，不直接修改正式 Layout。只有 Confirm 才提交 transaction。

### Risks / Likely Bugs

- Preview 意外写入正式 Layout。
- Cancel 后家具丢失。
- UI click 穿透到 Scene。
- Scene representation 与 Layout data 不一致。
- Decoration Mode 退出后 Game Time 未恢复到预期状态。

### Tests

- Preview 不改变正式 Layout。
- Confirm 只提交一次。
- Cancel 恢复原位置。
- Illegal placement 不能确认。
- Rotate、move、store 后 data 与 Scene 一致。
- Decoration Mode 强制 Pause。
- UI interaction 不触发 Scene placement。
- 人工检查 preview、颜色反馈和 beginner 操作流程。

### Not Included

经营功能、anchors、路径验证、家具商店、Atmosphere。

---

## Phase 7 — Functional Furniture & Layout Readiness

### Goal

让经营系统通过 furniture capabilities 和 interaction anchors 使用布局，而不是依赖固定 Scene object names。

### Scope

- Furniture capability definitions。
- Cash Register、Coffee Machine 和 Pick-up Surface。
- Employee / customer interaction anchors。
- Pick-up Surface Slot。
- Anchor rotation 与 validity。
- Layout readiness report。
- 缺少必要功能或路径准备条件时禁止营业。

### Why Before Cafe Loop

Order 和 NPC systems 从一开始就使用可移动 functional furniture contract，避免之后实现装修时重写固定 stations。

### Main Difficulty 与 Solution

Anchors 使用 furniture-local coordinates。家具位置或旋转变化后重新计算 world position，并重新验证 occupancy 与 navigation sampling。

### Risks / Likely Bugs

- 家具旋转后 anchor 没有旋转。
- Anchor 落在家具、墙体或 locked cell 内。
- 旧 anchor 在家具移动后仍被 NPC 使用。
- 错误提示只说“布局无效”，没有指出原因。

### Tests

- 四个 rotation 下的 anchor coordinates。
- Blocked anchor 被标记 invalid。
- 缺少 Cash Register、Coffee Machine 或 Pick-up 时不能营业。
- Furniture move 后旧 anchors invalidated。
- Readiness report 包含具体 furniture 和 failure reason。
- 人工检查 Scene gizmos / feedback 与 data 一致。

### Milestone Gate

玩家能装修基础空间；功能家具由通用 capability 和 anchors 描述；还没有 Customer 经营。

---

# Milestone B — Core Cafe Loop

## Phase 8 — Order Domain

### Goal

建立 Customer、Cashier 和 Barista 共用的唯一 Order state authority。

### Scope

- Unique Order ID
- Order state machine
- FIFO waiting queue
- Create、claim、transition、complete 和 fail
- Duplicate-operation protection

### Why Now

Order 是完整 Cafe Loop 的业务核心，先以纯逻辑验证，不混入 NPC 或 NavMesh。

### Main Difficulty 与 Solution

所有 state changes 只能通过 `OrderService`，不允许不同 controllers 直接修改 Order state。

### Risks / Likely Bugs

- Duplicate claim / completion。
- Illegal state transitions。
- FIFO 顺序错误。
- Failed Order 重新进入 queue。

### Tests

- IDs 唯一递增。
- FIFO claim。
- 同一 Order 不能 claim 或 complete 两次。
- Invalid transition 不改变 state。
- Completed 和 Failed 是 terminal states。
- Order list 和 waiting queue 保持一致。

---

## Phase 9 — Capacity & Reservation

### Goal

统一管理 active customers、Counter Queue 和 Pick-up 的容量及预留。

### Scope

- Total customer capacity
- Counter Queue capacity
- Pick-up capacity
- Reserve / occupy / release
- Atomic capacity transactions

### Why Separate From Orders

Capacity bugs 会造成负数、超员、reservation leak 和系统死锁，必须独立测试。

### Main Difficulty 与 Solution

Service 返回明确 reservation token / ownership，只有 owner 可以完成或释放对应 reservation。

### Risks / Likely Bugs

- Capacity 超过 max 或低于 zero。
- Reserve 失败却部分修改状态。
- Repeated release。
- Customer 离开后 reservation 未释放。

### Tests

- 每种 capacity 的 upper / lower bounds。
- Failed reserve 不改变任何 count。
- Repeated release idempotent。
- Pick-up 满时拒绝新 reservation。
- Release 后可以再次 reserve。
- Reservation ownership 正确。

---

## Phase 10 — Navigation & Movement Recovery

### Goal

提供 Customer 和 Employee 共用的可靠 NavMesh movement。

### Scope

- Destination sampling
- Path validation
- Arrival detection
- Scaled movement
- Timeout、retry、recovery point
- Safe cleanup callback
- Furniture layout change invalidation

### Why Before Characters

Customer 和 Employee state machines 只需要处理 movement result，不重复实现 NavMesh failure logic。

### Main Difficulty 与 Solution

移动前验证 destination 和 complete path；第一次 timeout 重新计算一次；第二次进入 configured recovery；无法 recovery 时返回明确 failure。

### Risks / Likely Bugs

- Agent 不在 NavMesh。
- Destination unreachable。
- Pause 时 timeout 继续累计。
- Layout change 后沿旧 path 穿过家具。
- Agent 永久卡住或不断 retry。

### Tests

- Valid path arrival。
- Invalid destination immediate failure。
- Pause 不推进 timeout。
- `2x` 正确影响 movement。
- 只允许一次 retry。
- Recovery 成功和失败路径。
- Layout changed 时 current path invalidated。
- Cleanup callback 只调用一次。

---

## Phase 11 — Customer Spawn & Counter Queue

### Goal

独立完成 Customer 的进入、排队、前移和安全离场。

### Scope

- Customer spawn interval
- Entrance / exit
- Counter Queue slots
- Queue order 和 forward movement
- Total / queue capacity integration
- Spawn pause / resume

### Why Before Employees

先验证 Customer lifecycle 与 physical queue，不同时加入收银、制作和 Order ownership。

### Main Difficulty 与 Solution

Queue order 由一个 queue authority 管理；Customer 不自行决定 slot。Slot change 通过明确 assignment 推进。

### Risks / Likely Bugs

- Queue 顺序交换。
- 前方离开后后方不前移。
- Spawn failure 留下 ghost capacity。
- 同时生成超过上限。

### Tests

- Customer 总数和 Queue 不超过 capacity。
- Queue 保持 arrival order。
- Front slot 释放后正确前移。
- 满容量暂停 spawn，释放后恢复。
- Invalid spawn location 不生成半成品 Customer。
- Failed / removed Customer 正确释放 capacity。

---

## Phase 12 — Employee Task Flow

### Goal

让固定 Cashier 和 Barista 可靠执行抽象工作任务。

### Scope

- Employee state machine
- Station capability lookup
- Cashier service task
- Barista preparation / delivery task
- Move、work、return idle
- Scaled work duration
- Task cancellation 和 recovery

### Why Before Integration

Employee workflow 先用 controlled tasks 验证，Phase 10 再连接真实 Customer 和 Order。

### Main Difficulty 与 Solution

Employee 一次拥有一个 task handle；task 负责声明 station capability、target anchors、duration 和 cleanup。

### Risks / Likely Bugs

- 同一 Employee 同时领取多个 tasks。
- Station 消失后 task 永久等待。
- Pause 时 work timer 继续。
- Failure 后 Employee 无法回到 Idle。

### Tests

- 一次只执行一个 task。
- 找到正确 capability station。
- Missing station 明确失败。
- Pause / `1x` / `2x` work timing。
- Task cancellation 只 cleanup 一次。
- Failure 后 Employee 可返回 Idle。

---

## Phase 13 — Integrated Takeout Cafe Loop

### Goal

整合第一个完整、自动运行的外带经营循环。

### Scope

```text
Customer spawn
→ Counter Queue
→ Cashier payment
→ Order creation
→ Barista FIFO preparation
→ Pick-up delivery
→ Correct Customer collection
→ Customer exit
```

- 两名固定岗位员工：Cashier 和 Barista。
- 一种基础 Coffee，不使用 Inventory。
- Read-only status UI。
- 营业中 functional furniture protection。

### Why After Phases 5–9

Order、Capacity、Movement、Customer 和 Employee 已分别通过 tests，integration failures 可以缩小到 system boundaries。

### Main Difficulty 与 Solution

使用 stable Customer ID、Order ID、reservation ownership 和 task handles 连接各系统，不以 Scene object equality 作为长期身份。

### Risks / Likely Bugs

- Customer 领取错误 Order。
- Pick-up reservation leak。
- Duplicate payment / delivery / collection。
- Employee failure 阻塞全部 Queue。
- 营业中移动必要家具导致 runtime references 失效。

### Tests

- 至少连续完成 20 个 automated Orders。
- FIFO 与 Order ownership。
- Duplicate prevention。
- Capacity full → pause → recovery。
- Movement / task failure cleanup。
- Pause、`1x`、`2x` 控制完整循环。
- 营业中必要 furniture 不允许直接移动，或必须先打烊。
- Status UI 与 runtime data 一致。
- Console 无未处理 error。

### Milestone Gate

完成 **Core Cafe Loop Prototype**：咖啡厅可以在有效玩家布局中自动完成连续外带服务。

---

# Milestone C — Playable Management MVP

## Phase 14 — Business Day Lifecycle

### Goal

建立 Closed、Open、Closing、Report 和 Next Day 的明确边界。

### Scope

- Start day / open cafe
- Business clock
- Early close
- Automatic close
- End-of-day cleanup
- Next day transition

### Why Before Economy

收入和统计必须明确属于哪个营业日，Save 也需要稳定 day boundary。

### Risks / Likely Bugs

- Closed 时仍生成 Customer。
- 打烊时遗留 active Orders。
- Pause 仍推进 business clock。
- Day number 增加两次。

### Tests

- Closed 不运行 cafe loop。
- Open / early close / automatic close transitions。
- Pause 不推进 business clock。
- Closing cleanup 只执行一次。
- Active Orders 按已批准规则完成或安全终止。
- Day number 每次只增加一次。

---

## Phase 15 — Economy & Daily Report

### Goal

记录基础 Coins、Product price、Revenue 和每日经营结果。

### Scope

- Coins balance
- Base product price
- Payment transaction
- Daily metrics
- Daily Report

### Why After Day Lifecycle

Daily Report 依赖可靠的 day start / end，不能使用不明确的统计清零时机。

### Risks / Likely Bugs

- Order 重复收费。
- Failed Order 错误计入完成收入。
- Report 与实际 transactions 不一致。
- UI refresh 改变 Coins。

### Tests

- 每个 Order 只支付一次。
- Transaction sum 等于 Coins change。
- Report Order count / Revenue 准确。
- 下一天 metrics 正确 reset。
- UI 是 read-only projection。
- Failed / recovered Order 遵守已批准 payment rule。

---

## Phase 16 — Save Foundation

### Goal

可靠保存 Layout、day、coins 和后续系统可扩展的 versioned game state。

### Scope

- Save schema 与 `saveVersion`
- Stable IDs
- Atomic write
- Backup 与 recovery
- Load validation
- Layout、day、coins 和当前必要状态

### Why Now

从此处开始持久状态快速增加。更早设计会缺少真实需求，更晚加入会迫使多个系统同时重构。

### Main Difficulty 与 Solution

只保存纯数据和 stable IDs；先写 temporary file，验证成功后再替换正式 Save；保留最近有效 backup。

### Risks / Likely Bugs

- 保存 Scene object references。
- 写入中断破坏唯一 Save。
- Unknown Definition ID 导致家具静默消失。
- Load 重复生成 instances。

### Tests

- Save / Load round trip。
- Corrupted newest Save 恢复 backup。
- Write failure 不覆盖旧 Save。
- Unknown ID 显示明确错误。
- Repeated Load 不重复生成 data。
- Save version mismatch 走明确 migration / rejection path。

---

## Phase 17 — Application Shell & Loading

### Goal

建立从启动游戏到安全进入 Cafe 的完整 application flow。

### Scope

- Boot scene / initialization state
- Splash / logo presentation
- Title Screen
- New Game、Continue 和 Settings
- Loading screen 与 progress / status feedback
- Save selection、load failure 和 recovery UI
- Scene transition 和 duplicate-load protection
- Return to Title
- First-launch flow

### Why After Save Foundation

`Continue`、`New Game` 和 recovery 必须连接真实 versioned Save。更早实现只能产生无法验证的假流程。

### Main Difficulty 与 Solution

使用明确 Boot state machine，初始化 services、读取 Save、加载 Scene 和进入 gameplay 各有单一 ownership；重复操作返回同一个 active transition。

### Risks / Likely Bugs

- 重复点击造成 Scene 加载两次。
- Load 失败后停留在不可恢复黑屏。
- Return to Title 保留旧 runtime objects。
- Loading UI 完成但 gameplay 尚未初始化。

### Tests

- New Game、Continue 和 No-Save states。
- Corrupted / incompatible Save recovery。
- Duplicate-load protection。
- Loading failure recovery。
- Scene initialization order。
- Return to Title cleanup。
- First-launch persistence。
- Manual launch-to-game smoke test。

### Not Included

Online login、Cloud Save、正式 VFX 和 mobile platform lifecycle。

---

## Phase 18 — Recipe & Menu

### Goal

定义 Products、Recipes、required equipment 和当前可售 Menu。

### Scope

- Product Definition
- Recipe Definition
- Equipment requirements
- Menu enable / disable
- Product availability query

### Why Before Inventory

Inventory 必须依据 Recipe 检查和预留材料；先定义商品需求，再管理材料。

### Risks / Likely Bugs

- Recipe references missing Product / Ingredient。
- Disabled Product 仍被 Customer 选择。
- Equipment 忙碌被误判为设备不存在。
- Menu 出现 duplicate Product。

### Tests

- Definition reference validation。
- Missing required equipment 时 unavailable。
- Busy equipment 仍允许 Order 进入 FIFO。
- Disabled Product 不可点单。
- Menu 无 duplicate entries。
- Availability result 包含明确 reason。

---

## Phase 19 — Inventory, Reservation & Restocking

### Goal

让 Order、Recipe 和 Inventory 使用一致、可恢复的材料 transaction。

### Scope

- Ingredient inventory
- Available / reserved / consumed
- Payment-time reservation
- Preparation-time consumption
- Recoverable failure handling
- Basic restocking
- Minimum-income basic supply

### Why After Orders, Save and Recipe

Inventory transaction 依赖稳定 Order states、Recipe requirements 和 persistence。

### Main Difficulty 与 Solution

每个 reservation 绑定 Order ID；所有 reserve、consume 和 release 使用原子 transaction；总量必须满足守恒检查。

### Risks / Likely Bugs

- 多个 Orders 使用同一材料。
- Reserved inventory 永久泄漏。
- Retry 导致重复消耗。
- Save / Load 后 available + reserved 不一致。

### Tests

- Insufficient inventory 阻止付款。
- Multiple Orders 不重复预留。
- Consume 只执行一次。
- Recoverable equipment / path failure 保持 reservation。
- Final cancellation 正确释放。
- Restock 后 Product 恢复销售。
- Save / Load 前后 inventory 总量守恒。
- Minimum supply 不可囤积或直接出售。

### Milestone Gate

完成 **Playable Management MVP**：玩家可以装修基础店面、营业多个 days、管理 Menu 和 Inventory，并可靠 Save / Load。

---

# Milestone D — Character-driven Vertical Slice

## Phase 20 — Core Character Models & Animation Pipeline

### Goal

建立可扩展的动物角色 Model、Rig、Animation 和 Unity character Prefab pipeline。

### Scope

- Reference animal body proportions
- 第一批 Customer、Cashier 和 Barista Models
- Shared / compatible Rig strategy
- Idle、walk、work 和 carry animations
- Character pivot、Collider 和 NavMeshAgent standards
- Held-item / interaction anchors
- Character Materials 和 Prefabs
- Animation import / controller conventions

### Why Before Character Systems

Phase 8–13 可用 Capsule 验证逻辑；在 Staff identity 和 Species 差异进入正式体验前，必须证明角色视觉与 animation pipeline 可扩展。

### Main Difficulty 与 Solution

不同 Species 体型可能无法直接共享 Rig。先完成一个 reference body 和少量可变比例，再验证 animation reuse；特殊体型后续使用明确 variation strategy。

### Risks / Likely Bugs

- Root motion 与 NavMeshAgent 冲突。
- Collider、Agent radius 与视觉体型不一致。
- Carry anchor 导致杯子漂浮。
- Rig 或 animation import settings 不一致。
- 新 Species 破坏已验证的 animation。

### Tests

- Model scale、pivot 和 forward direction。
- Rig / animation import validation。
- Idle / walk / work / carry state transitions。
- NavMesh movement 与 animation synchronization。
- Collider / Agent bounds。
- Held-item anchor。
- Missing bones、materials 和 clips scan。
- Windows performance baseline。

### Not Included

全部 Species、Breed variants、完整 facial animation 和所有正式 costumes。

---

## Phase 21 — Staff Identity & Work Capabilities

### Goal

加入稳定 Staff identity、多员工与基础工作能力。

### Scope

- Character ID
- Species、Breed、display name、appearance
- Staff capabilities
- Multiple employees
- Task assignment
- Basic staff details

### Why After Stable Cafe Loop

先有可靠 task flow，再加入多人分配，避免同时调试基础任务和调度。

### Risks / Likely Bugs

- 两名 Staff 领取同一个 task。
- Character ID 在 Load 后改变。
- 缺少 capability 的 Staff 接受任务。
- 移除 Staff 破坏 active Order。

### Tests

- Stable ID across Save / Load。
- Task ownership unique。
- Capability filtering。
- Staff add / remove recovery。
- 多 Staff 不破坏 FIFO 和 Inventory。

---

## Phase 22 — Customer Identity & Returning Visits

### Goal

让 Customer 可以成为被玩家记住的 returning character。

### Scope

- Stable Customer ID
- Preferences
- Visit history
- Returning-customer selection
- Customer profile

### Why Before Traits

Traits、Mood 和 Relationships 都需要可靠确认“这是同一个 Customer”。

### Risks / Likely Bugs

- 同一 unique Customer 同时出现两次。
- Visit count 重复增加。
- Returning selection 永远偏向少数角色。
- Preferences 在 Load 后丢失。

### Tests

- Stable ID 与 visit history。
- 同一 unique Customer concurrency protection。
- Returning selection boundaries。
- Preferences persistence。
- Random regular Customer 可升级为 returning Customer。

---

## Phase 23 — Traits, Mood & Relationships

### Goal

加入克制、可读且可保存的角色差异。

### Scope

- Small approved Trait set
- Mood
- Cafe relationship
- Character-to-character relationship
- Hidden Trait discovery
- Bounded behavior modifiers

### Why After Identity

所有变化必须绑定 stable Character ID，且需要真实经营行为作为观察来源。

### Main Difficulty 与 Solution

Species 和 Breed 只提供概率倾向；Traits 使用有上下限的 modifiers，不直接硬编码完整行为结果。

### Risks / Likely Bugs

- Trait modifiers 无限叠加。
- Breed 被写成绝对性格。
- Mood 每帧更新造成快速漂移。
- Relationship 超出范围。

### Tests

- Modifier bounds。
- 相同 Breed 仍可生成不同 Traits。
- Mood / Relationship clamping。
- Hidden Trait discovery conditions。
- Save / Load persistence。
- Character modifiers 不破坏 Capacity、Order 和 Inventory invariants。

---

## Phase 24 — Events & Player Decisions

### Goal

让经营状态和角色关系产生简单、可选择的事件。

### Scope

- Event definitions
- Trigger conditions
- Pending event
- Player choices
- Outcomes
- Small chained events

### Why Last in Character Milestone

Events 会读取 Order、Economy、Character、Mood 和 Relationships，必须建立在稳定 state systems 上。

### Risks / Likely Bugs

- Event 重复触发。
- Choice outcome 应用两次。
- Pending Event 阻塞普通经营。
- Save / Load 后条件重新触发。

### Tests

- Trigger conditions。
- Duplicate prevention。
- Choice applies once。
- Outcome data correctness。
- Pending event persistence。
- Event 不阻塞 cafe loop。

### Milestone Gate

完成 **Character-driven Vertical Slice**：玩家能经营咖啡厅，并逐渐认识具有差异的 Staff 和 returning Customers。

---

# Milestone E — Cafe Growth 与 Advanced Decoration

## Phase 25 — Interior Furniture Model Set

### Goal

制作支持成长、扩建、Dine-in 和主题装修的第一批正式室内家具。

### Scope

- Tables、chairs、shelves 和 lights
- Plants 和 decorative furniture
- Basic theme starter set
- Bakery / Merchandise 基础 display furniture
- Source files、Models、Materials、Colliders 和 Prefabs
- Furniture catalogue thumbnails / icons 的生产边界

### Why After Management MVP

真实 Cafe Loop 和 Decoration playtest 已提供尺寸、通道、可读性和互动需求，避免提前制作大量无法使用的家具。

### Main Difficulty 与 Solution

以小型 modular set 覆盖多种布局，不追求大量单件。每件家具必须先通过 footprint、pathfinding 和 camera readability，再进入 content catalogue。

### Risks / Likely Bugs

- 家具细节在固定 Camera 下不可读。
- Table / chair footprint 与 Dine-in anchors 不兼容。
- Decorative Collider 造成不必要堵路。
- Theme pieces 只能组合成单一布局。

### Tests

- Scale、pivot、footprint 和 rotation。
- Collider / NavMesh behavior。
- Camera-distance readability。
- Table / chair anchor compatibility。
- Material / texture / reference scan。
- Modular combination fixtures。
- Windows performance baseline。

---

## Phase 26 — Progression & Unlocks

### Goal

让经营结果转化为 equipment、menu 和 strategy choices。

### Scope

- Unlock conditions
- Equipment / Menu unlocks
- Strategy points
- Affordable / Premium 基础 branches
- Mixed build support

### Why Now

只有核心经营与角色体验稳定后，才能判断 upgrade 是否真正有价值。

### Risks / Likely Bugs

- Reward 重复领取。
- Locked content 可被绕过。
- Strategy route 形成意外永久互斥。
- Upgrade 破坏最低收益保障。

### Tests

- Unlock idempotency。
- Locked-content enforcement。
- Persistence。
- Strategy combinations。
- Minimum-income regression。

---

## Phase 27 — Store Expansion

### Goal

允许玩家购买新的矩形 Grid 区域并扩大可用店面。

### Scope

- Expansion definitions
- Cost transaction
- Region unlock
- Occupancy integration
- Camera bounds update
- Expanded-layout validation

### Why After Progression

Expansion 需要明确 cost、unlock source 和 gameplay value。

### Risks / Likely Bugs

- Cost 重复扣除。
- Locked cells 仍可放置。
- 新区域与旧区域断开。
- Camera bounds 未更新。

### Tests

- Purchase transaction idempotency。
- Locked / unlocked placement。
- Region adjacency / connectivity。
- Camera bounds。
- Save / Load unlocked regions。

---

## Phase 28 — Exterior Model Set

### Goal

制作 Exterior Facade、Outdoor Decoration 和入口区域需要的正式 Models。

### Scope

- Exterior wall modules
- Doors、windows、signs 和 awnings
- Outdoor lights、planters 和 benches
- Bicycle、blackboard menu 和 doorway mat
- Garden pieces 和基础 seasonal decoration
- Exterior Materials、Colliders、attachments 和 Prefabs

### Why Before Exterior Gameplay

Exterior attachment、visibility、Collider 和 entrance-path rules 必须用真实尺寸验证，不能只依赖 Cubes。

### Main Difficulty 与 Solution

Facade modules 和 free-placement decorations 使用不同 contracts。分别建立 wall attachment fixtures 和 ground-placement fixtures，并保持 entrance corridor 可读。

### Risks / Likely Bugs

- Facade seams、scale 或 attachment offsets。
- Outdoor Collider 阻挡入口。
- Flat decoration 错误成为 obstacle。
- Exterior assets 在 Camera 角度下被建筑遮挡。

### Tests

- Module alignment 和 attachment points。
- Exterior footprint / rotation。
- Collider / obstacle policy。
- Entrance corridor smoke test。
- Camera visibility。
- Material / texture / reference scan。
- Save-compatible stable definition IDs。

---

## Phase 29 — Exterior Zone & Facade

### Goal

让玩家改变店铺建筑外观，并在实际 Exterior Scene 中看到结果。

### Scope

- Exterior zone data
- Exterior Scene renderer
- 外墙颜色和材质
- 店铺招牌
- 门窗
- 遮阳棚
- 外墙灯饰
- Facade attachment surfaces
- Exterior appearance Save / Load

### Why After Store Expansion

Exterior 是可解锁店铺区域的一部分，需要复用 Phase 21 的 region ownership、unlock 和 expansion rules。

### Main Difficulty 与 Solution

Facade elements 不使用普通地面 Grid placement。墙面、门窗、招牌和遮阳棚使用明确 attachment surfaces 和 stable attachment IDs；Exterior renderer 根据 Layout data 重建实际外观。

### Risks / Likely Bugs

- 更换外墙时门和入口位置发生意外变化。
- Sign 或 awning 附着到不兼容 Surface。
- UI 中选择已保存，但 Scene 仍显示默认外观。
- Load 后 Exterior attachment 丢失。

### Tests

- Interior 与 Exterior zones 保持分离。
- Facade、sign、door、window 和 awning 的选择正确显示。
- 不兼容 attachment 被拒绝。
- Exterior renderer 与 Layout data 一致。
- Save / Load 保留全部外观选择。
- 外观修改不改变 Entrance stable ID 和位置。

### Not Included

自由摆放户外家具、Customer exterior route、户外座位功能、Atmosphere 数值和天气系统。

---

## Phase 30 — Outdoor Decoration Placement

### Goal

让玩家在 Exterior ground 和兼容外部 surfaces 上自由摆放装饰。

### Scope

- Exterior ground Grid
- Exterior-only / Interior-only compatibility
- 花盆、自行车、黑板菜单和长椅
- 门口地毯、户外灯饰和节日装饰
- 小花园装饰
- Move、rotate、store、confirm 和 cancel
- Outdoor placement preview
- Outdoor furniture Save / Load

### Why After Exterior Facade

先建立稳定 Exterior zone 和 attachment surfaces，再复用 Phase 2–3 的 occupancy 与 Decoration Mode rules。

### Main Difficulty 与 Solution

不同装饰使用不同 placement surfaces 和 navigation behavior。Flat decorations 可以不阻挡 Navigation；实体家具根据 validated footprint 和 obstacle rules 参与路径检查。

### Risks / Likely Bugs

- Interior-only furniture 被放到 Exterior。
- Preview 与正式 occupancy 不一致。
- 门口地毯错误生成 obstacle。
- Remove 后 Exterior cells 没有释放。
- Outdoor furniture 在 Load 后位置或 rotation 改变。

### Tests

- Interior / Exterior compatibility。
- Ground、wall 和 doorway attachment rules。
- Rotation、footprint、overlap 和 bounds。
- Cancel 不修改正式 Layout。
- Remove 正确释放 occupancy。
- Flat decoration 不错误阻挡 Navigation。
- Save / Load 保留 Outdoor furniture。
- Scene representation 与 Exterior Layout data 一致。

### Not Included

Customer path readiness、可使用的户外座位、Atmosphere bonus 和外部经营功能。

---

## Phase 31 — Entrance Route & Exterior Validation

### Goal

把 Exterior decoration 与 Customer arrival 和开店 readiness 安全连接。

### Scope

- Exterior Customer Spawn Point
- Street / approach path
- Required entrance corridor
- `Spawn → Entrance` route validation
- Exterior layout change 后的 path refresh
- Invalid layout explanation
- Decoration Mode pause / resume integration
- Business opening readiness integration

### Why After Outdoor Placement

只有实际存在可移动户外家具后，才能正确验证入口阻挡、Collider 和 NavMesh 更新问题。

### Main Difficulty 与 Solution

Layout data 先检查 required corridor，再由 Navigation layer 验证完整 path。退出 Decoration Mode 时 invalidates old paths、重新验证，并只在 readiness 通过后允许营业。

### Risks / Likely Bugs

- 花盆、长椅或 Collider 堵住入口。
- NavMesh 尚未刷新就恢复 Customer movement。
- Customer 继续使用 Layout 改变前的旧 path。
- 错误提示无法指出阻挡区域。
- Decoration Mode 中 movement 没有安全暂停。

### Tests

- `Spawn → Entrance` 存在完整 path。
- 阻挡 required corridor 的 placement 被拒绝或使营业 readiness 失败。
- Error report 指出具体 blocked area / furniture。
- Layout change invalidates old paths。
- Decoration Mode 暂停 Customer movement。
- 退出 Decoration Mode 后重新验证再恢复。
- 多名 Customers 可连续从 Exterior 进入店铺。
- Console 无 NavMesh、Collider 或 missing-reference error。

### Milestone Gate

完成 **Exterior Decoration Milestone**：玩家可以改变实际店铺外观、摆放户外装饰，Customer 能安全经过玩家设计的 Exterior 区域进入店铺。

---

## Phase 32 — Seating & Dine-in Service

### Goal

在保留 Takeout 的同时加入座位、送餐、用餐和清理。

### Scope

- Tables / chairs
- Seat reservation
- Dine-in customer choice
- Table delivery
- Dining duration
- Table cleanup
- Phase 23 已允许摆放的 Outdoor tables / chairs 在本 Phase 获得实际 seat reservation、delivery、dining 和 cleanup 功能。

### Why After Expansion

Dine-in 需要更大空间，也用于验证 Expansion 是否提供真实 gameplay value。

### Risks / Likely Bugs

- Seat 重复预留。
- Takeout 被 Dine-in 完全替代。
- 商品送到错误 Table。
- Cleanup failure 永久占用座位。

### Tests

- Seat reservation ownership。
- Takeout / Dine-in coexistence。
- Correct table delivery。
- Cleanup release。
- Invalid dine-in layout 不阻塞 Takeout。
- Capacity、Navigation 和 Save regression。

---

## Phase 33 — Atmosphere & Themes

### Goal

让装修产生可读但不强迫单一最优布局的经营影响。

### Scope

- Atmosphere values
- Theme tags
- Diminishing returns
- Theme combinations
- Customer preference reaction

### Why After Real Decoration Playtest

需要先观察玩家实际如何摆放家具，才能设计不会鼓励重复堆叠的算法。

### Risks / Likely Bugs

- 同一家具无限叠加最优。
- Theme bonus 重复计算。
- Preview 与实际 Atmosphere 不同。
- Decoration 变成强制数值作业。

### Tests

- Diminishing returns。
- Bonus duplicate prevention。
- Deterministic calculation。
- UI 与 data 一致。
- 多种风格达到相近可行结果的 balance checks。

---

## Phase 34 — Special Rooms & Multi-floor

### Goal

支持后厨、露台、VIP 区和受控多楼层空间。

### Scope

- Preset special regions
- Floor identity
- Controlled entrances
- Stairs / floor transitions
- Floor navigation
- Camera floor switching

### Why Late

多 NavMesh surfaces、跨层 paths、Camera 和 Save complexity 很高，单层系统必须先稳定。

### Risks / Likely Bugs

- NPC 卡在楼层 transition。
- Camera 显示错误 floor。
- 不同楼层 Grid occupancy 混合。
- Save 丢失 floor ID。

### Tests

- Floor-specific occupancy。
- Cross-floor route。
- Unreachable floor readiness failure。
- Camera switching。
- Multi-floor Save / Load。
- Single-floor old Save compatibility。

---

# Milestone F — Signature Content

## Phase 35 — Coffee Bean Visual Asset Set

### Goal

制作 Coffee Bean discovery、collection、packaging 和 drinks 需要的视觉资产。

### Scope

- Bean / bag / package Models
- Quality / rarity visual language
- Collection presentation assets
- Drink presentation variations
- Icons / thumbnails production specifications
- Unity Prefabs 和 Materials

### Why Before Coffee Bean Gameplay

探索奖励必须在固定 Camera 和 UI 中可辨认；先确定 visual vocabulary，gameplay data 才能绑定稳定 asset IDs。

### Risks / Likely Bugs

- 不同 quality 在实际视角下无法区分。
- Asset variants 与 gameplay definitions 数量不一致。
- Icon、Model 和 product ID 错配。

### Tests

- Stable asset-definition mapping。
- Camera / UI readability。
- Variant completeness。
- Missing material / texture scan。
- Prefab scale 和 packaging footprint。
- Performance baseline。

---

## Phase 36 — Coffee Bean Exploration

### Goal

加入员工派遣、Coffee Bean discovery、quality、flavor 和 collection。

### Why First

这是最能体现 AnimalCafe 特色的独立成长系统，并直接使用 Staff、Product、Inventory 和 Progression。

### Risks / Likely Bugs

- 派遣 Staff 仍参与 Cafe work。
- 时间奖励重复领取。
- Random result 超出配置。
- Duplicate discovery 规则不一致。

### Tests

- Dispatch / return state。
- Staff availability。
- Result bounds 与 deterministic seeded tests。
- Duplicate discovery behavior。
- Collection 与 Save / Load。

---

## Phase 37 — Syrup & Add-on Visual Asset Set

### Goal

制作 Add-on devices、flavor indicators、drink variations 和 UI icons。

### Why Before Syrup Gameplay

Add-on 是永久装备而 Syrup 是消耗资源，视觉上必须明确区分，避免玩家误解。

### Risks / Likely Bugs

- Flavor variants 只靠难以辨认的微小颜色差。
- Add-on Model 与 slot ID 错配。
- Drink variation 数量失控。

### Tests

- Add-on / Syrup visual distinction。
- Slot / asset ID mapping。
- Flavor readability。
- Material / texture / reference scan。
- Variant budget check。

---

## Phase 38 — Syrup & Add-on Gameplay

### Goal

加入共用 Syrup inventory、Add-on slots 和 flavored drinks。

### Why Separate From Coffee Beans

两者都会修改 Recipe 和 Menu；分开加入可以准确定位 product-generation bugs。

### Risks / Likely Bugs

- Add-on 被错误消耗。
- 每种 flavor 错误建立独立 Syrup inventory。
- Slot replacement 丢失永久 unlock。
- Fusion recipe 在普通条件下可用。

### Tests

- Shared Syrup consumption。
- Add-on permanence。
- Slot equip / replace。
- Single-flavor normal rule。
- Fusion conditions。
- Save / Load。

---

## Phase 39 — Bakery Visual Asset Set

### Goal

制作 Bakery equipment、trays、display assets 和第一批 products。

### Why Before Bakery Gameplay

Bakery 会同时出现 production 与 display states，Model set 必须先证明两种状态清楚可读。

### Risks / Likely Bugs

- Raw / preparing / ready products 难以区分。
- Tray、display 和 product scale 不一致。
- Bakery equipment anchors 不可达。

### Tests

- State readability。
- Equipment anchor validation。
- Product / tray / display scale。
- Asset-definition mapping。
- Material / reference scan。
- Scene performance fixture。

---

## Phase 40 — Bakery Gameplay

### Goal

加入少量 Bakery recipes、equipment 和与 Coffee 共用的 ingredients。

### Why Separate

Bakery 会改变 production time、equipment queue、shared Milk 和 throughput，需要独立 balance 与 regression。

### Risks / Likely Bugs

- Shared Milk 重复预留。
- Bakery task 占用错误 equipment。
- Display inventory 与 production inventory 不一致。

### Tests

- Recipe / equipment validation。
- Shared ingredient reservations。
- Equipment queue。
- Production / display inventory。
- Out-of-stock recovery。
- Economy 和 Save integration。

---

## Phase 41 — Merchandise Visual Asset Set

### Goal

制作 Merchandise products、packages、displays 和 UI presentation assets。

### Why Before Merchandise Gameplay

Merchandise 不进入制作流程，视觉和 Prefab contract 必须与 food products 分离。

### Risks / Likely Bugs

- Merchandise 被错误配置为 preparation item。
- Display footprint 与 shelf capacity 不匹配。
- 小物件在 Camera 下不可辨认。

### Tests

- Merchandise category validation。
- Display / shelf compatibility。
- Camera readability。
- Asset-definition mapping。
- Material / reference scan。
- Performance budget。

---

## Phase 42 — Merchandise Gameplay

### Goal

加入不需要制作的进货商品和 Customer purchase behavior。

### Why Last in Signature Content

Merchandise 依赖成熟的 Customer budget、interest、Relationship 和 Economy，但不应污染 food-production pipeline。

### Risks / Likely Bugs

- Purchase 扣库存两次。
- Customer budget 变为负数。
- Merchandise 错误进入 Barista task queue。

### Tests

- Restocking。
- Purchase transaction。
- Budget bounds。
- Interest / relationship modifiers。
- Merchandise 不创建 preparation task。
- Save / Load。

---

# Milestone G — Offline 与 Release

## Phase 43 — Offline Progression

### Goal

在玩家返回时使用摘要计算推进普通经营，并显示 Offline Report。

### Scope

- Leave / return timestamps
- Offline cap
- Summary simulation
- Inventory / Economy changes
- Pending major events
- Automatic closing boundary
- Offline Report

### Why Near the End

Offline Mode 必须总结前面几乎所有稳定 gameplay rules。过早实现会随着每个系统不断重写。

### Main Difficulty 与 Solution

不在后台运行完整 Scene。使用纯数据 simulation，并复用 Active Mode 的 business rules 与 transaction services。

### Risks / Likely Bugs

- 修改设备时间获得异常收益。
- Offline 绕过 Inventory 或 Capacity。
- 自动打烊后继续无限结算。
- Major Event 阻塞全部收益。

### Tests

- Same input → deterministic result。
- Offline time cap。
- Clock rollback / large jump protection。
- Inventory / Economy invariants。
- Automatic closing boundary。
- Pending major events。
- Offline Save recovery。

### Milestone Gate

完成 **Feature-complete Alpha**：所有主要 gameplay systems 已存在，后续不再加入大型核心系统。

---

## Phase 44 — Online Identity & Cloud Save — Optional

### Goal

在产品需求确认后，为 Guest/local profile 提供可选平台账号绑定和 Cloud Save。

### Activation Gate

本 Phase 默认不实施。只有用户明确批准 Cloud Save、跨设备恢复、排行榜、好友或跨平台账号需求后，才设计具体方案。

### Recommended Default Scope

- Guest / local profile
- Optional platform-account binding
- Cloud Save upload / download
- Save conflict detection
- User-controlled conflict resolution
- Offline fallback
- Sign-out / unlink safety
- Privacy and data-deletion entry points

### Why After Stable Save and Offline

Cloud synchronization 必须建立在稳定 Save schema、versioning、migration 和 offline conflict rules 上。

### Main Difficulty 与 Solution

不要默认自建 password authentication。优先使用目标平台 identity，并让 local Save 在没有网络或账号时仍可使用。

### Risks / Likely Bugs

- Cloud 覆盖较新的 local Save。
- 同一账号在多设备产生冲突。
- Sign-out 导致本地进度丢失。
- Network retry 重复上传或下载。
- Privacy / deletion flow 不完整。

### Tests

- Guest-only gameplay。
- Link / unlink / sign-out。
- Newer-local、newer-cloud 和 equal-version conflicts。
- Offline launch 和 reconnect。
- Retry idempotency。
- Corrupted-cloud fallback。
- Account deletion / local-data policy checks。
- Platform sandbox integration tests。

---

## Phase 45 — UI/UX Integration & Accessibility

### Goal

统一所有 feature UI、navigation、onboarding 和 accessibility，形成完整可用体验。

### Scope

- Final information hierarchy
- Cross-feature navigation
- Tutorial / onboarding
- Formal icons、copy 和 transitions
- Error-message consistency
- Accessibility settings
- Localization-ready layouts
- Keyboard / controller considerations
- UI sounds
- User-testing fixes

### Why After Feature Complete

所有真实页面和 workflows 已存在，才能统一 navigation 和信息层级；更早进行 full polish 会因功能变化反复返工。

### Risks / Likely Bugs

- Tutorial 与最终 rules 不一致。
- Modal / back navigation 形成死路。
- Long text 和 localization overflow。
- Accessibility settings 只改变视觉、不改变交互。

### Tests

- Complete navigation map。
- New-player first-day usability。
- Back / cancel / modal flows。
- Long-text / localization fixtures。
- Color / text / input accessibility。
- Error recovery journeys。
- Multiple resolutions。
- UI regression automation 与 manual checklist。

---

## Phase 46 — VFX Production & Integration

### Goal

在已有 functional feedback 上加入正式、可扩展并符合性能预算的 VFX。

### Scope

- Coffee steam 和 preparation feedback
- Order / payment / coin effects
- Trait discovery 和 unlock celebration
- Ambient 和 seasonal particles
- Screen / panel transitions
- VFX quality settings
- Mobile-safe variants

### Why After Stable Gameplay

Functional phases 已证明何时需要 feedback；正式 VFX 可以绑定稳定 events，不必随着 state rules 重写。

### Risks / Likely Bugs

- VFX 重复触发。
- Pause 后 particles / timing 错误。
- Effects 遮挡 characters 或 UI。
- Overdraw、particle count 和 mobile cost 超出预算。

### Tests

- Event-to-effect one-shot mapping。
- Pause / resume behavior。
- Camera / UI occlusion。
- Missing VFX reference fallback。
- Particle / overdraw performance fixtures。
- Quality-level switching。
- Mobile-safe material compatibility。

---

## Phase 47 — Final Model Replacement & Asset Optimization

### Goal

清除剩余 primitives 和临时视觉资产，并统一、优化全部正式 Models。

### Scope

- Placeholder inventory 和 replacement
- Remaining production Models
- LOD、texture compression 和 material consolidation
- Collider cleanup
- Definition / Prefab reference validation
- Windows / mobile memory budgets
- Final art consistency pass

### Why Before Release Preparation

核心 Models 已分批完成，本 Phase 只处理遗漏和整体优化，不承担第一次制作所有资产的高风险工作。

### Risks / Likely Bugs

- Replacement 改变 footprint、pivot 或 anchors。
- Definition IDs 改变导致旧 Save 丢失家具。
- Material consolidation 破坏外观。
- Optimization 导致 Collider 或 LOD popping。

### Tests

- Zero unintended primitives / placeholders。
- Stable Definition IDs。
- Footprint、pivot、anchor 和 Collider regression。
- Missing reference scan。
- Save migration fixtures。
- LOD / material visual QA。
- Memory、load time 和 frame-time budgets。

---

## Phase 48 — Windows Release Preparation

### Goal

把 Feature-complete Alpha 整理为稳定、易理解和性能合格的 Windows Release Candidate。

### Scope

- Tutorial / onboarding
- UI / UX polish
- Audio / animation / feedback
- Content pass
- Economy / progression balance
- Accessibility basics
- Performance profiling
- Save migrations
- Windows builds
- Bug triage

### Why After Feature Complete

避免为之后会重写的 systems 过早制作正式 UI、animation 或大量 content。

### Risks / Likely Bugs

- Late polish 引入 regression。
- Tutorial 与真实 rules 不一致。
- Long-session memory / state leaks。
- Upgrade installation 破坏 Save。

### Tests

- Full automated regression。
- New-player first-day usability test。
- Long-session soak test。
- Fresh install / upgrade install。
- Save migration / corruption recovery。
- Multiple resolutions / aspect ratios。
- Performance profile。
- Release build smoke test。

### Milestone Gate

产生通过 release checklist 的 Windows Release Candidate。

---

## Phase 49 — Mobile UI Adaptation

### Goal

将已完成的 UI system 适配为可用、可读的 mobile presentation。

### Scope

- Touch target sizes
- Safe Area
- Mobile aspect ratios
- Small-screen panel variants
- Gesture / UI conflict handling
- Mobile text scale
- Virtual keyboard flows
- Background / foreground UI state

### Why Before iOS Platform Adaptation

先验证 UI 和 interaction presentation，再处理 build、device lifecycle 和 platform integration，避免同时调试 layout 与 native issues。

### Risks / Likely Bugs

- Touch gesture 与 ScrollView / Camera 冲突。
- Notch / home indicator 遮挡 controls。
- Small screen modal 无法关闭。
- Background 后恢复错误 panel state。

### Tests

- Target device resolutions 和 Safe Areas。
- Minimum touch targets。
- Gesture priority。
- Long text / keyboard。
- Background / foreground UI restoration。
- No-hover complete workflows。
- Mobile UI manual usability test。

---

## Phase 50 — iOS Adaptation

### Goal

在不分叉核心 gameplay rules 的情况下适配 iOS。

### Scope

- Touch pan / pinch zoom / tap
- Mobile-safe UI
- Safe Area
- Background / foreground lifecycle
- Mobile performance / memory / battery
- iOS Save path
- Device builds

### Why Last

Windows 版本先验证 gameplay 和 Save model；iOS 是 platform adaptation，不建立第二套游戏 architecture。

### Risks / Likely Bugs

- UI 依赖 hover 或精细 mouse input。
- Background 时丢失 state。
- Touch 与 UI event 冲突。
- Mobile memory 和 battery 超出目标。

### Tests

- 所有核心操作可通过 touch 完成。
- No-hover usability。
- UI Safe Area 与常见 aspect ratios。
- Background / foreground persistence。
- Touch / UI conflict tests。
- Device performance、memory 和 battery。
- Windows 与 iOS 使用相同 gameplay data rules。

### Milestone Gate

产生通过 device checklist 的 iOS Release Candidate。

---

## 6. Major Milestone Review Gates

| Milestone | 完成 Phase | Review 重点 |
|---|---:|---|
| Foundation Prototype | 0 | 基础 interaction 与 time 是否稳定 |
| Visual Foundation | 5 | Asset pipeline、第一批正式 Models 与 UI foundation 是否可重复扩展 |
| Layout Foundation | 7 | Layout contracts 是否足够支持经营，但未 over-engineer |
| Core Cafe Loop Prototype | 13 | 自动外带服务是否稳定、可读、无卡死 |
| Playable Management MVP | 19 | 多日经营、Application Shell、Menu、Inventory 与 Save 是否形成闭环 |
| Character-driven Vertical Slice | 24 | 正式角色 pipeline 与角色差异是否真实影响体验 |
| Exterior Decoration | 31 | Exterior assets、outdoor placement 与 Customer entrance 是否安全连接 |
| Cafe Growth | 34 | Expansion、Dine-in 与 Advanced Decoration 是否互相支持 |
| Signature Content | 42 | 特色视觉资产与 gameplay 是否增加选择而非无意义复杂度 |
| Feature-complete Alpha | 43 | 所有核心 rules 是否支持 Offline summary |
| Optional Online Services | 44 | 如启用，Cloud conflicts、offline fallback 和账号安全是否可靠 |
| Release Visual & UX Integration | 47 | UI、VFX、Models 和 performance 是否达到 release preparation 标准 |
| Windows Release Candidate | 48 | Stability、usability、balance 和 performance |
| Mobile UI Ready | 49 | Touch layout、Safe Area 和 mobile workflows 是否完整 |
| iOS Release Candidate | 50 | Platform lifecycle、device performance 和 build checklist |

每个 Milestone 完成后：

1. 进行完整 regression 和 manual playtest。
2. 记录玩家可见的优点、摩擦和新 risks。
3. Re-review 后续 Phase 顺序和 scope。
4. 必要时更新 Game Design 与 Roadmap。
5. 未经 approval 不自动开始下一 Milestone。

---

## 7. Current Next Step

**Phase 1 — Layout Data Model** 已完成 implementation、automated verification、manual acceptance、merge 和 merged-main regression，状态为 `Completed`。

**Phase 2 — Grid Occupancy & Placement Rules** 已完成 approved design、implementation、automated verification、review、manual acceptance、merge 和 merged-main regression，状态为 `Completed`。

**Phase 3 — Visual Style & Asset Pipeline Foundation** 状态为 `In Review`。

- 已完成 fresh automated evidence：EditMode `285 / 285`、PlayMode `48 / 48` passed，failed、skipped、inconclusive 均为 `0`；production validator 为 `3 / 3` valid、`0 issues`。
- Studio Owner Camera/readability manual acceptance 以及 source license/use-right confirmation 仍为 `Pending`。
- 当前只是 benchmark pipeline 和 readability baseline；未开始 Phase 4 formal asset set、gameplay、placement 或 runtime integration。
- 不执行旧版 Phase 1 Core Cafe Loop plan。
- 不开始 Decoration UI 或 Customer AI。
