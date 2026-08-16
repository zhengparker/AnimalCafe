# AnimalCafe Phase 6 — Basic Decoration Mode Design

> Status: Approved by Studio Owner
> Date: 2026-08-16
> Lifecycle stage: Approved design spec → test-case design
> Lead: Game Design
> Required reviews: Production、Engineering、QA & Player Research
> Implementation status: Not started

## 1. Goal

Phase 6 让玩家在手机优先的 Decoration Mode 中安全地放置、选择、拖动、旋转、确认、取消和收起普通 Floor Furniture。

本 Phase 必须证明：

- Preview 与正式 `CafeLayout` 分离；
- multi-cell `Footprint` 的每一格都参与 placement validation；
- `Confirm` 才提交正式 Layout；
- `Cancel` 可以无损恢复；
- Layout data 与 Scene representation 始终一致；
- Touch、Camera、UI 与 Scene input 不互相穿透；
- Phase 2、Phase 4 和 Phase 5 foundations 可以在正式 `MainCafe` 中共同工作。

## 2. Player-visible result

玩家从正常 HUD 进入 Decoration Mode 后，游戏自动 Pause，并显示 Furniture Catalogue Bottom Sheet。Catalogue 以家具缩略图格子展示当前可用的 Counter presets。

玩家点击一个 Catalogue tile 后，对应家具出现在 Camera 画面中央附近的最近 Grid cell，立即进入悬空 Preview。玩家可以单指拖动、旋转并查看完整 footprint。绿色表示可以放置；红色表示不能放置，并显示具体原因。玩家必须单件 `Confirm` 或 `Cancel`。

玩家也可以点击已摆放家具，使其立即悬空并进入同一套编辑流程。`Store` 需要二次确认。

## 3. Dependencies and reused foundations

### 3.1 Phase 0

- `Game Time` 与 Pause ownership；
- 固定 3/4 isometric-like Camera；
- Camera pan / zoom foundations；
- Unity Editor development input mapping。

### 3.2 Phase 1–2

- `CafeLayout` 是正式 Layout data Source of Truth；
- `GridSize`、rotation 与 footprint rules；
- occupancy、bounds、locked / blocked cells 与 placement result；
- Phase 6 不复制或重写 placement legality。

### 3.3 Phase 4

正式 `MainCafe` 继续复用：

- `PF_Environment_Floor_8x8.prefab`；
- `PF_Environment_Wall_BackLeft_8x3.prefab`；
- `PF_Environment_Wall_BackRight_8x3.prefab`；
- `PF_Environment_Entrance_2x2.prefab`；
- `PF_Environment_Window_01.prefab`；
- Counter Module model、Prefab、Definition 与 asset contracts；
- `FurnitureDefinitionAsset`、Prefab reference、`Footprint`、placement surface 与 stable Definition ID。

Phase 6 不重做 Floor、Wall、Entrance、Window 或 Counter Module source model。

### 3.4 Phase 5

Phase 6 必须复用：

- canonical `UI Root`；
- Bottom Sheet；
- Modal；
- Button roles；
- `AnimalCafeUiTheme`；
- Toast / Validation Message；
- Safe Area；
- UI / Scene pointer boundary；
- Pause coordination；
- navigation、dismissal 与 input blocking rules。

Phase 6 不建立第二套 runtime UI system。

## 4. Platform and presentation baseline

- 正式 interaction 以 mobile `Touch Input` 为准；
- `Portrait 1080 × 1920` 是主要 design 与 manual acceptance reference；
- small / tall Portrait 必须无裁切；
- Landscape 必须功能可用且无裁切，但最终专属布局优化属于 Phase 50；
- Mouse 只作为 Unity Editor 中模拟 Touch 和 automated tests 的 mapping，不形成独立 PC interaction design。

## 5. Environment and Grid boundary

- 正式 production scene 为现有 `Assets/Scenes/MainCafe.unity`；
- 不创建第二套 production MainCafe；
- 初始 Floor Layout 为 `8 × 8`；
- 每个 Floor Grid cell 为 `1 m × 1 m`；
- Decoration Mode 显示完整、低强调的 `8 × 8` Grid；
- 当前 Preview footprint 使用更强的 valid / invalid visual；
- 离开 Decoration Mode 后 Grid 隐藏；
- Floor Furniture 不能超出 Layout bounds，也不能进入 blocked、locked、occupied 或 Entrance Clearance cells。

