# AnimalCafe Development Roadmap

> 状态：Draft for Review
>
> 类型：Milestone-based Development Roadmap
>
> 当前开发环境：Windows / Unity 6
>
> 目标兼容平台：iOS

## 1. 文档用途

本文档将 `AnimalCafe_Project_Design.md` 中的长期 Game Design 拆分为可逐步开发和验证的 development phases。

- `AnimalCafe_Project_Design.md` 仍是游戏规则和长期设计的 Source of Truth。
- 本文档只定义 development parts、实现顺序、scope、dependencies 和完成 gate。
- Roadmap 使用 milestone 和可验证结果，不提供周数或 deadline。
- 每个 Phase 完成后，游戏都应保持可运行、可测试。
- 每次只为当前 Phase 编写详细 implementation plan，不一次实现整份 roadmap。
- Design document 中的 Open Design Questions 不能自动视为已确认需求；进入相关 Phase 前必须先解决会影响实现的问题。

---

## 2. Development Strategy

AnimalCafe 采用 **Playable Loop 逐层扩展** 的开发方式。

第一目标不是一次实现所有长期系统，而是尽快得到一个能够自动运转、容易观察和容易测试的小型咖啡厅。后续 Phase 在这个稳定循环上逐层增加 economy、inventory、characters、progression、signature content 和 offline progression。

### 2.1 核心原则

1. **先完成闭环，再增加深度**
   - 优先完成“客人进入 → 点单 → 制作 → 取餐 → 离开”的完整流程。
   - 不先单独开发大量无法形成游戏体验的底层系统。

2. **自动经营是默认体验**
   - 员工和订单应在没有持续手动操作时正常运行。
   - 玩家操作主要用于观察、调整和处理重要事件。

3. **每个 Phase 都必须可验收**
   - 每个 Phase 都有明确的 Play Mode、Console、data 和 persistence 检查。
   - 未达到完成 gate 时，不进入下一个大型系统。

4. **长期兼容 iOS，近期先完成 Windows**
   - 早期 input 和 UI 不依赖只能由鼠标完成的精细操作。
   - 实际 touch、mobile layout 和 iOS performance adaptation 放在 Windows 版本稳定后。

5. **保持 scope 克制**
   - 新系统先实现最小可用规则，再根据实际 playtest 扩展。
   - 暂不需要的系统不得提前进入当前 Phase。

---

## 3. Roadmap Overview

| Phase | Development Part | 主要可验证结果 |
|---|---|---|
| 0 | Project Foundation | Camera、input、interaction、time control 和测试基础可运行 |
| 1 | Core Cafe Loop MVP | 客人、订单、员工和 Pick-up 形成自动服务循环 |
| 2 | Day Cycle & Economy | 从开店到日报再到下一天形成完整经营闭环 |
| 3 | Inventory & Menu | 商品可用性、材料预留、消耗和补货正确运行 |
| 4 | Staff & Character Foundation | 多员工、工作分配、Character ID 和基础 traits 可运行 |
| 5 | Customer Identity & Events | 回头客、关系、心情、trait 发现和简单事件产生角色差异 |
| 6 | Progression & Cafe Growth | 玩家可通过解锁、设备和店面成长形成经营方向 |
| 7 | Signature Content Systems | Coffee Bean、Syrup、Bakery 和 Merchandise 增加游戏特色 |
| 8 | Offline & Persistence | Offline progression、报告和可靠 autosave 正确运行 |
| 9 | Release Preparation | Windows 版本达到内容、体验、性能和稳定性目标 |
| 10 | iOS Adaptation | Touch、mobile UI、performance 和 iOS build 达到发布要求 |

### 3.1 Major Milestones

- **Foundation Prototype:** Phase 0 完成
- **Core Loop Prototype:** Phase 1 完成
- **Playable Management MVP:** Phase 3 完成
- **Character-driven Vertical Slice:** Phase 5 完成
- **Feature-complete Alpha:** Phase 8 完成
- **Windows Release Candidate:** Phase 9 完成
- **iOS Release Candidate:** Phase 10 完成

---

## 4. Phase Details

