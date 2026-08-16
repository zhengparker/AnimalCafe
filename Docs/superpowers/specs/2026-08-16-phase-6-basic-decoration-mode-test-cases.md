# AnimalCafe Phase 6 — Basic Decoration Mode Test Cases

> Status: Approved by Studio Owner
> Date: 2026-08-16
> Source spec: `Docs/superpowers/specs/2026-08-16-phase-6-basic-decoration-mode-design.md`
> Target engine: Unity `6000.5.5f1`
> Primary interaction: mobile `Touch Input`
> Primary presentation: Portrait `1080 × 1920`
> Implementation status: Not started

## 1. Purpose

本文件在 implementation 前定义 Phase 6 的 automated、integration、regression 与 Studio Owner manual test cases。

测试必须证明：

- Decoration Mode lifecycle、Pause ownership 与恢复正确；
- Catalogue 只提供已批准的 Counter presets；
- Preview 不提前修改正式 `CafeLayout`；
- Confirm、Cancel 与 Store transaction 可恢复、不可重复提交；
- `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3` footprint 全格验证；
- Layout data 与 Scene representation 一致；
- mobile Touch、Camera、UI 和 Scene input ownership 不冲突；
- invalid feedback 具体且不只依赖颜色；
- Phase 2、4、5 与 `MainCafe` regression 不被破坏。

本文件不授权 implementation。每项 automated behavior 必须在 implementation plan 中映射到 TDD RED → GREEN task。

## 2. Result definitions

- `PASS`：实际结果完整满足 Expected Result；
- `FAIL`：任意 expected condition 不满足；
- `BLOCKED`：测试环境或 approved dependency 无法运行，必须记录 blocker；
- `N/A`：只允许用于本 Phase 明确排除或当前 runner 无法测量的项目，并记录原因；
- automated result 不使用“看起来正常”或“基本通过”；
- failed、skipped、inconclusive 必须分别记录，不能只报告 passed count。

## 3. Test layers

### 3.1 EditMode

覆盖不依赖真实 frame / Touch 的 pure rules：

- state transitions；
- Catalogue filtering / binding；
- footprint 与 rotation；
- Preview transaction；
- validation reason mapping；
- Instance ID 与 Scene sync planning；
- asset / thumbnail / prefab validators；
- MainCafe canonical contract。

### 3.2 PlayMode

覆盖 runtime behavior：

- Decoration Mode enter / exit；
- real UI navigation；
- Input System Touch ownership；
- Furniture drag、Camera pan、Pinch 与 edge auto-pan；
- Preview visuals；
- Confirm / Cancel / Store；
- Layout / Scene representation；
- interruption recovery；
- `MainCafe` integration。

### 3.3 Standalone runtime

覆盖 player assembly、Scene loading、input mapping 与 runtime-only boundaries。Standalone 不能依赖 `UnityEditor`。

### 3.4 Manual Play Mode

覆盖 touch feel、readability、visual hierarchy、animation、Camera framing 和 beginner comprehension。Automated tests 不能替代 Studio Owner hands-on acceptance。

## 4. Shared fixtures

### 4.1 Approved production content

- Phase 4 `8 × 8` Floor；
- Back-left / Back-right Walls；
- Entrance 与 `2 × 2` Entrance Clearance Zone；
- Window；
- Counter `1 × 1`；
- Counter presets `1 × 2`、`1 × 3`、`2 × 3`；
- Phase 5 canonical UI Root、Bottom Sheet、Modal、Theme 和 input boundary。

### 4.2 Validation layout

Validation Scene 必须能够明确创建：

- empty valid cells；
- occupied cells；
- blocked cells；
- locked cells；
- Entrance Clearance cells；
- bounds edge / corner；
- enough free area for every approved footprint and rotation。

### 4.3 Touch fixtures

PlayMode tests 必须使用 Unity Input System Touch device / virtual Touch mapping，至少支持：

- one-finger tap；
- one-finger drag；
- second-finger join；
- two-finger Pinch；
- pointer IDs；
- press / move / release ordering；
- Safe Area coordinates。

Mouse 只可作为 Editor mapping coverage，不能替代 Touch acceptance。

## 5. Automated cases — lifecycle and state

### P6-LC-001 — Enter Decoration Mode