Phase 6 不编辑 Wall Grid。Phase 4 Wall、Window 和 Wall Surface 继续显示但不可选择；Wall Decoration 与 Surface editing 属于 Phase 7。

## 6. Furniture Catalogue

### 6.1 Placeholder inventory rule

- 所有 Phase 6 Catalogue presets 都可以无限重复放置；
- 不显示价格、库存数量或解锁状态；
- 不实现购买、出售、economy、progression、分类、搜索或筛选；
- `Store` 只从当前 Layout 移除一个 Furniture Instance；
- Store 后对应 preset 仍可从 Catalogue 再次无限放置。

### 6.2 Catalogue presets

Phase 6 Catalogue 提供：

- Counter `1 × 1`；
- Counter `1 × 2`；
- Counter `1 × 3`；
- Counter `2 × 3`。

每个 preset 必须：

- 是独立 `FurnitureDefinitionAsset`；
- 使用稳定且唯一的 Definition ID；
- 引用一个明确 Prefab；
- author 明确的 Floor `Footprint`；
- `AllowedPlacementSurfaces = Floor`；
- 作为一个完整 Furniture Instance 移动和旋转；
- 不根据多个相邻 `1 × 1` instances 自动合并；
- 可以在未来替换正式模型时尽量保留 Definition ID。

### 6.3 Preset visuals

- `1 × 1` 复用现有 Counter Module；
- `1 × 2` 在一个 Prefab root 下组合两份 Counter model visual；
- `1 × 3` 优先检查并复用现有 `PF_Validation_Counter_1x3_01.prefab` 的 authoring structure，再升级为 Catalogue preset；
- `2 × 3` 在一个 Prefab root 下组合六份 Counter model visual；
- 不通过拉伸单一模型制造 multi-cell size；
- 多个 child visuals 不改变“一个 Layout Instance”的数据合同。

### 6.4 Work Table boundary

- Phase 4 Work Table Definition、Prefab、source reference、validators 与 tests 保留；
- Work Table 不显示在 Phase 6 Catalogue；
- Work Table 不作为 Phase 6 MainCafe 初始家具；
- Phase 8 再判断 Work Table 是否具有区别于 Counter Module 的 gameplay 意义；
- Phase 6 不删除或合并 completed Phase 4 Work Table assets。

### 6.5 Catalogue thumbnails

- Catalogue 使用预先生成的透明背景 `thumbnail Sprite`；
- thumbnail 使用统一 Camera angle、framing、lighting 和 scale convention；
- 不在每个 Catalogue tile 中实时运行 3D Camera 或 RenderTexture；
- Prefab visual 更新后，Editor authoring tool 可以重新生成 thumbnail；
- tile 显示 thumbnail、简短名称与 `1 × 1` / `1 × 2` / `1 × 3` / `2 × 3` size；
- missing Definition、Prefab 或 thumbnail 必须产生明确 validation issue；不能生成空白 Scene object。

## 7. MainCafe initial state

- 移除 `TEMP_P4_ManualReviewFixtures_DELETE_LATER` 与只服务于该 temporary fixture 的 production-scene content；
- 保留 Phase 4 Floor、Walls、Entrance 和 Window；
- MainCafe 初始 Floor Layout 只摆放一个正式 `1 × 1 Counter Module`，用于立即测试已有家具 selection / edit；
- 其他 Counter sizes 由玩家从 Catalogue 放入；
- Cash Register 和 Coffee Machine 不预先摆放在 Surface Slot 上；
- Phase 6 不删除 Phase 4 Cash Register、Coffee Machine 或 Surface Slot contracts。

任何 temporary fixture support code、materials 或 regression tests 的删除范围必须在 implementation plan 中逐项列出并证明不再有其他 consumer；未经明确 review 不做 broad cleanup。

## 8. Decoration Mode lifecycle

### 8.1 States

```text
Closed
→ BrowsingCatalogue
→ PreviewingNewFurniture | EditingExistingFurniture
→ Confirm | Cancel
→ BrowsingCatalogue
```