## Phase 0 — Project Foundation

### Goal

建立简单、可理解、可测试的 Unity project foundation，让后续系统共享一致的 Camera、input、interaction 和 game time。

### Included

- 整理简单的 `Assets` folder structure。
- 在 `MainCafe` scene 建立固定斜俯视 Camera。
- 实现 Camera pan、zoom 和 scene bounds。
- Windows 使用 mouse input，同时保留未来 touch input 的扩展边界。
- 点击场景测试对象并显示基础 visual feedback。
- 建立 Pause、`1x` 和 `2x` Game Time。
- 建立基础 Play Mode tests 和 Console error 检查方式。
- 明确 scene object、runtime system 和 data definition 的基本职责边界。

### Not Included

- Customer AI
- Orders
- Economy
- Inventory
- Save system
- 正式 UI art

### Completion Gate

- 玩家可以在 `MainCafe` scene 平移和缩放 Camera。
- Camera 不会移出允许边界。
- 玩家可以点击测试对象并看到反馈。
- Pause、`1x` 和 `2x` 能正确改变受 Game Time 控制的测试行为。
- 基础 Play Mode tests 通过。
- Console 没有未处理的 error。

---

## Phase 1 — Core Cafe Loop MVP

### Goal

证明咖啡厅能够在没有持续手动操作的情况下完成最小自动服务循环。

### Included

```text
生成客人
→ 前往柜台
→ 从固定菜单选择商品并付款
→ 创建订单
→ 空闲员工自动领取 FIFO 订单
→ 前往设备并制作
→ 商品送到 Pick-up
→ 客人取餐并离开
```

- 一个小型 cafe layout。
- 一名 employee。
- 一种 customer presentation。
- 一台制作设备。
- 一种不依赖完整 inventory 的基础咖啡。
- 简单 placeholder UI。
- 清晰的 order states 和 character states。
- 基础 movement failure detection 和安全 fallback。

### Not Included

- Inventory 和 restocking
- Character traits 和 relationships
- 多员工工作分配
- Seats 和 dine-in service
- Story events
- Offline progression

### Completion Gate

- 多名客人可以连续完成整个服务流程。
- Orders 默认按照 FIFO 处理。
- 同一个 order 不会被领取或完成两次。
- Employee 和 customer 不会因正常流程永久卡住。
- Queue 或 Pick-up 达到容量时，不继续生成无法容纳的 customer。
- Console 没有未处理的 error。

---

## Phase 2 — Day Cycle & Economy

### Goal

把自动服务流程放入可重复的营业日，使玩家能完成一天、查看结果并开始下一天。

### Included

- 开店、正常营业、提前打烊和自动打烊。
- 基础 coins、product price 和 order revenue。
- 当日 order count、revenue 和 failed order statistics。
- 简洁的 Daily Report。
- 打烊后的 management state。
- 玩家确认后开始下一天。
- 最低收益保障使用简单、透明的基础供应规则。

### Not Included

- 完整 upgrade tree
- 员工工资和复杂 expenses
- Bankruptcy 或 Game Over
- 完整 economy balancing
- Offline day simulation

### Completion Gate

- 玩家可以从 Day 1 开始营业并正常打烊。
- 自动打烊与提前打烊都不会遗留错误的 active orders。
- Daily Report 的数字与当天实际 orders 和 revenue 一致。
- 玩家可以从报告进入 Day 2。
- 经营表现不佳不会使游戏永久无法继续。

---

## Phase 3 — Inventory & Menu

### Goal

让 product availability、orders 和 inventory 使用同一套可靠规则，形成第一版稳定经营 MVP。

### Included

- 克制的基础 ingredients：
  - Milk
  - Coffee Beans
  - Syrup
  - 当基础 Bakery 进入测试时才加入 Flour 和 Egg
- Recipe data definitions。
- Product 所需 ingredients 和 equipment 检查。
- Customer 付款时预留 ingredients。
- Employee 开始制作时正式消耗预留 ingredients。
- 简单 restocking。
- Menu product enable / disable。
- 基础 inventory 和 menu UI。
- Save system 的最小版本，只保存完成本 Phase 所需的核心经营数据。