**Type:** PlayMode / Normal
**Precondition:** `MainCafe` running at normal Game Time；no modal open。
**Action:** Tap Decoration entry once。
**Expected:**

- state becomes `BrowsingCatalogue`；
- Decoration Pause reason acquired exactly once；
- effective Game Time becomes Paused；
- Grid visible；
- Catalogue Bottom Sheet visible；
- no Layout entry changes；
- no duplicate UI Root、EventSystem 或 Decoration owner。

### P6-LC-002 — Repeated enter is idempotent

**Type:** PlayMode / Boundary
**Action:** Dispatch repeated enter requests while already in Decoration Mode。
**Expected:** one active Decoration session、one Pause ownership、one Grid visual、one Bottom Sheet。

### P6-LC-003 — Exit without active Preview

**Type:** PlayMode / Normal
**Action:** Exit from `BrowsingCatalogue`。
**Expected:** Grid and Catalogue close；Decoration Pause ownership releases；pre-entry Game Time restores；Layout unchanged。

### P6-LC-004 — Exit auto-cancels existing-furniture Preview

**Type:** PlayMode / Recovery
**Action:** Move and rotate an existing Counter without Confirm；exit Decoration Mode。
**Expected:** original position / rotation restored；no pending transaction；Grid / Preview cleared；Game Time restores。

### P6-LC-005 — Exit auto-cancels new-furniture Preview

**Type:** PlayMode / Recovery
**Action:** Select new Catalogue item；drag without Confirm；exit。
**Expected:** Preview removed；no Layout Instance created；no occupancy residue。

### P6-LC-006 — Nested Pause ownership

**Type:** EditMode + PlayMode / Boundary
**Precondition:** another valid system already holds Pause。
**Action:** Enter then exit Decoration Mode。
**Expected:** Decoration releases only its own reason；effective Game Time remains Paused until other owner releases。

### P6-LC-007 — Restore non-default pre-entry speed

**Type:** PlayMode / Boundary
**Precondition:** Game Time is an approved non-default running speed。
**Action:** Enter then exit Decoration Mode。
**Expected:** prior speed restores；not forced to a hard-coded default。

### P6-LC-008 — Owner disable / destroy cleanup

**Type:** PlayMode / Recovery
**Action:** Disable or destroy the Decoration owner during active Preview。
**Expected:** Preview、Grid、input ownership、blocker and Decoration Pause reason clear；no permanent Pause or exception。

## 6. Automated cases — Catalogue and assets

### P6-CAT-001 — Catalogue includes approved presets

**Type:** EditMode
**Expected:** exactly the approved Counter `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3` definitions appear in the Phase 6 Catalogue source。

### P6-CAT-002 — Work Table hidden without deletion

**Type:** EditMode / Regression
**Expected:** Work Table Definition and Prefab remain valid under Phase 4 validators but are absent from Phase 6 Catalogue。

### P6-CAT-003 — Non-Floor definitions excluded

**Type:** EditMode / Invalid
**Expected:** Cash Register、Coffee Machine and Window do not appear in the Floor Catalogue。

### P6-CAT-004 — Tile binds correct data

**Type:** EditMode + PlayMode
**Expected:** each tile displays matching thumbnail、name、footprint label and Definition reference；no cross-binding after reopen。

### P6-CAT-005 — Unlimited repeated selection

**Type:** PlayMode / Normal
**Action:** Place the same preset repeatedly。
**Expected:** each Confirm creates a unique Instance；tile remains available；no inventory count appears or decreases。

### P6-CAT-006 — Missing Definition

**Type:** EditMode / Invalid
**Expected:** validator reports a specific missing Definition issue；runtime cannot create a blank Preview。

### P6-CAT-007 — Missing Prefab

**Type:** EditMode + PlayMode / Invalid
**Expected:** validator identifies the Definition；tile is disabled or excluded with specific feedback；no empty GameObject is committed。

### P6-CAT-008 — Missing thumbnail

**Type:** EditMode + PlayMode / Invalid
**Expected:** validator reports specific missing thumbnail；Catalogue never shows an unexplained blank tile。

### P6-CAT-009 — Duplicate Definition ID

**Type:** EditMode / Invalid
**Expected:** duplicate rejected with both asset paths；Catalogue ordering cannot silently select one。