`ConfirmingStore` 是 EditingExistingFurniture 上方的 temporary Modal state。

同一时间最多存在一个 active furniture Preview。

### 8.2 Enter

- 正常 HUD 提供独立 Decoration Mode entry；
- 进入时获得自己的 Pause reason / ownership；
- 进入前的 Game Time state 被保留；
- NPC 与经营行为暂停；
- Bottom Sheet 打开 Catalogue；
- Grid 显示；
- 不修改现有 Layout。

### 8.3 Exit

- 如果存在 active Preview，退出动作先自动执行 `Cancel`；
- 不弹出未确认修改 Modal；
- Grid 与 placement visuals 清除；
- Decoration Pause ownership 释放；
- 恢复进入 Decoration Mode 前合理的 Game Time state；
- 不覆盖其他系统仍持有的 Pause reason；
- 不留下 stale input owner、blocker、Preview 或 duplicated Scene representation。

## 9. Catalogue and Bottom Sheet flow

- 进入 Decoration Mode 时 Bottom Sheet 显示 Catalogue grid；
- Catalogue 可以向下收成小把手，保留更多 Scene view；
- 点击 Catalogue tile 后 Catalogue 缩回；
- Bottom Sheet 同一区域切换为 compact actions：`Store / Rotate / Cancel / Confirm`；
- new furniture 不显示可用的 `Store` action；
- editing existing furniture 显示 `Store`；
- `Confirm` 或 `Cancel` 完成后重新显示 Catalogue；
- 当前 Bottom Sheet 高度、drag offset 和 transition values 是 provisional tuning parameters；
- Bottom Sheet、Modal 与 Safe Area 必须遵守 Phase 5 contracts。

## 10. Selecting and previewing furniture

### 10.1 New furniture

- 点击 Catalogue tile 后，在当前 Camera 画面中央附近的最近 Grid cell 创建 Preview；
- 即使初始 cell invalid，也保留 Preview 并显示 invalid visual；
- 系统不自动寻找远处的合法位置；
- Cancel 后新 Preview 完全消失，不创建正式 Layout Instance。

### 10.2 Existing furniture

- 在 Decoration Mode 中 tap 已摆放 Floor Furniture 后，它立即进入悬空 Preview；
- 原正式 Layout entry 在 Preview 期间保持未提交状态；
- placement validation 必须忽略当前被编辑 Instance 自己的旧 occupancy，但不能忽略其他 Instance；
- Cancel 后恢复原 position 与 rotation；
- Confirm 后原 entry 以一次 transaction 更新；
- 不允许 Scene 中同时存在同一 Instance 的两个正式 representations。

### 10.3 Changing selection

- active Preview 期间明确 tap 另一件 Furniture，当前 Preview 自动 `Cancel`；
- existing furniture 回原 position / rotation；
- new furniture Preview 消失；
- 然后新目标进入 Preview；
- 一次 drag release 落在另一件家具上不能被误判为新的 tap selection；
- active Preview 时 tap 空白地面不 Confirm、不 Cancel，也不移动 Preview；
- 没有 active Preview 时 tap 空白地面取消普通 selection 并返回 Catalogue state。

## 11. Touch interaction ownership

一次 Touch gesture 从 pointer down 到全部相关 pointers release 只能由一个明确 owner 处理。

### 11.1 One-finger drag

- 从 Furniture 开始的单指 drag：Furniture owns gesture；
- 从空白 Scene 开始的单指 drag：Camera pan owns gesture；
- 从 UI 开始的单指 gesture：UI owns gesture；
- gesture 中途移动到其他区域不能改变 owner；
- Furniture drag 时 Camera 不因普通 pan input 同时移动；
- UI gesture 不触发 Furniture、Camera 或 Scene selection。

### 11.2 Touch drag offset

- Furniture Preview 相对手指向上偏移，使手指不遮挡家具和 footprint；
- visual Preview 的实际 Grid position 是 placement validation position；
- 松开手指不自动 Confirm；
- offset distance 是 provisional tuning parameter，由 mobile-feel manual test 调整。

### 11.3 Pinch zoom