### Not Included

- Coffee Bean quality 和 exploration
- Syrup Add-on slots
- 复杂 Bakery production
- Merchandise
- Premium pricing strategy

### Completion Gate

- 缺少材料或设备的 product 不能被点单。
- 多个 orders 不会重复使用同一份 available inventory。
- Order state 改变时，reserved 和 consumed inventory 保持一致。
- Restock 后 product 可以重新进入 available menu。
- 退出并重新进入游戏后，coins、day、inventory 和基础 menu state 正确恢复。
- 第一版 Playable Management MVP 可以连续运行多个营业日。

---

## Phase 4 — Staff & Character Foundation

### Goal

在不破坏自动经营的前提下加入多员工和最小 character identity，为后续角色故事建立稳定数据基础。

### Included

- 稳定且唯一的 `Character ID`。
- Species、Breed、display name 和 appearance reference。
- 少量容易观察的 Individual Traits。
- 多名 employees。
- 自动领取可执行任务。
- 基础 work capability 和 equipment compatibility。
- Employee status、current task 和简单详情 UI。
- Trait 对工作速度、耐心或互动概率产生克制影响。
- Save employee identity、traits 和必要 runtime state。

### Not Included

- 完整 trait list
- 深度 relationship simulation
- Dynamic career 和 life stages
- Employee schedules 和 salary balancing
- 复杂 manual order assignment

### Completion Gate

- 多员工不会同时领取同一个 task。
- Employee 能根据 capability 领取可完成的工作。
- Trait 差异可以通过可见行为或数据被观察。
- Character identity 在 Save / Load 后保持不变。
- 增加员工后，原有 cafe loop、inventory 和 day cycle tests 仍然通过。

---

## Phase 5 — Customer Identity & Events

### Goal

让 customers 从一次性单位变成可被玩家认识的角色，完成 Character-driven Vertical Slice。

### Included

- Random customers 和 returning customers。
- Customer `Character ID`、preferences 和 visit history。
- 基础 mood 和 cafe relationship。
- 少量 relationship changes。
- Obvious traits 与 hidden traits。
- Hidden trait 通过重复行为或事件逐步发现。
- Employee–customer 和 customer–customer 的简单 interactions。
- 少量正面与负面 events。
- 重要 event 提供简短 player choice。
- Customer profile 和 event feedback UI。

### Not Included

- 完整人生模拟
- Marriage、retirement 等长期 life events
- 大量手写 storylines
- 完整 procedural narrative system
- Offline event resolution

### Completion Gate

- Returning customer 的 identity、preferences 和 relationship 能跨天保留。
- 同类 characters 可因 traits、mood 和 relationship 表现不同。
- Event choice 会产生明确、可保存和可解释的结果。
- Hidden trait 只在满足 discovery condition 后显示。
- 角色系统不会阻塞核心经营循环。

---

## Phase 6 — Progression & Cafe Growth

### Goal

让玩家通过经营结果解锁选择、扩大咖啡厅，并形成不同的发展方向。

### Required Design Decisions Before Planning

- Coins、income、expenses 和 upgrade cost 的基础范围。
- Equipment unlock 和 cafe expansion 规则。
- Strategy points 来源。
- Affordable 与 Premium tracks 的第一批效果。
- Strategy 是否允许 reset，以及 reset cost。

### Included

- 基础 progression currency 或 unlock condition。
- Equipment unlock 和 purchase。
- Menu capacity 或 product unlock。
- Cafe usable area 或 station capacity growth。
- Strategy Track 的最小 Affordable 与 Premium branches。
- Mixed build 保持可行，不建立永久互斥路线。
- Progression overview UI。

### Not Included

- 最终数量的 upgrades
- 大型 decoration catalogue
- 完整 building editor
- 所有最终 balance values

### Completion Gate

- 玩家通过正常经营获得至少一种明确的成长选择。
- Upgrade 会产生可观察的 gameplay change。
- 不同 Strategy choices 会影响 customers、menu 或 operation，但不会禁止其他玩法。
- 旧 Save 能通过明确 migration 或 version handling 继续读取。
- 没有 upgrade 会破坏最低收益保障。