### P6-CAT-010 — Preset structure and root scale

**Type:** EditMode / Asset contract
**Expected:** `1 × 2`、`1 × 3`、`2 × 3` each remain one root Furniture Prefab / one Definition；root scale and child model composition satisfy approved Phase 4 technical contracts。

### P6-CAT-011 — Thumbnail generation deterministic

**Type:** EditMode / Authoring
**Action:** Generate thumbnails twice from unchanged Prefabs。
**Expected:** framing、orientation、background、resolution and output identity remain deterministic；no duplicate asset GUIDs。

### P6-CAT-012 — Catalogue reopen has no duplicate tiles

**Type:** PlayMode / Recovery
**Action:** select、Cancel / Confirm and reopen Catalogue repeatedly。
**Expected:** exactly one tile per approved Definition；no duplicated handlers or stale selection。

## 7. Automated cases — Preview and selection

### P6-PRV-001 — New Preview starts near visible Camera center

**Type:** PlayMode / Normal
**Action:** Select a Catalogue tile。
**Expected:** Preview uses the selected Definition and nearest Grid cell around visible Camera center；Catalogue compacts to action bar。

### P6-PRV-002 — Invalid initial location remains visible

**Type:** PlayMode / Boundary
**Precondition:** Camera center resolves to invalid cells。
**Expected:** Preview remains at nearest cells with invalid feedback；system does not jump to a distant valid location。

### P6-PRV-003 — Existing furniture tap creates one Preview

**Type:** PlayMode / Normal
**Expected:** selected furniture appears suspended；one active Preview exists；original Layout entry is not yet changed。

### P6-PRV-004 — One active Preview maximum

**Type:** EditMode + PlayMode / Invariant
**Expected:** no transition can create two active Previews or two pending transactions。

### P6-PRV-005 — Select another existing furniture

**Type:** PlayMode / Recovery
**Action:** edit Furniture A；explicitly tap Furniture B。
**Expected:** A auto-cancels to original position / rotation；B becomes the only active Preview。

### P6-PRV-006 — Select another while new Preview active

**Type:** PlayMode / Recovery
**Action:** create new A Preview；tap existing B。
**Expected:** A disappears without Layout entry；B becomes active Preview。

### P6-PRV-007 — Blank tap with active Preview

**Type:** PlayMode / Boundary
**Expected:** no Confirm、Cancel or movement；Preview remains active。

### P6-PRV-008 — Blank tap without active Preview

**Type:** PlayMode / Normal
**Expected:** ordinary selection clears and state returns to `BrowsingCatalogue`。

### P6-PRV-009 — Drag release over another furniture

**Type:** PlayMode / Bug regression
**Action:** begin drag on A and release pointer over B。
**Expected:** A remains active at snapped Preview position；B is not selected；no auto-cancel caused by release。

### P6-PRV-010 — Preview visual does not become formal data

**Type:** EditMode + PlayMode / Invariant
**Expected:** moving / rotating Preview changes no formal Layout entry、occupancy snapshot or stable Scene representation before Confirm。

## 8. Automated cases — footprint, snapping and rotation

### P6-GRID-001 — Complete `1 × 1` footprint

**Expected:** exactly one correct Grid cell highlighted and validated。

### P6-GRID-002 — Complete `1 × 2` footprint

**Expected:** exactly two contiguous authored cells highlighted and validated。

### P6-GRID-003 — Complete `1 × 3` footprint

**Expected:** exactly three contiguous authored cells highlighted and validated。

### P6-GRID-004 — Complete `2 × 3` footprint

**Expected:** exactly six cells highlighted and validated；no anchor-cell-only shortcut。

### P6-GRID-005 — Rotation swaps asymmetric footprint

**Type:** EditMode + PlayMode / Normal
**Expected:** `1 × 2 ↔ 2 × 1`、`1 × 3 ↔ 3 × 1`、`2 × 3 ↔ 3 × 2` at `90° / 270°`；`0° / 180°` use authored dimensions。

### P6-GRID-006 — Four rotations return to origin contract

**Expected:** four Rotate actions restore original orientation、footprint and deterministic snapped position；no accumulated transform drift。

### P6-GRID-007 — Rotation preserves visual center