- Decoration Mode 保留双指 Pinch Camera zoom；
- Furniture drag 中第二根手指加入时，停止单指 Furniture drag update并切换到 Pinch；
- Furniture 保持在当前 Preview Grid position；
- 切换不 Confirm、不 Cancel；
- Pinch 结束后，Furniture 仍为 active Preview，可以继续单指 drag。

### 11.4 Edge auto-pan

- Furniture drag 接近有效 Scene viewport 边缘时请求 Camera auto-pan；
- 越靠近边缘速度越高，但必须有 maximum speed；
- 离开 edge zone 或 release 后立即停止；
- Bottom Sheet、Modal、Safe Area inset 和其他 UI regions 不触发 edge auto-pan；
- edge zone、speed curve 与 maximum speed 是 provisional tuning parameters；
- auto-pan 不改变 gesture owner，Furniture 仍 owns drag。

## 12. Grid snapping, rotation and placement visuals

### 12.1 Snapping

- Preview 连续跟随 Touch，但 placement position 始终吸附最近 Grid cells；
- footprint 的完整 occupied cells 必须可见；
- Grid highlight 不能修改正式 Floor mesh 或 material asset；
- Decoration Mode 退出后 highlight 完全清除。

### 12.2 Rotation

- `Rotate` 每次旋转 `90°`；
- `0° / 180°` 使用 authored Width × Depth；
- `90° / 270°` 交换 Width / Depth；
- 旋转尽量保持 visual center，再吸附最近 Grid cells；
- 系统不为获得合法结果而自动移动到远处；
- rotation 后立即重新计算 occupancy、highlight、validation reason 与 Confirm availability。

### 12.3 Valid and invalid feedback

- valid footprint 使用绿色 visual；
- invalid footprint 使用红色 visual；
- feedback 不能只依赖颜色，必须结合 icon、pattern、shape 或明确文字；
- 任意一个 footprint cell invalid，整个 placement invalid；
- invalid 时 `Confirm` disabled；
- UI 显示最具体、可行动的原因，例如：
  - `这里已有家具`；
  - `超出可装修区域`；
  - `这个区域尚未解锁`；
  - `入口区域不能放置家具`；
- Phase 6 使用 Phase 2 placement result，不在 UI 中复制 legality rules。

## 13. Confirm, Cancel and Store transactions

### 13.1 Confirm

- 每件家具单独 Confirm；
- Confirm 是一次 atomic Layout transaction；
- new furniture 创建一个正式 Instance；
- existing furniture 更新同一 Instance；
- repeated / double Confirm 只能提交一次；
- successful Confirm 后 Scene representation 与 Layout data 一致；
- Confirm 不影响此前已经确认的其他家具。

### 13.2 Cancel

- new furniture：移除 Preview，不创建 Layout entry；
- existing furniture：恢复原 position 与 rotation；
- Cancel 不影响其他 Furniture Instances；
- Cancel 后 occupancy、highlight、input ownership 与 Preview state 完全清理。

### 13.3 Store

- Store 只适用于 existing furniture；
- tap `Store` 后显示确认 Modal；
- Modal copy 明确说明将从当前 Layout 收起家具；
- Confirm Store 后一次性移除 Layout entry、occupancy 与 Scene representation；
- 返回 / dismiss Modal 后家具继续处于原 editing Preview state；
- Modal 阻止下层 UI 与 Scene input；
- Store 不改变 Catalogue inventory count，因为 Phase 6 inventory 为无限 placeholder。

## 14. Layout and Scene representation contract

- `CafeLayout` 是正式 runtime data Source of Truth；
- Scene representation 从 Layout entry 的 Instance ID、Definition ID、Grid position 与 rotation 得出；
- Scene object transform 不是唯一可信数据；
- 每个正式 Layout Instance 最多对应一个正式 Scene representation；
- Preview representation 与正式 representation 有明确 ownership 和 lifecycle；
- missing Definition / Prefab 产生具体 error / validation state，不静默丢失数据；
- rebuilding Scene representation 不重复生成 Instance；
- Confirm、Cancel、Store、owner disable、Scene reload 和 interruption 后都必须保持一致；
- Phase 6 不实现 Save file，但 stable Instance ID contract 必须能被 Phase 17 消费。