---

## Phase 7 — Signature Content Systems

### Goal

在基础经营、角色和 progression 已稳定后，加入能区分 AnimalCafe 的特色内容。

### Development Order Inside This Phase

1. Coffee Bean discovery and collection
2. Coffee Bean product effects
3. Syrup inventory and Add-on slots
4. Basic Bakery extension
5. Merchandise extension

每个子系统应拥有独立 implementation plan 和 completion gate，不同时全面展开。

### Included

#### Coffee Bean

- 派遣 employee 进行 exploration。
- Employee 暂时不能参与 cafe work。
- Discover 具有 quality、rarity 和少量 flavor traits 的 Coffee Beans。
- Coffee Bean collection。
- Beans 可用于 drinks 或 packaged sale。

#### Syrup Add-on

- 共用 Syrup inventory。
- Unlockable Add-on slots。
- 装备 flavor Add-on。
- 普通 flavored drink 一次只使用一个 Add-on。
- 特殊条件下允许 fusion recipe。

#### Bakery

- 少量 Bakery recipes。
- 与 Coffee 共用 Milk，并使用必要的 Flour 和 Egg。
- 简单、可读的 production 和 inventory rules。

#### Merchandise

- 通过 restocking 获得。
- 不要求 employee 制作。
- Customer interest、budget 和 relationship 影响购买意愿。

### Not Included

- 复杂现实咖啡知识模拟
- 大量随机属性
- 每种 flavor 的独立 Syrup inventory
- 未经过 playtest 的大型 content catalogue

### Completion Gate

- 每个 signature subsystem 都能独立关闭，不破坏核心经营循环。
- Coffee Bean exploration 的人员取舍清晰可见。
- Bean quality 和 flavor 对 product 有简单、可解释的影响。
- Add-on 不会被错误当作 consumable inventory。
- Bakery 和 Merchandise 使用与基础 order、inventory、economy 一致的规则。

---

## Phase 8 — Offline & Persistence

### Goal

让 Active Mode 和 Offline Mode 使用同一套经营状态，并保证 Save 可靠、可恢复。

### Required Design Decisions Before Planning

- Offline progression time limit。
- Offline simulation granularity。
- 离线期间允许变化的 resources 和 character states。
- Major events 的 pending 规则。
- Autosave frequency、backup 和 recovery policy。

### Included

- 记录玩家离开和返回时间。
- 使用摘要计算而非后台实时运行。
- 普通 orders 和 small events 的 offline resolution。
- Major events 暂存，等待玩家返回后处理。
- 简短 Offline Report。
- 关键经营状态变化后的 autosave。
- Background、normal exit 和 day transition save。
- Save versioning、backup 和 recovery。
- Save failure 不覆盖最近的有效 Save。

### Not Included

- 无限 offline rewards
- 在后台持续运行完整 scene simulation
- 未经玩家确认自动解决所有重大故事事件

### Completion Gate

- 相同输入数据可以得到可重复检查的 offline result。
- 离线期间的 income 和 resource changes 不违反 Active Mode rules。
- Major event 不会阻塞其他 offline operation。
- Corrupted newest Save 可以从最近有效 backup 恢复。
- Save / Load 和 offline tests 覆盖主要经营与角色数据。
- 完成后达到 Feature-complete Alpha。

---

## Phase 9 — Release Preparation

### Goal

把 feature-complete Alpha 整理为稳定、易理解、性能合格的 Windows Release Candidate。

### Included

- 完整 onboarding 和基础 tutorial。
- UI hierarchy、feedback 和 readability。
- Audio、animation 和 visual feedback polish。
- Content pass：products、traits、events、upgrades 和 characters。
- Economy 和 progression balancing。
- Accessibility basics。
- Save migration 和 recovery testing。
- Performance profiling 和 optimization。
- Resolution 和 aspect-ratio checks。
- Windows build、fresh install 和 long-session testing。
- Bug triage：blocker、major、minor。

### Not Included

- iOS-specific UI
- Touch-only interaction
- 大型新 gameplay system
- Release 前临时扩大核心 scope