**Type:** PlayMode / Presentation contract
**Expected:** asymmetric furniture re-snaps nearest to prior visual center；does not jump to a distant valid cell。

### P6-GRID-008 — Rotation becomes invalid near bounds

**Expected:** Preview stays visible；highlight turns invalid；reason is bounds-specific；Confirm disabled。

### P6-GRID-009 — Drag snapping deterministic

**Type:** EditMode + PlayMode / Boundary
**Expected:** same pointer world position and orientation always resolve to the same Grid anchor；threshold boundary has an explicit deterministic tie rule。

### P6-GRID-010 — Full Grid lifecycle

**Type:** PlayMode / Normal
**Expected:** subtle `8 × 8` Grid appears only in Decoration Mode；active footprint is stronger；all visuals clear on exit / disable / reload。

## 9. Automated cases — placement validation and feedback

### P6-VAL-001 — Valid placement

**Expected:** every footprint cell valid；green / valid visual and non-color cue present；Confirm enabled；no error copy。

### P6-VAL-002 — One occupied cell invalidates multi-cell

**Expected:** complete placement invalid；specific occupied cells visibly indicated；reason `这里已有家具`；Confirm disabled。

### P6-VAL-003 — One blocked cell invalidates multi-cell

**Expected:** complete placement invalid；blocked reason specific；no partial Confirm。

### P6-VAL-004 — One locked cell invalidates multi-cell

**Expected:** complete placement invalid；reason `这个区域尚未解锁`；Confirm disabled。

### P6-VAL-005 — Entrance Clearance Zone

**Expected:** any intersecting footprint invalid；reason `入口区域不能放置家具`。

### P6-VAL-006 — Left, right, top and bottom bounds

**Type:** EditMode + PlayMode / Boundary
**Expected:** every out-of-bounds direction rejected with `超出可装修区域`；no index exception。

### P6-VAL-007 — Four corners

**Type:** Boundary
**Expected:** multi-cell footprint validates every corner correctly at every rotation。

### P6-VAL-008 — Existing furniture ignores only itself

**Type:** EditMode + PlayMode / Bug regression
**Expected:** Preview may overlap its original occupancy；cannot overlap any other Instance；Confirm produces one final occupancy set。

### P6-VAL-009 — Validation reason updates during drag

**Expected:** valid / invalid visual、specific copy and Confirm availability update in the same interaction cycle as snapped cell changes。

### P6-VAL-010 — Feedback not color-only

**Type:** EditMode + PlayMode / Accessibility
**Expected:** valid and invalid states differ through approved icon、pattern、shape or text in addition to color。

### P6-VAL-011 — UI cannot override placement legality

**Type:** EditMode / Architecture
**Expected:** UI consumes Phase 2 placement result / mapped reason；no duplicate bounds or occupancy algorithm exists in Catalogue / Bottom Sheet code。

### P6-VAL-012 — Disabled Confirm cannot be invoked indirectly

**Type:** PlayMode / Invalid
**Action:** attempt button tap、submit action、repeated input and direct command path while invalid。
**Expected:** no formal Layout mutation；one specific validation response maximum per approved feedback rule。

## 10. Automated cases — Confirm, Cancel and Store

### P6-TXN-001 — Confirm new furniture

**Expected:** one stable Instance ID；correct Definition ID、Grid position、rotation and footprint；one occupancy commit；one Scene representation。

### P6-TXN-002 — Confirm existing move

**Expected:** same Instance ID updated；old occupancy released；new occupancy acquired；no remove-plus-duplicate identity change。

### P6-TXN-003 — Confirm existing rotation

**Expected:** same Instance ID and Definition；rotation and occupancy update atomically；Scene transform matches Layout。

### P6-TXN-004 — Double Confirm

**Type:** PlayMode / Bug regression
**Action:** dispatch two Confirm inputs in the same or adjacent frames。
**Expected:** exactly one commit、one Instance、one representation and one completion feedback。

### P6-TXN-005 — Cancel new furniture

**Expected:** Preview destroyed；zero formal entry、occupancy or permanent representation。

### P6-TXN-006 — Cancel existing move and rotation

**Expected:** original Grid position、rotation、occupancy and Scene representation restored exactly。

### P6-TXN-007 — Cancel does not affect prior Confirm