## 15. Runtime persistence boundary

- 本次运行期间已 Confirm 的 Layout changes 持续存在；
- 退出并重新进入 Decoration Mode 不重置；
- Pause、resume 或打开其他 UI 不重置；
- reload `MainCafe`、停止 Unity Play Mode 或重新启动游戏后可以恢复 initial Layout；
- Phase 6 不创建临时 Save file；
- Phase 6 不定义 Save schema、migration 或 corruption recovery；
- 正式 Save / Load 属于 Phase 17。

## 16. Functional furniture and future Phase boundaries

### Phase 7

- Wall Decoration；
- Window move / add / remove；
- Paint、Wallpaper、Wainscoting 与 Floor Surface appearance；
- Wall Slot Grid interaction；
- Camera / wall occlusion editing presentation。

### Phase 8

- Cash Register、Coffee Machine 与 Pick-up Surface placement；
- Surface Slot selection 与 occupancy；
- Equipment rotation；
- Employee / Customer interaction anchors；
- anchor validity；
- Layout readiness report；
- 缺少必要功能家具时阻止营业并提供具体原因。

Phase 6 可以移动提供 Surface Slot 的 Counter Furniture，但不放置或编辑 Surface-mounted equipment，也不能破坏 Phase 4 `SurfaceSlotMarker` contracts。

## 17. Explicitly out of scope

- Furniture Shop；
- price、purchase、sale、currency；
- finite inventory / warehouse；
- unlock、progression、category、search、filter；
- Atmosphere value 或 theme bonus；
- Cash Register / Coffee Machine operating behavior；
- Surface Slot equipment editing；
- Interaction Anchors；
- Customer AI、Employee AI；
- NavMesh rebuild 或完整 service-path validation；
- Pick-up Point；
- Wall Decoration；
- Paint、Wallpaper、Wainscoting、Floor Surface editing；
- build、move 或 remove walls；
- move Window；
- Save / Load；
- Undo / Redo history；
- multi-select、batch move、copy / duplicate action；
- final tutorial；
- final mobile layout、gesture、performance 或 platform adaptation；
- formal audio、haptics 或 VFX production。

## 18. Automated acceptance

### 18.1 Normal

- enter requests Pause and shows Catalogue；
- Catalogue loads exactly the intended Phase 6 presets；
- tiles bind correct Definition、thumbnail and footprint label；
- new Preview uses correct Definition and nearest visible Grid cell；
- `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3` highlight every occupied cell；
- four rotations produce correct Width / Depth；
- Confirm commits exactly once；
- move / rotate existing furniture updates the same Instance；
- Store confirmation removes the Instance；
- re-enter Decoration Mode preserves current runtime Layout。

### 18.2 Invalid and boundary

- overlap is rejected；
- outside `8 × 8` bounds is rejected；
- locked / blocked cell is rejected；
- Entrance Clearance Zone is rejected；
- any invalid cell invalidates the complete multi-cell footprint；
- invalid reason maps to specific player-facing copy；
- invalid Confirm is disabled and cannot be invoked through code or repeated input；
- rotation near bounds updates validity immediately。

### 18.3 Cancel and recovery

- new Preview Cancel leaves no Layout entry or Scene object；
- existing Preview Cancel restores original position / rotation；
- selecting another furniture auto-cancels the current Preview；
- tap blank with active Preview does not cancel；
- exit auto-cancels active Preview；
- dismiss Store Modal preserves edit state；
- disable / destroy owner clears Preview, pointer ownership and Decoration Pause request；
- interruption leaves no stale blocker, duplicate UI Root or duplicate Furniture representation。

### 18.4 Touch and input boundary

- Furniture-start drag moves only Furniture；
- blank-Scene-start drag pans only Camera；
- UI-start gesture affects only UI；
- ownership cannot switch mid-gesture；
- second Touch switches to Pinch without Confirm / Cancel；
- edge auto-pan starts and stops only in valid regions；
- Modal blocks underlying UI and Scene；
- drag release over another furniture does not create a false tap selection。

### 18.5 Presentation and compatibility