### Completion Gate

- 新玩家可以在没有开发者指导的情况下完成第一天。
- 关键 management information 可读且 feedback 明确。
- 所有 blocker 和 major bugs 已解决或有明确 release decision。
- 目标 Windows hardware 上 performance 稳定。
- Fresh install、upgrade install、Save / Load 和 recovery tests 通过。
- 连续多日经营不会发生 progression blocker。
- 产生 Windows Release Candidate build。

---

## Phase 10 — iOS Adaptation

### Goal

在不改变核心 Game Design 的前提下，将稳定的 Windows 版本适配到 iOS。

### Included

- Touch pan、pinch zoom 和 tap interaction。
- Mobile-safe UI sizing 和 spacing。
- Safe Area 和常见 aspect ratios。
- Touch 与 mouse input 共用 gameplay commands。
- Mobile performance、memory 和 battery optimization。
- App background / foreground lifecycle handling。
- iOS Save path 和 data persistence validation。
- Device testing。
- iOS build 和 release checklist。

### Not Included

- 为 iOS 重写一套独立 gameplay architecture
- 只在 mobile 存在的核心规则
- 在适配阶段加入大型新 content system

### Completion Gate

- 所有核心操作都能通过 touch 完成。
- UI 不依赖 hover 或精细 mouse positioning。
- App background / foreground 不会丢失经营状态。
- 目标 iOS devices 上 performance 和 memory 达到可接受标准。
- Windows 与 iOS 使用相同的核心 gameplay rules 和 Save model。
- 产生 iOS Release Candidate build。

---

## 5. System Dependencies

后续系统依赖前面建立的稳定规则：

```text
Project Foundation
└─ Core Cafe Loop
   ├─ Day Cycle & Economy
   │  ├─ Inventory & Menu
   │  │  ├─ Progression & Cafe Growth
   │  │  └─ Signature Content Systems
   │  └─ Offline Progression
   └─ Staff & Character Foundation
      ├─ Customer Identity & Events
      ├─ Signature Content Systems
      └─ Offline Character Events

All Stable Systems
└─ Windows Release Preparation
   └─ iOS Adaptation
```

### 5.1 Dependency Rules

- Character traits 可以影响已存在的工作规则，但不能代替 order system。
- Signature content 必须复用已有 inventory、menu、economy 和 progression rules。
- Offline Mode 必须摘要计算 Active Mode 的规则，不能维护第二套互相矛盾的 economy。
- UI 负责显示和发出 commands，不保存唯一的 gameplay truth。
- Save system 保存可恢复的 game state，不直接依赖 scene object instance。

---

## 6. Implementation Plan Rules

每个 Phase 开始前，单独创建 implementation plan。Plan 必须：

1. 只覆盖当前 Phase 或一个明确的 Phase 子系统。
2. 列出将创建和修改的 files，以及每个 file 的单一用途。
3. 先解决会影响实现的 Open Design Questions。
4. 定义 data model、runtime responsibilities 和 public interfaces。
5. 把工作拆成可独立验证的小 tasks。
6. 每个 task 优先采用：

```text
写 failing test
→ 确认 test 正确失败
→ 实现最小功能
→ 确认 test 通过
→ 在 Play Mode 验证玩家可见行为
```

7. 为该 Phase 定义：
   - Play Mode acceptance
   - Console acceptance
   - Data consistency acceptance
   - Save / Load acceptance（如果该 Phase 修改持久数据）
8. 明确本次包含和不包含的内容。
9. 完成后将新确认的长期规则同步回 Game Design document。

---

## 7. Current Next Step

Roadmap 批准后，只为 **Phase 0 — Project Foundation** 创建第一份详细 implementation plan。

Phase 0 plan 预计覆盖：

- 推荐的 `Assets` folder structure
- Camera controller
- Input boundary
- Click interaction prototype
- Game Time controller
- Test setup
- `MainCafe` scene integration
- 每一步的 beginner-friendly Play Mode 验证方式

在 Phase 0 完成并通过 completion gate 前，不开始 Customer AI、Order System 或 Economy implementation。