**Expected:** cancelling current furniture leaves all previously confirmed furniture unchanged。

### P6-TXN-008 — Store opens Modal

**Expected:** `ConfirmingStore` state；Modal blocks lower UI and Scene；Furniture not yet removed。

### P6-TXN-009 — Dismiss Store Modal

**Expected:** Modal closes；Furniture remains active Preview in prior editing state；no Layout mutation。

### P6-TXN-010 — Confirm Store

**Expected:** exact Instance、occupancy and Scene representation removed once；Catalogue preset remains available。

### P6-TXN-011 — Double Store confirmation

**Type:** Bug regression
**Expected:** second input is ignored safely；no missing-key exception or removal of another Instance。

### P6-TXN-012 — Store unavailable for new Preview

**Expected:** Store action hidden or disabled for a not-yet-confirmed new Preview；no store transaction exists without stable Instance ID。

## 11. Automated cases — Layout and Scene representation

### P6-SYNC-001 — Initial MainCafe representation

**Expected:** one initial `1 × 1 Counter Module` Layout Instance corresponds to exactly one Scene representation。

### P6-SYNC-002 — Rebuild idempotency

**Action:** rebuild Scene representation twice from unchanged Layout。
**Expected:** one object per Instance ID；no duplicate roots or children。

### P6-SYNC-003 — Confirm sync

**Expected:** Layout entry and Scene transform / orientation agree immediately after transaction completion。

### P6-SYNC-004 — Cancel sync

**Expected:** formal Layout and Scene remain at original values；no Preview child survives。

### P6-SYNC-005 — Store sync

**Expected:** removed Instance absent from Layout lookup、occupancy and Scene hierarchy。

### P6-SYNC-006 — Missing Definition during rebuild

**Type:** Invalid / Recovery
**Expected:** specific issue references Instance ID and Definition ID；system does not substitute a wrong Prefab or silently delete data。

### P6-SYNC-007 — Missing Prefab during rebuild

**Expected:** specific recoverable error；other valid Instances still rebuild；no empty permanent representation。

### P6-SYNC-008 — Scene transform cannot bypass Layout

**Type:** Architecture
**Expected:** direct temporary transform changes do not become formal Layout data without approved transaction。

### P6-SYNC-009 — Unique Instance IDs under rapid placement

**Type:** Boundary
**Action:** Confirm multiple identical Definitions rapidly。
**Expected:** every Instance ID unique and stable；no collision or overwritten dictionary entry。

### P6-SYNC-010 — Runtime-session persistence

**Expected:** confirmed changes survive Decoration close / reopen and unrelated UI navigation within the same run。

### P6-SYNC-011 — Scene reload boundary

**Expected:** reload may restore approved initial Layout；no Phase 6 Save file created；no expectation of cross-reload persistence。

## 12. Automated cases — Touch, Camera and UI ownership

### P6-IN-001 — Furniture-start single-finger drag

**Expected:** Furniture Preview moves；Camera pan delta not applied；owner remains Furniture until release。

### P6-IN-002 — Blank-Scene-start single-finger drag

**Expected:** Camera pans；no Furniture selection or Preview movement。

### P6-IN-003 — UI-start gesture

**Expected:** UI owns complete gesture；Camera、Grid selection and Furniture do not react。

### P6-IN-004 — Owner cannot switch mid-gesture

**Action:** begin on Furniture then move through blank Scene and UI。
**Expected:** gesture remains Furniture-owned until release；UI and Camera ignore it。

### P6-IN-005 — Drag threshold separates tap and drag

**Expected:** movement within threshold selects without unintended pan；movement beyond threshold drags without generating release tap。

### P6-IN-006 — Touch drag offset

**Expected:** Preview uses configured offset consistently；placement position matches visible Preview rather than hidden finger coordinate。

### P6-IN-007 — Second finger joins Furniture drag

**Expected:** single-finger drag update stops；Pinch becomes active；Furniture retains current snapped Preview；no Confirm / Cancel。

### P6-IN-008 — Pinch ends, Preview remains editable

**Expected:** Camera zoom applied within bounds；Furniture remains active；subsequent one-finger drag works。

### P6-IN-009 — Edge auto-pan starts

**Expected:** Furniture-owned drag in approved edge zone requests Camera pan in correct direction；Preview continues snapping。