- Portrait `1080 × 1920`；
- small and tall Portrait；
- Landscape functional layout；
- simulated Safe Area；
- long localized labels；
- valid / invalid feedback remains understandable without color alone；
- thumbnail framing is deterministic and no tile is blank；
- Bottom Sheet transitions do not block Scene after close。

### 18.6 Regression

- full EditMode；
- full Editor PlayMode；
- standalone/mobile-compatible runtime test run；
- Phase 2 placement and occupancy suites；
- Phase 4 production asset validators；
- Phase 5 UI / input boundary suites；
- MainCafe canonical hierarchy and Build Settings isolation；
- no new runtime assembly reference to `UnityEditor`。

## 19. Manual playtest

Studio Owner 在 Unity Play Mode 中验证：

- Catalogue preview 是否清楚；
- preset size 是否容易理解；
- tap furniture 后悬空反馈是否明确；
- drag offset 是否避免手指遮挡；
- Grid 与 active footprint 是否清楚但不过度抢眼；
- valid / invalid feedback 和具体原因是否容易理解；
- Rotate 是否出现明显跳动；
- Bottom Sheet 是否遮挡目标位置；
- Pinch 与 single-finger drag 是否冲突；
- edge auto-pan zone 与速度是否自然；
- Confirm、Cancel、selection switch、exit auto-cancel 与 Store confirmation 是否符合预期；
- Portrait 操作空间、Safe Area 和 Landscape fallback；
- MainCafe Console 是否没有 unexpected Error / Exception / unexplained Warning。

以下数值在 implementation 后通过 Playtest 调整，不在 design spec 锁死：

- Preview hover height；
- touch drag offset；
- drag threshold；
- edge auto-pan zone；
- edge auto-pan speed curve / maximum speed；
- Bottom Sheet collapsed / action height；
- Grid line opacity、valid / invalid intensity 与 transition timing。

## 20. Risks and likely bugs

- Preview 提前修改正式 Layout；
- Cancel 后家具丢失或 occupancy 未恢复；
- moving Instance 被自己的旧 occupancy 阻挡；
- multi-cell 只检查 anchor cell；
- rotation 后 footprint、visual 与 occupancy 不一致；
- drag Furniture 时 Camera 同时 pan；
- second Touch 导致 unintended Confirm / Cancel；
- edge auto-pan 在 Bottom Sheet 或 Safe Area 上误触；
- Store double-submit；
- UI close click 穿透到 Scene；
- Scene reload 后永久 Pause 或 stale input owner；
- duplicate Scene representation；
- Catalogue thumbnail 与 Definition / Prefab 不匹配；
- replacing a placeholder visual silently changes footprint；
- temporary P4 fixture cleanup 删除仍有 consumer 的 support asset。

## 21. Department closeout targets

### Game Design

- direct-touch、single-item Confirm / Cancel flow 清楚；
- invalid feedback 可理解；
- explicit non-goals 不被提前实现；
- player flow ready for Studio Owner review。

### Production

- Phase 2 / 4 / 5 dependencies 明确；
- MainCafe、validation、asset 与 documentation scope 可追踪；
- completed Phase rework 有独立 regression boundary；
- Phase 7、8、17、50 ownership 不被 Phase 6 越界。

### Engineering

- Layout / Preview / Scene representation contracts 分离；
- Touch ownership 和 interruption recovery 可测试；
- asset authoring、thumbnail 与 preset contracts 可验证；
- implementation 可拆成小型 TDD tasks。

### QA & Player Research

- normal、invalid、boundary、recovery、Touch、compatibility 和 regression coverage 完整；
- manual mobile-feel tuning 项目明确；
- no completion claim before fresh full verification and Studio Owner hands-on acceptance。

## 22. Approval gate

进入 implementation planning 前必须完成：

1. Studio Owner review 并批准本 spec；
2. 另行写出 normal / invalid / boundary / regression test-case document；
3. 明确 manual playtest case IDs 与 acceptance evidence format；
4. 使用 `superpowers:writing-plans` 创建 implementation plan；
5. implementation plan 逐项列出会创建、修改或删除的 files / assets；
6. Studio Owner 再次批准 implementation plan；
7. 未经批准不开始 code、Scene 或 asset modification。