### P6-IN-010 — Edge auto-pan speed cap

**Expected:** speed increases according to approved curve but never exceeds maximum；frame-rate-independent movement。

### P6-IN-011 — Edge auto-pan stops

**Expected:** leaving edge zone、pointer release、Cancel、Confirm、Modal open or owner disable stops auto-pan immediately。

### P6-IN-012 — Safe Area and Bottom Sheet do not trigger edge pan

**Expected:** Touch within excluded UI / inset regions never produces Camera auto-pan。

### P6-IN-013 — Modal blocks lower layers

**Expected:** Store Modal gesture cannot rotate、drag、select、pan or activate Catalogue below it。

### P6-IN-014 — UI close input does not pass to Scene

**Type:** Phase 5 regression
**Expected:** the input that closes / dismisses UI cannot select or move Furniture in the same gesture。

### P6-IN-015 — Multiple pointer IDs clean up independently

**Type:** Boundary / Recovery
**Expected:** release / cancellation order does not leave stale ownership or blocked future input。

## 13. Automated cases — responsive UI and accessibility

### P6-UI-001 — Portrait reference

**Expected:** Catalogue、action bar、Modal、Grid view and key furniture remain readable at `1080 × 1920` reference。

### P6-UI-002 — Small Portrait

**Expected:** controls remain inside Safe Area；no overlap or inaccessible Confirm / Cancel。

### P6-UI-003 — Tall Portrait

**Expected:** no stretched tiles、unbounded empty space or misplaced Bottom Sheet。

### P6-UI-004 — Landscape fallback

**Expected:** all actions functional and visible；no final Landscape-specific polish required。

### P6-UI-005 — Long localized labels

**Expected:** preset names、validation reasons and Store confirmation wrap / truncate according to Phase 5 rules without hiding essential meaning。

### P6-UI-006 — Minimum touch targets

**Expected:** Catalogue tiles and actions meet Phase 5 minimum touch target contract。

### P6-UI-007 — Catalogue / action transition

**Expected:** selection compacts Catalogue into action bar once；Confirm / Cancel returns to Catalogue once；interrupted transition leaves one usable state。

### P6-UI-008 — Thumbnail consistency

**Expected:** all four presets use consistent angle、framing and readable relative size；no clipping or transparent-empty image。

### P6-UI-009 — Invalid reason specificity

**Expected:** copy matches placement failure and remains visible without covering essential Preview area。

### P6-UI-010 — Color-independent state

**Expected:** grayscale / color-vision review still distinguishes valid and invalid through non-color cues and text。

### P6-UI-011 — Reduced Motion expansion point

**Expected:** Bottom Sheet、hover and highlight transitions use Phase 5 motion contract and can honor Reduced Motion without changing logic。

### P6-UI-012 — Pause-time UI remains interactive

**Expected:** Catalogue、Rotate、Cancel、Confirm and Store Modal use unscaled UI behavior while Game Time is Paused。

## 14. Automated cases — authoring, MainCafe and cleanup

### P6-AUTH-001 — Builder idempotency

**Expected:** running approved Phase 6 Scene / asset setup twice creates no duplicate presets、thumbnails、UI nodes、runtime owners or Layout entries。

### P6-AUTH-002 — MainCafe uses canonical Phase 4 environment

**Expected:** exact Floor、Walls、Entrance and Window Prefab references remain valid；no copied replacement environment。

### P6-AUTH-003 — Temporary P4 fixture removed intentionally

**Expected:** `TEMP_P4_ManualReviewFixtures_DELETE_LATER` absent after approved migration；no unrelated Phase 4 production asset removed。

### P6-AUTH-004 — Temporary support consumer scan

**Expected:** every proposed temporary setup utility、material or test deletion has zero remaining consumer；otherwise retain it。

### P6-AUTH-005 — MainCafe initial Layout

**Expected:** exactly one approved `1 × 1 Counter Module` formal Instance；other Counter sizes available through Catalogue, not pre-filled in Scene。

### P6-AUTH-006 — Cash Register / Coffee Machine not placed

**Expected:** Surface equipment is absent from Phase 6 initial Layout and Floor Catalogue；Phase 4 assets and tests remain intact。

### P6-AUTH-007 — Window not selectable in Phase 6

**Expected:** Window remains visible but Floor Furniture selection cannot acquire it；Phase 7 ownership preserved。

### P6-AUTH-008 — Validation Scene excluded from production Build Settings

**Expected:** `MainCafe` remains the sole approved production Scene；Validation Scene is not enabled for player build。

### P6-AUTH-009 — Runtime assembly boundary

**Expected:** Phase 6 runtime assemblies reference no `UnityEditor` API；Editor tools remain in Editor assembly。

### P6-AUTH-010 — Missing / duplicate canonical nodes validator

**Expected:** specific issues for missing or duplicate Decoration owner、Grid root、Catalogue binding、UI Root or EventSystem。

## 15. Regression suites

### P6-REG-001 — Phase 1 Layout regression

Run full existing Layout data tests；failed / skipped / inconclusive must be `0` unless a separately approved known limitation is recorded。

### P6-REG-002 — Phase 2 placement regression

Run full occupancy、bounds、rotation、blocked / locked and Entrance rules。

### P6-REG-003 — Phase 4 asset regression

Run production validators for Work Table、Counter、Coffee Machine、Cash Register、Window、Floor、Walls and Entrance。Work Table remains valid despite Catalogue exclusion。

### P6-REG-004 — Phase 5 UI regression

Run full UI theme、navigation、Modal、Bottom Sheet、Safe Area、Pause and pointer-boundary suites。

### P6-REG-005 — Full EditMode

Fresh full EditMode result required before completion claim。

### P6-REG-006 — Full Editor PlayMode

Fresh full Editor PlayMode result required；order-sensitive Input System failures cannot be hidden by reporting only focused suites。

### P6-REG-007 — Standalone runtime

Fresh standalone/mobile-compatible runtime result required；must validate player assembly and Scene loading。

### P6-REG-008 — MainCafe smoke

Load production `MainCafe`；enter / use / exit Decoration Mode；resume Game Time；Console has no unexpected Error / Exception / unexplained Warning。

### P6-REG-009 — Focused real Touch suite

Run focused Touch tests separately to provide readable evidence, but do not treat them as replacement for full PlayMode。

### P6-REG-010 — Repeat / order check

Run the relevant full suite again when initial run modifies canonical assets、imports thumbnails or exposes order dependence；report both initial and rerun results honestly。

## 16. Studio Owner manual playtest

Manual cases use the production `MainCafe` unless explicitly marked Validation Scene。

### P6-M-001 — Enter and presentation

Enter Decoration Mode from HUD。Confirm automatic Pause、full subtle Grid、Catalogue Bottom Sheet and clean visual hierarchy。

### P6-M-002 — Catalogue thumbnails

Confirm four Counter tiles show recognizable furniture previews、consistent angle and clear size labels。

### P6-M-003 — New `1 × 1`

Select、drag、Rotate、Confirm。Confirm Preview appears near Camera center and becomes one formal Scene furniture。

### P6-M-004 — New `1 × 2`

Confirm two-cell highlight、rotation to `2 × 1` and single-instance movement。

### P6-M-005 — New `1 × 3`

Confirm three-cell highlight、rotation、visual composition and no model stretching。

### P6-M-006 — New `2 × 3`

Confirm six-cell highlight、rotation to `3 × 2` and readable footprint in Portrait。

### P6-M-007 — Existing furniture selection

Tap initial `1 × 1 Counter Module`。Confirm immediate suspended Preview and compact action bar。

### P6-M-008 — Touch drag offset

Drag every size。Confirm finger does not hide furniture / footprint and Preview position feels connected to Touch。

### P6-M-009 — Valid feedback

Confirm green plus non-color cue is clear without overpowering Floor / furniture art。

### P6-M-010 — Occupied invalid feedback

Overlap another furniture。Confirm red plus non-color cue、specific `这里已有家具` message and disabled Confirm。

### P6-M-011 — Bounds invalid feedback

Drag every asymmetric size across multiple Floor edges。Confirm correct cells and `超出可装修区域`。

### P6-M-012 — Entrance Clearance

Drag a multi-cell preset into the Entrance zone。Confirm placement is blocked and reason is specific。

### P6-M-013 — Rotate near obstacle

Rotate from valid to invalid and back。Confirm center does not jump far、highlight updates immediately and no hidden Confirm occurs。

### P6-M-014 — Cancel existing

Move and rotate initial Counter；Cancel。Confirm exact original position / rotation returns。

### P6-M-015 — Cancel new

Select a new preset；move；Cancel。Confirm it disappears completely and Catalogue returns。

### P6-M-016 — Switch selection

Edit A then tap B。Confirm A automatically returns / disappears as applicable and B becomes the only active Preview。

### P6-M-017 — Blank tap rules

With active Preview, tap blank Floor and confirm nothing is cancelled。Without Preview, tap blank Floor and confirm ordinary selection clears。

### P6-M-018 — Store confirmation

Tap Store；verify blocking Modal。Dismiss once, then reopen and confirm Store。Confirm only the selected furniture is removed。

### P6-M-019 — Exit auto-cancel

Leave an existing furniture unconfirmed and exit Decoration Mode。Confirm original position returns and no warning Modal appears。

### P6-M-020 — Runtime-session persistence

Confirm multiple changes；exit and re-enter Decoration Mode。Confirm the current runtime Layout remains。

### P6-M-021 — Camera pan ownership

Single-finger drag from blank Floor。Confirm only Camera moves。

### P6-M-022 — Furniture drag ownership

Single-finger drag from Furniture。Confirm Furniture moves and Camera stays stable except approved edge auto-pan。

### P6-M-023 — Pinch during active Preview

Start Furniture drag, add second finger and Pinch。Confirm Camera zooms、Furniture stays pending and no Confirm / Cancel occurs。

### P6-M-024 — Edge auto-pan

Drag each furniture size near all usable viewport edges。Confirm direction、activation zone、speed curve and stop behavior feel controllable。

### P6-M-025 — Bottom Sheet and Safe Area

Confirm Catalogue / action bar transitions do not hide furniture or essential actions；Safe Area does not cause edge-pan misfires。

### P6-M-026 — Portrait sizes

Check reference、small and tall Portrait。Confirm no clipping、overlap or unreachable controls。

### P6-M-027 — Landscape fallback

Confirm all actions remain usable and readable without requiring final Landscape polish。

### P6-M-028 — Beginner comprehension

Without reading implementation notes, confirm it is clear how to select、drag、rotate、confirm、cancel and store；record any confusing label or hidden state。

### P6-M-029 — Repeated session recovery

Enter / exit twice；perform Confirm、Cancel and Store across sessions。Confirm no duplicate UI、Grid、Furniture、permanent Pause or broken input。

### P6-M-030 — Console and final smoke

Complete a mixed placement session and resume Game Time。Console must contain no unexpected Error / Exception or unexplained Warning。

## 17. Manual tuning record

The manual evidence must record the accepted values or observations for：

- Preview hover height；
- touch drag offset；
- drag threshold；
- edge auto-pan zone；
- edge auto-pan speed curve / maximum speed；
- Bottom Sheet collapsed / action height；
- Grid line opacity；
- valid / invalid intensity；
- transition timing；
- Portrait Camera framing and furniture readability。

These are not pass-by-default. Any value changed after manual acceptance requires rerunning affected focused tests and the relevant manual cases。

## 18. Required evidence before Phase 6 completion

- TDD RED evidence for every new behavior group；
- focused EditMode results；
- focused PlayMode real Touch results；
- Phase 4 asset validator result；
- Phase 6 Scene / Catalogue validator result；
- fresh full EditMode XML and log；
- fresh full Editor PlayMode XML and log；
- fresh standalone runtime XML and log；
- manual `P6-M-001–030` result sheet；
- Console evidence；
- known limitations with explicit acceptance；
- exact Unity version、commit / working-tree state and test timestamps。

Focused passes do not permit a “fully green” claim when a full suite fails。A rerun pass after an initial failure must report the initial cause and corrective action or environmental explanation。

## 19. Approval gate

Before implementation planning：

1. Studio Owner reviews and approves this test-case document；
2. unresolved test behavior returns to design discussion；
3. approved cases are mapped into `superpowers:writing-plans` tasks；
4. each implementation task names RED command、expected failure、GREEN command and regression boundary；
5. Studio Owner reviews the implementation plan；
6. no code、Scene or asset implementation starts before that approval。
