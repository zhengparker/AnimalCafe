# AnimalCafe Phase 7 — Interior Walls & Surface Customization Test Cases

> Status: `PASS — merged-main automated regression and Studio Owner manual acceptance complete; Phase 7 closed`
>
> Date: 2026-08-24；amended through post-merge remediation 2026-09-02
>
> Source spec: `Docs/superpowers/specs/2026-08-24-phase-7-interior-walls-surface-customization-design.md`
>
> Source plan: `Docs/superpowers/plans/2026-08-24-phase-7-interior-walls-surface-customization.md`
>
> Target engine: Unity `6000.5.5f1`
>
> Primary interaction: mobile `Touch Input`；Mouse 只作为 Unity Editor mapping
>
> Implementation status: `Tasks 1–20 implemented and reviewed；MT-001–MT-034 34/34 PASS；Studio Owner decision GO`
>
> 测试预期只来自 approved spec / plan，不从 implementation 反推；文末 final closeout evidence 记录当前通过状态，前文 pending notes 保留为历史时间点。

## 1. Purpose

本文件在 Phase 7 implementation 继续前冻结 automated、integration、regression 与 Studio Owner manual test cases。它必须证明：

- `RoomSurfaceLayout` 以 stable IDs 表达当前 Room 的 2 面 Walls 与 `8 × 8` Floor；
- 每面墙的 `8 × 2` Wall Slots、occupancy、bounds 与 cross-wall move 保持 atomic；
- Surface Preview、Undo、Confirm、Cancel 不会在 Confirm 前污染 confirmed Layout；
- Floor `Whole Room / Single Grid`、rotation 与 multi-grid Preview 符合 approved transaction；
- Wall 在同一 target transaction 内组合 Preview `Base Surface / Wainscoting / None`，并以一次 Confirm / Cancel 原子提交或恢复；Wall 不提供 Apply All；
- `Furniture / Floor / Wall / Wall Decor` 四个 Mode 的 Scene input ownership 隔离；
- Bottom Sheet 三种 snap state、nested scrolling、fixed Surface footer、`Using / Preview` visual grammar 与 `0.16s` transition 可用；
- Wall projection、Invalid reason、occlusion fade 与 cleanup 可恢复；
- Wall Decor / Window 支持 new、move、Store、cross-wall drag，且不提供 Rotate；
- Phase 4、Phase 6 与 production `MainCafe` behavior 不回退；
- automated / placeholder technical acceptance 与正式素材 visual acceptance 明确分离。

本文件不增加 price、inventory、unlock、Save / Load UI、wall construction、real opening 或其他 player-facing rule；也不授权 commit、push、merge、branch/worktree cleanup 或 Phase completion。

## 2. Result definitions

- `PASS`：实际结果完整满足该 case 的全部 Expected Result，并留下指定 Evidence。
- `FAIL`：任一 Expected Result 不满足；不能以“基本正常”替代失败记录。
- `BLOCKED`：runner、Scene 或 approved dependency 无法运行；必须记录 blocker、owner 与下一步。
- `N/A`：只允许用于 approved scope 明确排除或当前 runner 无法测量的项目，并写明原因。
- `READY`：现有 pure fixture、placeholder 或已批准基础资产足以执行技术验收。
- `WAIT-ASSET`：只有正式 Floor / swatch / projection / Wall Decor / Window 等素材到位后，才能作正式视觉验收；placeholder 通过不能把它改记为 `PASS`。
- automated evidence 必须记录 passed / failed / skipped / inconclusive counts；`skipped` 或 `inconclusive` 不能折算为通过。
- automated、Mock、placeholder 与截图 evidence 不能替代 Studio Owner hands-on Play Mode acceptance。
- 每个 case 的 Risk 首词标明 `Normal / Invalid / Boundary / Recovery / Regression / Visual`；后接该失败会造成的主要影响。

## 3. Test layers

### 3.1 Automated EditMode (`AT-*`)

验证 pure data、stable identity、validation、transaction、Catalogue contract、asset contract 与 builder/validator 的 deterministic behavior。除 Scene/asset authoring transaction 外，不依赖 frame、rendering 或真实 Touch。

### 3.2 Integration / PlayMode (`IT-*`)

验证真实 uGUI、Input System Touch、Scene views、MaterialPropertyBlock、projection、fade cleanup、lifecycle 与 `MainCafe` integration。UI pointer、Touch ordering 与 Scene loading 不能只由 pure fake 代替。

### 3.3 Regression (`RT-*`)

验证 Phase 1/2/4/5/6、production Scene、runtime/editor assembly boundary 及完整 EditMode / PlayMode suites。focused tests 不能替代最终 full regression。

### 3.4 Studio Owner Manual Play Mode (`MT-*`)

验证 beginner comprehension、visual hierarchy、texture seams、snap feel、fade opacity、responsive layout 与正式素材外观。凡属视觉品味或 provisional tuning，由 Studio Owner 最终决定。

## 4. Shared fixtures

### 4.1 Pure domain fixture

- Room ID：`room.main`，符合 lowercase stable ID pattern `^[a-z0-9][a-z0-9._-]*$`。
- Wall Surface IDs：`wall.back-left`、`wall.back-right`，恰好 2 面。
- Floor positions：`GridPosition(0..7, 0..7)`，恰好 64 个 unique cells。
- Floor rotations：`Degrees0 / Degrees90 / Degrees180 / Degrees270`。
- Wall appearances：一个 Paint Base、一个 Wallpaper Base；Wainscoting 同时覆盖 value 与 `null = None`。
- 所有 before/after assertions 使用 value snapshot；失败操作必须证明 input 与 confirmed data 都未改变。

### 4.2 Wall-mounted fixture

- 两个 `WallSurfaceLayout`，每面 `8 columns × 2 rows`，Slot 为 `1 m × 1 m`。
- Footprints：`1×1`、`2×1`、`1×2`、`2×2`、`3×2`；较大尺寸只需 test fixture，不要求正式 model。
- 已占用 Slot 同时包含 Wall Decor 与 Window，证明两者共享 occupancy。
- deterministic nearest-slot fixture 先优先 preferred Surface，再以 Manhattan distance → stable Surface ID ordinal → Column → Row 破 tie。
- cross-wall failure fixture 在 destination 设置 overlap / out-of-bounds，并记录 source/destination dictionaries 与 occupied-slot counts。

### 4.3 Surface and Catalogue fixture

- Categories 顺序：`Furniture`、`Floor`、`Wallpaper`、`Paint`、`Wainscoting`、`Wall Decor`、`Windows`。
- Paint：Cream、Sage、Terracotta；Wallpaper：Cream Floral、Sage Sprig；Wainscoting：Warm White + Rail、Sage Plain 与 `None`。
- Floor fixture：3 个 production kinds：Warm Wood、Light Tile、Dark Stone；正式视觉仍由 Studio Owner manual acceptance 决定。
- Furniture 保留 Phase 6 的 4 个 Catalogue presets。
- Wall-mounted fixture 使用 3 个 production Wall Decor 与 2 个 production Window entries；test-only `2×2/3×2` fixtures 只用于 legality coverage，不进入 production Catalogue。

### 4.4 Scene / input fixture

- production `Assets/Scenes/MainCafe.unity` 保持唯一 enabled production Scene；`Assets/Scenes/Validation/Phase7InteriorWalls.unity` 不进入 player Build Settings。
- canonical Phase 4 Floor、Back-left / Back-right Walls、Entrance 与 Phase 6 Furniture roots 不被替换；MainCafe 不预放 active Window，两个 Window definitions 仍保留在 Catalogue。
- Input fixture 使用 Unity Input System Touch device / virtual Touch，覆盖 tap、drag、press/move/release、pointer ID、UI-start gesture 与 Safe Area coordinates。
- responsive fixture 至少覆盖 reference Portrait、narrow Portrait、tall Portrait、Landscape fallback 与 safe-area insets；exact heights、card size 与 drag threshold 保持可调，但 Expanded / Compact transition 固定为 `0.16s`。

### 4.5 Evidence fixture

- EditMode / PlayMode XML 与 log 记录 exact counts。
- Scene tests 记录 loaded Scene 与 canonical object IDs；视觉 evidence 使用代表截图或短视频。
- Manual result sheet 对每个 `MT-*` 记录 `PASS / FAIL / BLOCKED`、Unity version、日期、观察与 Console 状态。

## 5. Automated EditMode cases — stable Surface data (`AT-*`)

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-001 | 2 个 unique Wall IDs、64 个 unique Floor cells | 构造 `RoomSurfaceLayout` 并读取 collections | Room ID、2 Walls、64 Floor tiles 完整；collections 对外只读 | Normal：基础数据缺失 | NUnit assertion + XML | READY |
| AT-002 | valid lowercase IDs | 对 Room、Wall、Style IDs 分别使用 empty、whitespace、uppercase/非法字符 | constructor / replacement 明确拒绝；原 snapshot 不变 | Invalid：未来 Save identity 不稳定 | parameterized NUnit + before/after snapshot | READY |
| AT-003 | 已有 2 Walls | 注入 duplicate Wall Surface ID、missing Wall 或第 3 个 unexpected Wall | 构造失败；不产生部分 Layout | Invalid：墙体 identity 冲突 | exception/result assertion | READY |
| AT-004 | 完整 `8 × 8` Floor | 注入 duplicate position、missing cell、`(-1,0)`、`(8,7)` | 全部拒绝；合法 64-cell source 不变 | Boundary：越界或稀疏 Floor | parameterized NUnit | READY |
| AT-005 | 两面墙分别有 Base 与 optional Wainscoting | 读取 `WallAppearance` | Paint/Wallpaper 只占一个 `BaseStyleId`；`null` 仅表示 No Wainscoting | Normal：layer 语义混淆 | value assertions | READY |
| AT-006 | 64 cells 含四种 rotation | 读取每格 appearance | style 与 rotation 按 position 保持；四次 90° cycle 回到 `Degrees0` contract | Boundary：rotation 丢失/越界 | parameterized NUnit | READY |
| AT-007 | confirmed Layout snapshot | `ReplaceWall` 修改一面墙 | 仅相同 stable Wall ID 的 appearance 改变；另一面墙和 Floors 不变 | Normal：跨墙污染 | dictionary diff | READY |
| AT-008 | confirmed Layout snapshot | `ReplaceFloor` 修改边角格 `(0,0)` 与 `(7,7)` | 仅目标 cell 改变；position、occupancy contract 不变 | Boundary：边缘索引错误 | dictionary diff | READY |
| AT-009 | 64 cells 混合 style/rotation | `ReplaceAllFloors(style, rotation)` | 恰好更新原有 64 keys；不新建、不丢失 position | Normal：Whole Room 不完整 | exact key/count diff | READY |
| AT-010 | 取得公开 Walls/FloorTiles views | 尝试 cast/mutate 或修改 source dictionaries | confirmed Layout 不可被外部 collection mutation 改写 | Recovery：绕过 transaction 污染 | immutability assertion | READY |

## 6. Automated EditMode cases — Wall-mounted layout and atomicity

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-011 | empty `8×2` wall | 分别 Validate/Place `1×1`、`2×1`、`1×2`、`2×2`、`3×2` | 完整 footprint 位于同一墙内时成功；occupied slots 等于面积 | Normal：通用 footprint 不完整 | parameterized NUnit | READY |
| AT-012 | empty wall | 在每个 footprint 上测试 left/right/top/bottom 边界与四角 | 完整落在 `8×2` 内才 valid；任何 cell 超界均失败 | Boundary：off-by-one | parameterized boundary matrix | READY |
| AT-013 | Wall Decor 已占 Slot | 将另一个 Wall Decor 部分/全部 overlap | 返回 overlap failure；两者 data/occupancy 不变 | Invalid：重叠 | result + snapshot | READY |
| AT-014 | Window 已占 Slot | Wall Decor overlap Window，反向再测 | 两方向都失败，证明共享 occupancy | Invalid：Window 特例绕过 | result + occupied slots | READY |
| AT-015 | 两个 registered surfaces | 使用 unknown Surface ID Validate/Place/Move | 返回 missing-surface failure；不创建 instance | Invalid：悬空 attachment | result + dictionary diff | READY |
| AT-016 | existing instance | same-wall move 到 valid Slot | Instance ID/definition 不变；source slots 释放、destination slots 占用 | Normal：移动 identity 变化 | before/after slot map | READY |
| AT-017 | existing instance | same-wall move 到 overlap/out-of-bounds | move 失败；source instance 与 source occupancy 完整保留 | Recovery：失败移动丢物件 | exact snapshot | READY |
| AT-018 | instance 位于 Back-left；Back-right 有 valid destination | cross-wall move | destination validation 成功后一次提交；同一 Instance ID 只存在于 Back-right | Normal：跨墙重复/丢失 | global lookup + both maps | READY |
| AT-019 | source valid；destination overlap | cross-wall move | destination 失败时 source/destination dictionaries、occupied counts 与 instance attachment 全不变 | Recovery：cross-wall rollback | byte/value-equivalent snapshots | READY |
| AT-020 | source valid；模拟 destination commit unexpected failure seam | cross-wall move | 两个 dictionaries 回滚到原值；无 duplicate/ghost occupancy | Recovery：atomic commit failure | fault-injection evidence | READY |
| AT-021 | 一个 footprint 靠近两墙 corner | 尝试用一个 instance 表达跨角 footprint | API 无 cross-corner representation；Confirm validation 失败 | Invalid：跨墙角 | construction/result assertion | READY |
| AT-022 | existing Wall Decor 或 Window | Remove/Store；重复 Remove | 首次移除并释放所有 Slots；第二次不重复修改；definition 仍可再次 Place | Recovery：Store 泄漏或库存化 | result + slot map + re-place | READY |

## 7. Automated EditMode cases — Catalogue and authoring contracts

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-023 | typed Surface and wall-mounted definitions | build presentation models | 7 categories 名称与顺序稳定；每行 items 只来自正确 kind | Normal：Category 混装 | NUnit model assertions | READY |
| AT-024 | 4 个 Phase 6 Furniture presets | bind Furniture category | 仍为 4 items；thumbnail + minimal name；presentation 不要求 footprint text | Regression：Phase 6 Catalogue 回退 | focused regression XML | READY |
| AT-025 | Surface definition matrix | 验证 normal entries | normal Surface 必须有 stable ID、correct Kind、Material、thumbnail；无 price/count/unlock fields | Invalid：内容合同不完整 | validator results | READY |
| AT-026 | Wainscoting `None` definition | 验证 None 与错误 kind None | 只有 Wainscoting 可 `IsNoneOption`；None 无 Material 但必须有 crossed-circle thumbnail/icon | Invalid：None 语义泄漏 | parameterized validator | READY |
| AT-027 | Surface Catalogue | 重复加入 ID、把 Paint 放 Wallpaper row 或 Floor 放 Wall row | deterministic validation failure；Catalogue 不产生部分 UI model | Invalid：stable ID/category 冲突 | exact issue codes/order | READY |
| AT-028 | Wall-mounted definitions | 测 missing Prefab/thumbnail、non-integer/invalid footprint、depth `>0.35m` | 全部拒绝；`<=0.35m` 合法；不添加 price/quantity/unlock | Boundary：visual depth/authoring 破约 | parameterized validator | READY |
| AT-029 | production Wall-mounted Catalogue set | bind rows and inspect authored Sprite contracts | `Wall Decor` 3 items、`Windows` 2 items；各有真实 prefab 的 mounted-angle transparent cutout + minimal name，不显示 footprint/count；imported `256×256`、transparent border、non-empty item、无墙/地面/黑底/checkerboard，builder 不重烘焙 | Normal：首批内容、缩略图语义或 UI 不符 | model count + hash + alpha-border/item-pixel assertions | READY |
| AT-030 | Texture/Material fixtures | 验证 Surface imports | Floor 为 `1m×1m` 二维 repeat；Wall 横向 `1m` repeat；Wrap Mode `Repeat`；Wainscoting 使用 project-approved shared waist reference，normalized cutoff 从 canonical wall 与该 reference 派生，texture 不能覆盖 | Boundary：接缝/比例错误 | asset validator + import assertions | READY |
| AT-031 | Scene/Prefab fixture | 扫描 Surface render objects 与 wall-mounted definitions | Surface 不新增 geometry Collider/occupancy/Nav obstacle；selection Collider 不在角色阻挡/Nav layer | Invalid：装修改变 gameplay legality | component/layer validator | READY |
| AT-032 | builder/setup target snapshots | 连续运行 builder/setup 两次 | 第二次为 zero asset/Scene diff；每个 Catalogue/Prefab/controller/registry/Surface root 唯一 | Recovery：重复执行污染 Scene | idempotency test + diff | READY |
| AT-033 | validation Scene/Build Settings | run validator | Validation Scene 不在 player Build Settings；`MainCafe` 仍为 production enabled Scene | Regression：测试 Scene 进入 build | validator report | READY |
| AT-034 | runtime and Editor assemblies | scan references | runtime source/assembly 无 `UnityEditor`；Editor setup/validator 留在 Editor boundary | Regression：player build failure | assembly/reference scan | READY |

## 8. Automated EditMode cases — Surface Preview transactions

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-035 | selected Wall、no active Preview | `BeginWall(surfaceId)`；在同一 transaction 依次 Select Wallpaper、Wainscoting、Paint | Preview 同时保存 Base + Wainscoting；后选 Paint 只替换 Base 的 Wallpaper；Decor/Window 与 confirmed Layout 不变 | Normal：multi-layer transaction 联动错误 | proposed-vs-confirmed diff | READY |
| AT-036 | target Wall 有 Wainscoting | 同一 transaction 更换 Base；再 Select another Wainscoting 和 None | Preview overlay 最终为 `null`；Preview Base 保留；confirmed Base/overlay/attachments 均未提前修改 | Normal：None 删除 Base 或拆散 transaction | snapshot assertions | READY |
| AT-037 | active Wall Preview | Select wrong-kind/unknown style | 返回 `WrongStyleKind/UnknownStyle`；Preview 与 confirmed Layout 不变 | Invalid：错误素材污染 Preview | result + snapshot | READY |
| AT-038 | Back-left 只被 selected、尚无 changes；随后产生 Wall change | 先选择 Back-right；再改 Back-right 后尝试选择 Back-left | 无 changes 时允许 retarget 并重抓 snapshot；有 changes 时返回 `ActivePreviewMustFinish` 且保留 active target/Preview | Invalid：target lock 过早或 silent target switch | result + target/snapshot assertion | READY |
| AT-039 | Wall snapshot 已捕获 | 依次选择不同 Base/Wainscoting，再把完整组合选回 confirmed values | `HasChanges` true→false；Confirm true 时 enabled、回到原组合后 disabled；Current check 始终读取 confirmed state | Recovery：no-op Confirm 或 indicator 漂移 | state sequence assertions | READY |
| AT-040 | active Wall Preview 同时修改 Base 与 Wainscoting | 分别执行 Cancel、successful Confirm 与 fault-injected Confirm | Cancel 一次恢复完整 snapshot；successful Confirm 一次原子提交两层；fault 时零 partial mutation并保留 Preview供重试/Cancel | Recovery：multi-layer partial commit | before/after + fault injection | READY |
| AT-041 | no active Preview | Begin Whole Room Floor；Select style/rotation | proposed snapshot 覆盖 64 cells；confirmed Layout byte/value-equivalent | Normal：Preview 提前提交 | exact confirmed snapshot | READY |
| AT-042 | Whole Room Preview | 检查 presentation state | 只有 Preview outline；不得显示绿色 Using check | Normal：Using 语义错误 | DTO/state assertion | READY |
| AT-043 | no active Preview | Begin Single Grid `(0,0)`；Select style；tap `(7,7)` | armed style/rotation 延续；两个 cells 加入同一 Preview；confirmed 不变 | Boundary：multi-grid transaction 漏格 | proposed diff + confirmed snapshot | READY |
| AT-044 | Single Grid target 已选 | 检查 card states | 目标 Grid 正式使用的 style 显示 Using check；preview style 显示 Preview outline；无目标时无 Using | Normal：状态指向错误 | state assertions | READY |
| AT-045 | Single Grid Preview with several changed cells | Rotate 后继续 tap 新格 | Rotate 只作用当前重新选中/之后铺设的 Grid；已加入 Preview 的其他格不追溯改变 | Boundary：rotation retroactive | per-cell rotation diff | READY |
| AT-046 | Single Grid armed style + rotation | 连续 tap；切换 style；再 tap | 当前 style/rotation 持续到主动更换；每次 tap 是独立 undo step | Normal：铺设状态丢失 | undo stack assertions | READY |
| AT-047 | active Floor Preview | 尝试切 `Whole Room / Single Grid` | 返回 `ActivePreviewMustFinish`；scope、proposed 与 confirmed 均不变 | Invalid：transaction 混 scope | result + snapshot | READY |
| AT-048 | Floor Preview 含 Surface change、rotation、Apply All | 逐次 `UndoLast` 到空 | 每次只撤最近 step；Apply All 整体一次撤销；Undo 不写 confirmed | Recovery：Undo 污染/顺序错 | snapshot sequence | READY |
| AT-049 | active Surface Preview | Cancel | 全部未确认 changes 消失；confirmed Layout 精确等于 before；active Preview 清空 | Recovery：Cancel 不完整 | before/after equivalence | READY |
| AT-050 | active valid Surface Preview | Confirm；再 Confirm/Undo | 首次一次性提交；active Preview 清空；重复 Confirm/无 Preview Undo 不重复修改 | Recovery：double commit | result + mutation count | READY |
| AT-051 | active Preview | 调用另一种 Begin 或另一 session begin gate | 系统一次只有一个 active Preview transaction；拒绝第二个且不丢第一个 | Invalid：并发 Preview | coordinator/session assertion | READY |
| AT-052 | Floor appearances 与 occupancy/collider/nav snapshots | Preview、Rotate、Confirm、Cancel 各执行一次 | rotation 只改 texture orientation；所有操作都不改 Grid position/occupancy/Collider/Nav | Regression：Surface 改变 gameplay | cross-domain snapshot | READY |

## 9. Automated EditMode cases — Wall-mounted Preview transactions

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-053 | empty compatible Slots | BeginNew without selecting Slot | Preview 出现在 viewport-center 附近最近 deterministic valid Slot | Normal：初始 Preview 不确定 | deterministic candidate assertion | READY |
| AT-054 | preferred Surface 及 fallback Walls 存在 equal Manhattan candidates | repeat BeginNew many times | priority 固定为 preferred Surface → Manhattan distance → Surface ID ordinal → Column → Row；结果不随 iteration order 变化 | Boundary：Camera corner nondeterminism或意外跳墙 | randomized-order repeat test | READY |
| AT-055 | new Preview | 检查 actions；Move valid；Confirm | 无 Store、无 Rotate；Confirm 后 exactly one confirmed instance | Normal：new lifecycle/action 错 | session state + mutation count | READY |
| AT-056 | existing Wall Decor/Window | BeginExisting；Move valid；Cancel | Store 可用、Rotate 不存在；Cancel 后 position/Surface/occupancy 精确恢复 | Recovery：existing move 无法回退 | before/after snapshot | READY |
| AT-057 | existing instance | Preview same-wall then cross-wall valid；Confirm | confirmed source 在 drag 中不被临时移除；Confirm 后 atomic move | Normal：Preview 暂时污染 Layout | mid-preview + final snapshots | READY |
| AT-058 | active Preview | pointer 经过 corner/no Surface/invalid Slot | Preview 为 Invalid；exact feedback key 为 `WallCrossCorner/WallSurfaceMissing/WallOutOfBounds`；Confirm disabled | Invalid：错误位置仍可提交 | parameterized result + action state | READY |
| AT-059 | destination occupied | MovePreview overlap | exact `WallOverlap`；source confirmed instance 保留；Confirm disabled | Invalid：overlap 提交 | result + source snapshot | READY |
| AT-060 | invalid Preview | 通过 direct handler/keyboard/programmatic path 请求 Confirm | `ConfirmPreview` 拒绝；无 Layout/occupancy mutation | Invalid：disabled UI 可被绕过 | direct-call assertion | READY |
| AT-061 | existing Preview | BeginStoreConfirmation；dismiss | blocking confirmation 出现；dismiss 后 instance/Preview/Slots 不变 | Recovery：误 Store | state snapshots | READY |
| AT-062 | existing Preview | ConfirmStore；重复确认 | 首次移除并释放 Slots；重复确认不再 mutation；Catalogue definition 仍可 BeginNew | Recovery：double Store/有限库存 | result + re-create | READY |
| AT-063 | new Preview | 尝试 Store | Store unavailable；无 confirmation、无 mutation | Invalid：new object 被 Store | state assertion | READY |
| AT-064 | Wall Decor 与 Window definitions | 对两种 item 运行 new/move/cancel/store/overlap/bounds matrix | 两者共享相同 placement lifecycle 与 legality；Window 不切 Wall geometry | Regression：Window 特例漂移 | parameterized session suite | READY |

## 9.1 Automated EditMode closure cases — identity, availability and thumbnail architecture

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| AT-065 | production `[Serializable]` `RoomSurfaceSnapshot` contract 与 valid `RoomSurfaceLayout`（RoomId、2 Walls、64 Floors） | 执行唯一确定路径：`RoomSurfaceLayout.CaptureSnapshot()` → serialize/deserialize `RoomSurfaceSnapshot` → `RoomSurfaceLayout.FromSnapshot(snapshot)`；重复执行并比较 ordered entries / serialized representation；另注入 duplicate/invalid Wall IDs、duplicate/missing/out-of-range Floor cells | round trip 后 RoomId、按 SurfaceId ordinal 排序的 WallAppearance entries、按 deterministic GridPosition order 排序的 exactly 64 FloorTileAppearance entries、styles/rotations value-equivalent；重复 capture 顺序稳定；invalid/duplicate snapshot 复用 normal production validation 并在无 partial Layout 的情况下拒绝；不创建 Phase 17 UI/storage | Recovery：Room Surface production snapshot 无法确定性重建 | ordered snapshot + serialized representation diff + NUnit | READY |
| AT-066 | production `[Serializable]` `WallMountedLayoutSnapshot` contract 与 valid `WallMountedLayout`（ordered Surfaces + mounted instances） | 执行唯一确定路径：`WallMountedLayout.CaptureSnapshot()` → serialize/deserialize `WallMountedLayoutSnapshot` → `WallMountedLayout.FromSnapshot(snapshot)`；比较 Surface/Instance ordered entries；另注入 duplicate Surface/Instance IDs、unknown/invalid attachment、out-of-bounds 与 overlap | round trip 保留 ordered Surfaces、InstanceId、DefinitionId、SurfaceId、Slot、Footprint，并从 attachments 重建相同 occupied Slots；顺序确定；`FromSnapshot` 重跑 production validation，任何 duplicate/invalid/overlap 整体拒绝且不暴露 partial Layout 和 partial occupancy；不创建 Phase 17 UI/storage | Recovery：Wall-mounted attachments/occupancy 重建不原子 | ordered snapshot + occupancy before/after + fault matrix + NUnit | READY |
| AT-067 | first production Catalogue asset fixture | 读取并验证首批 entries 与 availability state | exact counts：Paint `3`、Wallpaper `2`、Wainscoting `2 + None`、Floor `3`；全部可用且没有 disabled/unavailable/invalid state | Normal：首批内容数量或 availability 漂移 | exact count/order/state assertions | READY |
| AT-068 | 所有 Catalogue items 已绑定 thumbnail presentation | 检查 thumbnail asset type、tile binding 与 prefab/component graph | thumbnails 是预先生成的 `Sprite`；任何卡片不创建或持有独立 `Camera`、`RenderTexture` 或 runtime 3D thumbnail renderer | Regression：每卡实时渲染造成性能/架构回退 | asset/prefab component scan + NUnit | READY |

## 10. Integration / PlayMode cases — UI, input, Scene and lifecycle (`IT-*`)

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| IT-001 | fresh Decoration session | 从 HUD 进入 | 默认 `Furniture`；四 Tabs 可见；Game Time Pause；同 session 切 Tab 可记忆，退出重进仍默认 Furniture | Normal：entry/session state 错 | PlayMode assertion + screenshot | READY |
| IT-002 | 四 Tabs rendered | 检查 hierarchy/raycast target | Active Tab sibling/front layer 最高且略高；Inactive 不遮挡；每个 hit target 满足 approved minimum touch target | Visual：Tab 叠层不可用 | hierarchy bounds + screenshot | READY |
| IT-003 | 每个 Mode 无 active Preview | 依次切四 Tabs 并点击各类 Scene object | 只接受 spec mapping 的 hit kind；unsupported object 被忽略，不自动切 Mode | Invalid：cross-mode selection | Touch event log | READY |
| IT-004 | 各 Mode 有 active Preview | 点击 unsupported Scene object | active Preview、target 与 proposed state 均不变 | Recovery：误触取消编辑 | PlayMode state assertions | READY |
| IT-005 | active Preview | 点击另一个 Tab | 要求先 Confirm/Cancel；不自动保存、不 silent discard、不切 Mode | Invalid：跨 Mode transaction 丢失 | UI/session assertion | READY |
| IT-006 | Floor active Preview | 切 `Whole Room / Single Grid` | 明确阻止并保留 Preview；完成 Confirm/Cancel 后才允许切换 | Invalid：range 混 transaction | PlayMode assertion | READY |
| IT-007 | Wall target selected | 未修改时点另一面墙；产生 Preview 后再点另一面墙 | 未修改时 target/highlight 可切换；有修改时明确阻止，原墙与 Preview 保留 | Invalid：target selection 与 transaction lock 不一致 | target/highlight assertion | READY |
| IT-008 | no Preview / active Preview | drag Bottom Sheet 经过所有 snap states | 无 Preview 可到 `Expanded/Compact Preview/Tabs Only`；有 Preview 最低只到 Compact；Compact 保留 Tabs + fixed footer，隐藏 rows/cards，Confirm/Cancel 始终可见 | Boundary：关键 action 被藏 | state + visibility assertions | READY |
| IT-009 | Floor/Wall 与 Furniture/Wall Decor 分别创建 Preview | 观察初始 sheet state | Floor/Wall 保持 Expanded；Furniture/Wall Decor 自动 Compact | Normal：Scene 空间/动作不符 | state assertion | READY |
| IT-010 | 多 Category fixture | horizontal swipe item row、vertical swipe category list、diagonal gesture | horizontal row 与 vertical list direction-lock；不会同时大幅滚动或把 gesture 泄漏到 Scene | Invalid：nested scrolling 冲突 | ScrollRect delta + camera state | READY |
| IT-011 | Catalogue at scroll origin | inspect rows | 不显示 `Swipe →`/Scrollbar；horizontal 露出下一卡片，vertical 露出下一行；cards 近方形且 thumbnail/swatch 为主 | Visual：可发现性不足 | representative screenshots | READY |
| IT-012 | bind Furniture/Surface/Wall-mounted tiles | inspect visible content | Furniture/Wall-mounted 仅 minimal name、无 footprint/count；Surface image-only、无 Using/Preview/Available 文字 | Regression：卡片文案越界 | hierarchy/TMP assertions | READY |
| IT-013 | selected target 正式使用 A，Preview B | render Surface cards | A 显示中央 check；B 显示 outline；两者以 icon/shape 区分，不只靠 color | Visual：Using/Preview 混淆 | state assertions + grayscale screenshot | READY |
| IT-014 | Whole Room / Single Grid state combinations | bind Floor cards | Whole Room 从不显示 Using；Single Grid 仅在明确 target 时显示其 confirmed Using；Preview outline 仍独立 | Normal：Floor card grammar 错 | PlayMode matrix | READY |
| IT-015 | Wainscoting row | inspect None tile and select it | crossed-circle icon 始终存在；Using/Preview grammar 与普通 Surface 一致 | Visual：无法发现移除选项 | hierarchy + screenshot | READY |
| IT-016 | 切换五种 edit states | inspect actions、hierarchy、enabled state 与 presentation | Floor Sheet footer 第1行=`Whole Room/Single Grid`、第2行=`Undo/Rotate/Apply All/Cancel/Confirm`；Whole Room 时前三个 utility disabled/灰化，Single Grid 时 enabled；Surface Cancel/Confirm 加宽、单行且无 tooltip；Wall Sheet footer=`Cancel/Confirm` 且无 Apply All；Furniture使用跟随Preview的小圆`×/R/✓`，existing另有Store；new Wall-mounted为小圆`×/✓`、无Store/Rotate；existing另有Store、仍无Rotate | Invalid：错误 action 暴露或排版重叠 | exact active-button + hierarchy + presentation matrix | READY |
| IT-017 | Surface footer in Expanded/Compact | scroll catalogue、collapse/expand、resize viewport | footer 位于 Sheet hierarchy、不在 Scroll/overflow；与 Tabs 以 `0.16s` 同步移动且无 gap；Cancel/Confirm reachable；Pause-time UI 仍响应 | Boundary：无法完成/撤销或 footer 悬空 | bounds + animation + interaction assertions | READY |
| IT-018 | active Preview | request exit；观察 Modal；选择 Continue Editing | full-screen dim blocker + 独立暖色圆角 card；Continue/Discard 位于 card 内而非 Bottom Sheet/Catalogue controls；modal 仅两项；继续编辑后 Preview/Mode/target 保留；关闭 pointer 不泄漏到 Scene | Recovery：Modal 误操作或与 Catalogue 混在一起 | input trace + hierarchy/state | READY |
| IT-019 | active Preview | request exit；选择 Discard Changes | 不 Confirm；全部 pending changes 丢弃；退出一次；Pause/Camera/input state 恢复一次 | Recovery：exit 自动提交/重复恢复 | lifecycle counters + snapshots | READY |
| IT-020 | no active Preview | exit Decoration Mode | 直接退出；无 unnecessary discard modal；Pause/Camera/input 恢复 | Normal：退出被阻挡 | PlayMode state | READY |
| IT-021 | Floor Single Grid | tap Grid、选 Surface、连续 tap 多格、Undo/Cancel/Confirm，再 drag blank Scene | 当前 Grid highlight；所有 PreviewedGrids 边缘显示绿色勾；armed style 连续铺设；transaction 结束无 stale markers；drag 不 drag-to-paint，只由 Camera pan owner 处理 | Invalid：tap/drag ownership或Scene feedback错 | Touch trace + tile/marker diff | READY |
| IT-022 | Wall Mode | tap Back-left/Back-right、drag blank | tap 选择整面 wall segment，不按 Slot 涂；Camera angle/position不切正面墙视图 | Normal：墙选择单位错误 | target + camera transform | READY |
| IT-023 | Wall Decor Mode 无 Preview与new Preview | 先从空白Scene drag，再按住ghost跨两墙与corner gap drag | 无Preview时空白Scene drag由Camera pan owner处理；active ghost drag只移动物品且Camera transform不变；valid wall-to-wall snapping；corner/no slot期间Invalid；最终只能Confirm在同一Wall | Boundary：Camera/ghost ownership混淆或跨墙角提交 | Touch trace + Camera transform + projection + Layout | READY |
| IT-024 | gesture starts over UI | drag/release over Scene | UI owns complete gesture；Scene selection、Preview、Camera 均不响应 | Invalid：UI pointer leak | EventSystem/Touch trace | READY |
| IT-025 | two concurrent pointer IDs / interruption | press/move/release、disable owner、re-enable | pointer ownership 独立清理；无 stuck drag、ghost Preview commit 或 duplicate UI | Recovery：interruption state leak | pointer state assertions | READY |
| IT-026 | Surface Preview on Wall/Floor | render preview then Cancel/Confirm | Preview 只改变 appearance；Cancel restores confirmed visual；Confirm syncs once；source Materials 不被修改 | Recovery：Material/visual 状态污染 | MPB/material snapshots | READY |
| IT-027 | 64 Floor render tiles over canonical Floor | render mixed rotations | 恰好 64 render-only tiles；UV rotation per Grid；canonical Floor Collider/coordinate mapping 保持 authority | Regression：Floor gameplay geometry 被替换 | Scene component assertions | READY |
| IT-028 | Wall with Base + Wainscoting | render Paint/Wallpaper/None combinations and inspect renderer/material contract | Base 二选一；optional overlay 正确；Wallpaper vertical tiling `1`；Wainscoting 位于 shared waist 以下，normal 无额外 procedural crosshatch，bump轻微且 overlay 不单独投出 fence-like shadow | Boundary：layer/tiling/normal/shadow错 | shader/material/renderer assertions + screenshot | READY |
| IT-029 | valid Wall-mounted Preview，参数化五个 production prefabs | render ghost and projection | 真实 prefab renderer 可见；ghost 垂直地面、正面平行 target Wall、沿 wall normal 略微悬浮；仅 target Wall 显示 exact footprint 绿色 projection + `✓`；Confirm enabled；无 z-fighting | Normal：ghost 平躺或 valid feedback 不清楚 | transform/bounds assertions + screenshot | READY |
| IT-030 | overlap/out-of-bounds/cross-corner/missing Wall matrix | render projection | 红色 projection + `×`；对应具体 reason；Confirm disabled；不只依赖色相 | Invalid：错误反馈/可提交 | matrix + grayscale screenshot | READY |
| IT-031 | rotated target Wall 被真实 Furniture/Wall Decor 挡住；或 Floor Mode 无 Preview | select/edit Wall target；进入 Floor Mode | Wall rule：以真实 Wall plane（非 world AABB near face）为 ray distance，只淡化 camera-to-target blockers；target/highlight保持正常；Floor rule：所有 confirmed Furniture 立即约 `35%` 淡化，Wall/Wall Decor/Window 保持正常；两者都不改变 data/occupancy/collider/input boundary | Visual：目标/Floor不可读或淡化改逻辑 | rotated-plane raycast + object/material snapshots + screenshot | READY |
| IT-032 | Wall/Floor fade 已应用 | 切 target、切出对应 Mode、Cancel、Discard、exit、disable、fault seam；Continue Editing 留在当前 Mode | cleanup path 精确恢复原 Materials、MaterialPropertyBlocks 与 opacity；Continue Editing 保持对应 fade；无 permanent fade | Recovery：视觉状态泄漏 | parameterized cleanup assertions | READY |
| IT-033 | canonical MainCafe migrated | load Scene、enter/use/exit mixed session | exact two Wall IDs、64 Floor tiles、wall registries、four Tabs 存在；Entrance 与 Phase 6 Furniture preserved；MainCafe 无 active Window seed，但两个 Window Catalogue entries 仍可用 | Regression：production integration 破坏 | actual Scene-loading test XML | READY |
| IT-034 | Validation Scene and player build | load standalone-compatible runtime | runtime assembly/Scene load 不依赖 UnityEditor；Validation Scene 未进入 production Build Settings | Regression：player build失败 | player/Scene smoke log | READY |
| IT-035 | responsive fixture 含 reference/narrow/tall Portrait、Landscape 与 safe-area insets | 对每个尺寸依次：render 四 Tabs；横/竖 nested scroll；进入 Compact Preview；记录 `0.16s` transition；定位并 raycast Surface footer Cancel/Confirm | 每个尺寸下 Tabs 完整且 Active 在前；nested scroll direction-lock；Compact footer 不裁切且跟随 Sheet；Cancel/Confirm bounds 位于 safe containment 内、可 raycast、无遮挡且不被 safe-area 截断 | Boundary：responsive 下关键操作不可达 | parameterized PlayMode bounds/raycast/timing matrix + XML | READY |

## 11. Regression cases (`RT-*`)

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| RT-001 | Phase 1 Layout suites available | 跑完整 `CafeLayout` data tests | exact counts；failed/skipped/inconclusive 均为 0 或有单独 approved limitation | Regression：Layout 基础回退 | XML/log | READY |
| RT-002 | Phase 2 placement suites available | 跑 occupancy/bounds/rotation/Entrance/transaction tests | 旧 Floor Furniture legality 与 transaction semantics 不变 | Regression：placement 回退 | XML/log | READY |
| RT-003 | Phase 4 Wall suites/validators available | 跑 `WallSurfaceLayoutTests` 与 production asset validators | Phase 4 Wall API/result ordering、canonical Walls/Entrance 与 Window definition contracts 不变；不要求 MainCafe 预放 active Window | Regression：Wall contract 回退 | XML/log | READY |
| RT-004 | Phase 5 UI suites available | 跑 Theme/navigation/Modal/Bottom Sheet/Safe Area/Pause/pointer boundary tests | Phase 5 accessibility与 input boundary 继续通过 | Regression：UI foundation 回退 | XML/log | READY |
| RT-005 | Phase 6 EditMode suites available | 跑 Catalogue、`DecorationSessionTests`、Layout/Scene sync focused regressions | 4 Furniture presets、Rotate、Confirm/Cancel/Store 与 runtime Layout 不变 | Regression：Furniture flow 回退 | XML/log | READY |
| RT-006 | Phase 6 PlayMode Touch/UI suites available | 跑完整 Phase 6 Decoration Touch/UI/Scene tests | Furniture drag、Camera ownership、UI pointer blocking 与 Pause lifecycle 不变 | Regression：input ownership 回退 | XML/log | READY |
| RT-007 | production MainCafe | load、enter Phase 7、只用 Furniture flow、exit/resume | initial Furniture/Entrance preserved；MainCafe 无预放 active Window，Catalogue 仍提供两个 Window definitions；无 duplicate runtime/UI；Game Time 正常恢复 | Regression：MainCafe smoke | Scene test + Console log | READY |
| RT-008 | Phase 7 tasks focused GREEN | 跑 fresh full EditMode once at Phase closeout | XML 逐项记录 passed/failed/skipped/inconclusive；不可用 focused pass 替代 | Regression：隐藏跨系统失败 | full EditMode XML/log | READY |
| RT-009 | Phase 7 tasks focused GREEN | 跑 fresh full Editor PlayMode once at Phase closeout | exact counts；order/input failures 不得隐藏 | Regression：runtime integration失败 | full PlayMode XML/log | READY |
| RT-010 | full run imported/generated assets or exposed order issue | 修复后 rerun affected focused tests；最终再跑 full suite once | 报告首次原因、修复与最终 counts；不擦除首次失败历史 | Recovery：假绿/顺序依赖 | initial + final XML/log | READY |
| RT-011 | Standalone-compatible build path | run player assembly/MainCafe smoke | 无 UnityEditor dependency；Scene loads；basic enter/exit and input mapping usable | Regression：Editor-only 通过 | standalone log | READY |
| RT-012 | final mixed session | inspect Unity Console after exit/resume | 无 unexpected Error/Exception/unexplained Warning；无 duplicate UI/Grid/registry | Regression：清理泄漏 | Console export/screenshot | READY |

## 12. Studio Owner Manual Play Mode cases (`MT-*`)

Manual cases 默认使用 production `MainCafe`。每一行都按“前置条件 → 操作”执行；只要任一 Expected Result 不满足就记 `FAIL`，不要自行放宽。`READY` case 可使用 clearly labelled placeholder 做技术/可用性验收；`WAIT-ASSET` case 必须等正式素材后重跑视觉验收。

| ID | 前置条件 | 操作 | 预期结果 | 风险 | Evidence | Asset dependency |
|---|---|---|---|---|---|---|
| MT-001 | 打开 MainCafe，Console 清空 | 1) Play；2) 从 HUD 进入 Decoration Mode；3) 依次点四 Tabs | PASS：默认 Furniture、四 Tabs 都能点、Game Time Pause、无报错；否则 FAIL | Normal：入口不可用 | result sheet + screenshot | READY |
| MT-002 | Decoration Mode 已打开 | 1) 正面观察 Tabs 轮廓；2) 比较 Active/Inactive 高度、前后层与阴影；3) 点每个 Tab 的中心、边缘；4) 收起 Sheet 后再点一次 | PASS：Tabs 明确是从 Bottom Sheet 左上边缘伸出的 folder-tab shape，不是 pill segmented control；Inactive 较低/靠后并有克制阴影；Active 完整盖在相邻 Tabs 前方且不被任何 neighbor 遮挡；收起后四 Tabs 始终可见，实际 touch target 仍容易点击。任一条件不满足即 FAIL | Visual：folder tabs 形状、层级或触控差 | front/angled screenshots + touch observation | READY |
| MT-003 | 每个 Mode 依次 active | 1) 在 Furniture 点墙/地面；2) Floor 点墙饰/家具；3) Wall 点家具；4) Wall Decor 点地面/家具 | PASS：不支持对象不切 Mode、不打断 Preview；否则 FAIL | Invalid：误选跨 Mode 对象 | result sheet | READY |
| MT-004 | 无 Preview，然后创建 Surface Preview | 1) 拖 Sheet 到 Expanded/Compact/Tabs Only；2) Preview active 时再向下拖；3) 观察 tabs/footer transition | PASS：无 Preview 只有固定三档；有 Preview 不能低于 Compact；Compact 只保留 tabs+footer；两者 `0.16s` 同步移动、无 gap且 Confirm/Cancel 始终可见；否则 FAIL | Boundary：关键 action 隐藏或 footer 悬空 | short video | READY |
| MT-005 | 多 Category 内容已绑定 | 1) 横滑同一 row；2) 竖滑 rows；3) 斜滑；4) 从 UI 滑到 Scene | PASS：方向清楚、不会两轴乱跑、不会带动 Camera/Scene；否则 FAIL | Invalid：nested scrolling/input leak | short video | READY |
| MT-006 | Surface target 有 Using A，选择 Preview B | 1) 正常观察；2) 截灰度图；3) 查看 Wainscoting None | PASS：check、outline、crossed-circle 即使不靠颜色也能分辨，卡片无状态文字；否则 FAIL | Visual：状态不易懂/色觉依赖 | color + grayscale screenshots | READY |
| MT-007 | Floor Tab fresh session | 1) 检查footer两行；2) 确认默认Whole Room；3) 选Surface；4) Confirm；5) 切Tab后返回 | PASS：第1行仅Whole Room/Single Grid；第2行五个actions无重叠；Whole Room时Undo/Rotate/Apply All灰化disabled；整房64格一起变化、无Using check、同session记住range；否则FAIL | Normal：Whole Room flow或footer排版错 | video + result sheet | READY |
| MT-008 | Floor `Single Grid` | 1) 切Single Grid并确认三个utility enabled；2) 先点一格；3) 观察highlight；4) 选Surface；5) 连续点几格并换一次花纹；6) Cancel后重做并Confirm | PASS：Undo/Rotate/Apply All可用；当前Grid highlight；每个Previewed Grid边缘有绿色勾；armed style持续且可换花纹；Cancel/Confirm后Scene markers全清；否则FAIL | Normal：逐格铺设/feedback/transaction/footer state错 | video | READY |
| MT-009 | Single Grid Preview，至少三格 | 1) Rotate；2) 再铺一格；3) 重选旧格再 Rotate；4) Undo Last | PASS：90° 步进只影响当前/后续，不追溯其他格；Undo 只撤最近 step；否则 FAIL | Boundary：rotation/undo 错 | video + before/after screenshots | READY |
| MT-010 | Floor Preview 含多格与 Apply All | 1) 记录正式地面；2) Apply All；3) Undo；4) Cancel | PASS：Apply All 一次 Undo 全撤；Cancel 恢复本 transaction 前状态；否则 FAIL | Recovery：大范围修改不可恢复 | screenshots + result sheet | READY |
| MT-011 | Floor Preview active | 1) 尝试切 range；2) 尝试切 Tab；3) 分别用 Confirm/Cancel 完成后重试 | PASS：active 时必须先完成；无 silent save/discard；完成后可切换；否则 FAIL | Invalid：transaction gate 失效 | video | READY |
| MT-012 | Wall Tab，无 Preview | 1) 点 Back-left；2) 观察 footer；3) 未修改时点 Back-right；4) 选择 Base 后再尝试切墙 | PASS：选墙立即显示 Sheet内 Cancel/disabled Confirm；无修改可换墙；修改后锁定 target且无 Apply All；否则 FAIL | Normal：Wall target/footer contract错 | screenshots + video | READY |
| MT-013 | 一面墙已有 Base + Wainscoting | 1) 在同一 Preview 更换 Base；2) 换 Wainscoting；3) 选 None；4) Cancel；5) 重做并 Confirm | PASS：一个 transaction 可自由组合 layers；None 只移除 Wainscoting；Cancel整墙恢复；Confirm整墙一次提交；Decor/Window 保留；否则 FAIL | Normal：multi-layer rollback破坏内容 | before/after screenshots | READY |
| MT-014 | 一面墙已有 confirmed Base/overlay | 1) 改两个 layers；2) 逐项选回原组合；3) 再改两个 layers并 Confirm | PASS：改动时 Confirm enabled，完整回原组合后 disabled；Wall 无 Apply All；最终 Confirm 后两个 Current checks 一起更新；否则 FAIL | Recovery：HasChanges/indicator/atomic commit错 | screenshot matrix | READY |
| MT-015 | Wall selected/Preview states | 1) 未修改时点另一 Wall；2) 产生 Preview后再点另一 Wall；3) 点unsupported object；4) Cancel | PASS：未修改可换 target；修改后另一 Wall 不接管；unsupported 无影响；Cancel 精确恢复并清理highlight/outline；否则 FAIL | Recovery：target/Preview丢失 | video | READY |
| MT-016 | Wall/Floor production content 已可见 | 1) 检查相同 rotation 连续 Floor；2) 检查 Wallpaper 横向 repeat；3) 近看 Wainscoting 顶/底、板缝和阴影 | PASS：无 mapping 跳变、异常宽边或错误纵向 repeat；Wainscoting 无 diagonal crosshatch、位于下半墙且不投出围栏式独立阴影；否则 FAIL | Boundary：texture/normal/shadow contract错 | close-up screenshots | READY |
| MT-017 | production Wall Decor Catalogue 可用 | 1) 不选item，在空白Scene drag；2) 依次选择五个entries；3) 观察ghost姿态；4) 按住ghost drag；5) Confirm一件并立即点它 | PASS：空白drag只pan Camera；ghost drag只移动物品且Camera不动；真实prefab ghost垂直地面、平行墙面、略微悬浮并对齐footprint；小圆`×/✓`跟随ghost；无Rotate/Store；Confirm后exactly one instance、Sheet保持Compact且下一tap立刻可重新选中；否则FAIL | Normal：Camera/ghost ownership、姿态或Confirm后重选错 | video | READY |
| MT-018 | `MainCafe` Play Mode；production Wood Shelf `2×1` 与 Window `1×2` 可用；AT-011/AT-012 已验证large fixtures | 1) 保持MainCafe并进入Wall Decor；2) 用Wood Shelf在两墙内部与水平边界放置，再选择会使`2×1`越界的边缘Slot；3) 用Window在内部与垂直边界放置，再选择会使`1×2`越界的Slot；4) 慢过corner并重复另一面墙 | PASS：完整footprint在`8×2`内时valid；任一Slot越界即红叉且Confirm disabled；manual不依赖test-only Catalogue，`2×2/3×2`精确合法性继续由AT-011/AT-012判定；否则FAIL | Boundary：production footprint越界或manual入口不可操作 | MainCafe screenshot + short video + AT-011/AT-012 result | READY |
| MT-019 | Wall 上已有 Decor 与 Window | 1) 用另一件分别 overlap；2) 观察 projection/reason | PASS：两种 occupancy 都阻挡；红色 `×`、具体 overlap reason、Confirm disabled；否则 FAIL | Invalid：共享 occupancy 失效 | screenshot + result sheet | READY |
| MT-020 | active Wall Decor/Window Preview | 1) 从 Back-left 拖到 Back-right；2) 慢慢经过 corner；3) 在有效 Slot Confirm | PASS：两墙间可移动；corner 期间 Invalid/具体 reason；最终只占同一墙；否则 FAIL | Boundary：cross-wall/corner错 | short video | READY |
| MT-021 | existing Wall Decor/Window | 1) 开始编辑并移动；2) Cancel；3) 再移动 Confirm | PASS：Cancel 精确回原 Slot；Confirm 后旧 Slots 释放、新 Slots 占用；无 Rotate；否则 FAIL | Recovery：move rollback错 | before/after screenshots | READY |
| MT-022 | existing item | 1) 点 Store；2) 先 dismiss；3) 再确认；4) 从 Catalogue 再放同款 | PASS：blocking confirmation；dismiss 不变；confirm 移除并释放 Slots；同款仍可无限再放；否则 FAIL | Recovery：误删/库存化 | video | READY |
| MT-023 | valid/invalid Wall Preview，且目标格跨过 Wainscoting/rail/baseboard | 1) 分别制造 valid、overlap、out-of-bounds、cross-corner；2) 在上下不同 Slot 比较 projection 明暗；3) 暂时以灰度观察 | PASS：projection 始终位于最外层墙饰之前，green/red不忽深忽浅；green+✓ 与 red+×+具体文字都清楚；ghost仍贴Base Wall而不随projection悬浮；invalid永远不能Confirm；否则FAIL | Visual：projection被墙饰遮挡、色觉依赖或模型悬浮 | screenshots | READY |
| MT-024 | target Wall 被场景物件遮挡 | 1) 选择/拖 Wall target；2) 切另一 target；3) 退出 Mode | PASS（视觉）：只看到 camera-to-target blockers 临时 fade，target 仍清楚；切换/退出后外观完全恢复。data、occupancy、Collider 与 input boundary 由 IT-031/IT-032 自动化判定；否则 FAIL | Recovery：fade cleanup/视觉污染 | before/during/after screenshots | READY |
| MT-025 | 任意 active Surface/Wall-mounted Preview | 1) 点退出并观察 Modal；2) 选 Continue Editing；3) 再退出选 Discard Changes | PASS：Modal 使用 full-screen dim blocker + 独立暖色圆角 card，两个按钮位于 card 内且不再像 Catalogue controls；第一次保留全部编辑；第二次不 Confirm 并退出；再次进入无 pending change；否则 FAIL | Recovery：exit discard错或Modal层级混淆 | video | READY |
| MT-026 | reference/narrow/tall Portrait 与 Landscape/safe-area presets | 1) 每种分辨率切四 Tabs；2) scroll；3) Compact Preview；4) 操作 Confirm/Cancel | PASS：无 clipping/overlap/unreachable action；Active Tab 在前；safe area 不造成误触；否则 FAIL | Boundary：responsive不可用 | resolution screenshot set | READY |
| MT-027 | fresh Play session | 1) 混合执行 Furniture、Floor、Wall、Wall Decor 的 Confirm/Cancel/Store；2) 退出再进入两次；3) resume time | PASS：无 duplicate UI/Grid/registry、无 stuck Pause/input/fade，已 Confirm runtime changes 保留；否则 FAIL | Recovery：重复 session 泄漏 | video + Console screenshot | READY |
| MT-028 | 不阅读 implementation notes 的初学者测试者 | 1) 尝试浏览 Category；2) 完成四种 Mode 的一次 change；3) 说明 Using/Preview/Invalid/Store 含义 | PASS：能独立找到关键 action并正确解释状态；任何关键流程需猜测或卡住则 FAIL并记录位置 | Visual：beginner comprehension不足 | observation notes | READY |
| MT-029 | 正式 3 种 Floor textures/Materials/thumbnails 全部集成 | 1) 进入 Floor Mode但先不选地砖，观察全部 Furniture；2) 在 MainCafe 全铺和混铺；3) 检查 0/90/180/270；4) 检查近景/远景 seams、scale、方向与 swatch；5) Cancel/切 Tab/退出后观察 Furniture | PASS 由 Studio Owner：Floor Mode 内所有 confirmed Furniture 约 `35%` 淡化，Floor/Grid清楚，Wall/Wall Decor/Window不淡化；离开后外观精确恢复；三种正式 Floor 素材 seamless、比例、方向与 swatch 可接受；自动化结果不得代替；否则 FAIL/Revise | Visual：Floor readability/fade或正式素材未验收 | before/during/after + formal screenshots + Owner decision | READY |
| MT-030 | 正式 Paint、Wallpaper、Wainscoting、None swatches/Materials 已集成到 MainCafe | 1) 分别在 Back-left/Back-right 预览并 Confirm 每种 Paint 与 Wallpaper；2) 沿整面墙检查 horizontal seams；3) 确认 Wallpaper 覆盖 full wall height 且不 vertical repeat；4) 逐一检查 Wainscoting 的 rail 只在顶部、baseboard 只在底部、整体 scale、normal 与 shadow；5) 选 None；6) 比较 swatches | PASS 由 Studio Owner：正式 Wall Surfaces 无明显横向接缝，Wallpaper 满墙高且不纵向重复，Wainscoting rail/baseboard、scale、无crosshatch和非围栏式阴影可接受，None清楚，swatches可读。否则 FAIL/Revise | Visual：正式 Wall Surface 外观未验收 | formal wall close-ups + swatch screenshots + Owner decision | READY |
| MT-031 | 正式 3 个 Wall Decor + 2 个 Window Prefab/thumbnails 已集成 | 1) 检查五张thumbnail只有物品、无墙/地面/黑底/checkerboard且保留Back-left mounted `3/4`视角；2) 分别放置/移动/Store Painting 1x2、Wood Shelf 2x1、Monitor 1x1、Window 1x1、Window 1x2；3) 检查footprint、pivot、visual depth与角色/墙体关系 | PASS 由 Studio Owner：五张 transparent cutout 构图和五个正式模型外观/深度可接受；placeholder不算；技术 alpha/hash/bounds/depth 仍以 AT-029/AT-028/AT-031 为权威。否则 FAIL/Revise | Visual：正式 model 或 transparent thumbnail 未验收 | side-by-side stills + Owner decision | READY |
| MT-032 | MainCafe production content 可用 | 1) 选择被不同 blocker 遮挡的 Wall；2) 调整/观察 fade；3) 在四 Tabs 与三个 snap states 间切换；4) 观察 Bottom Sheet height/card/drag与固定 `0.16s` transition；5) 在四 responsive presets 重复 | PASS 由 Studio Owner：fade可读且恢复自然；UI尺寸舒适；Tabs/Surface footer无gap并同步移动；`0.16s`不丢transaction。opacity/size仍可调。否则 Revise | Visual：MainCafe fade/UI tuning未定 | tuning record + video | READY |
| MT-033 | 正式 Floor、Wall Surface 与 Wall-mounted assets 已全部集成 | 1) 重跑受正式 framing 影响的 MT-023/024/026/032；2) 检查模型/Surface是否遮住projection、tabs、Surface footer或target；3) 记录视觉 tuning revision | PASS 由 Studio Owner：正式素材下 projection、fade、Tabs、Compact footer 与 target readability 可接受；否则 FAIL/Revise | Visual：正式内容改变构图 | post-integration screenshots/video + Owner decision | READY |
| MT-034 | 完成全部适用 manual cases | 1) resume Game Time；2) 查看 Console；3) 检查 Window/Entrance/Phase 6 Furniture | PASS：无 unexpected Error/Exception/unexplained Warning；旧内容与 gameplay 可用；否则 FAIL | Regression：最终 smoke失败 | result sheet + Console export | READY |

## 13. Asset dependency gate

| Gate | 可接受输入 | 可关闭的测试 | 不可据此关闭的测试 | Gate decision |
|---|---|---|---|---|
| `G-A0 Pure fixture` | deterministic data、test Materials/Sprites、`2×2/3×2` test-only Wall Decor | 所有 pure `AT-*` 与不判断美术品质的 UI/transaction cases | 任何正式素材视觉 acceptance | Tasks 1–8 可按 TDD 推进 |
| `G-A1 Labelled placeholder integration` | 3 个 clearly labelled Wall Decor placeholders、canonical Phase 4 Window、deterministic Surface placeholders | `READY` 的 Scene、input、projection、Store、responsive 与 MT-032 MainCafe fade/UI tuning | MT-029、MT-030、MT-031、MT-033 | Task 9 技术 integration 可推进；不得称正式 Art complete |
| `G-A2 Formal Surface set` | 3 Floor textures/Materials、全部 Paint/Wallpaper/Wainscoting/None swatches/thumbnails、projection materials、Wall Slot display | MT-029、MT-030 及 MT-033 的 formal Surface recheck | Phase completion（若其他 gates 未通过） | 素材已接入；等待 Studio Owner visual decision |
| `G-A3 Formal Wall-mounted set` | 1×1、2×1、1×2 Wall Decor 与正式 1×1、1×2 Window Prefabs/Definitions/thumbnails | MT-031 及 MT-033 的 formal model recheck | Phase completion（若其他 gates 未通过） | 五个正式模型已接入；等待 Studio Owner visual decision |
| `G-A4 Phase acceptance` | automated/integration/regression fresh evidence + 全部 applicable manual PASS + accepted tuning | Phase 7 completion recommendation | merge/cleanup（仍需独立授权） | 仅 Studio Owner 可决定 Completed |

任何 `WAIT-ASSET` case 在依赖未满足时记录 `BLOCKED (WAIT-ASSET)`，不是 `FAIL`，也不是 `PASS`。若 placeholder 暴露技术缺陷，仍按正常 `FAIL` 处理并修复。

## 14. Execution gates

1. **Amendment freeze gate（现在）**：Studio Owner 已批准 2026-08-27 design；本 test update 与 implementation plan 必须再次 review 后才开始 Task 12。
2. **Task 12 gate**：先为改写后的 `AT-035–AT-040` 建立可信 RED，再修改 production Wall transaction；不得用现有 GREEN 代替新规则 RED。
3. **Per-Task TDD gate**：每个 implementation Task 映射相关 `AT/IT`；记录 focused RED → GREEN 与 direct regressions。未可信 RED 不修改 production 来迎合 fixture。
4. **Asset integration gate**：Tasks 1–9 可使用明确 placeholder 完成技术合同；Task 10 与正式 visual acceptance 必须等待 `G-A2/G-A3`。
5. **Phase closeout gate**：按 `Docs/AnimalCafe_Phase_Development_Process.md` 只在 Phase 收尾集中跑 fresh full EditMode、PlayMode、真实 Scene/Input、Engineering/QA review 与 manual acceptance。
6. **Completion gate**：automated/Mock/placeholder 全绿仍不是 Phase completion；只有 Studio Owner 明确 manual accept，才可更新 Roadmap 为 Completed。
7. **Version-control gate**：本矩阵不授权 commit、push、merge、切 branch、删除 branch/worktree；这些需要当前明确授权。

## 15. Spec-to-test traceability

| Approved requirement | Primary cases | Coverage |
|---|---|---|
| Goal / scope exclusions | AT-025、AT-028、AT-031、AT-034、AT-064、AT-066、IT-016、RT-011 | 无 price/inventory/unlock/real opening/Phase 17 Save UI；runtime boundary；Wall-mounted 无 Rotate、new 无 Store |
| Four Mode Tabs / hit isolation | IT-001–IT-007、IT-021–IT-025、MT-001–MT-003 | normal、invalid、gesture recovery |
| Catalogue rows / nested scrolling | AT-023–AT-029、AT-067–AT-068、IT-010–IT-015、IT-035、MT-004–MT-006 | exact initial counts/availability、pre-generated Sprite、categories、cards、Using/Preview/None、scroll ownership |
| Preview / Tab switch / exit discard | AT-049–AT-051、IT-005–IT-007、IT-018–IT-020、MT-011、MT-025 | one transaction、Confirm/Cancel、recovery |
| Bottom Sheet / Surface footer | IT-008–IT-009、IT-016–IT-017、IT-035、MT-004、MT-026、MT-032 | three snap states、Sheet内fixed footer、`0.16s`、responsive bounds/raycast、Owner tuning |
| Exact actions / action exclusions | AT-055–AT-056、AT-060、AT-063–AT-064、IT-016 | Floor/Furniture/new-existing Wall-mounted matrices；Wall只有Cancel/Confirm且无Apply All；new无Store；Wall-mounted/Window无Rotate；invalid Confirm disabled |
| Wall multi-layer transaction | AT-035–AT-040、IT-007、IT-028、MT-012–MT-015 | one target snapshot、Base/overlay/None、HasChanges、atomic Confirm/Cancel、target lock、no Apply All |
| Floor Whole / Single / rotation | AT-004、AT-006、AT-009、AT-041–AT-052、IT-021、IT-027、MT-007–MT-011 | 8×8、64 cells、tap only、selected highlight、Previewed Grid checks、armed style、rotation、Undo/Cancel |
| Texture authoring core | AT-030–AT-031、IT-027–IT-028、MT-016、MT-029–MT-030 | 1m repeat、shared waist reference/canonical-wall derived cutoff、no gameplay geometry、formal Floor/Wall seams |
| Wall Slots / footprints / occupancy | AT-011–AT-022、MT-018–MT-022 | 8×2、all fixture sizes、bounds、overlap、Store |
| Wall Decor / Window lifecycle | AT-053–AT-064、IT-023、MT-017–MT-022 | new/existing/cross-wall/no Rotate/Store |
| Placement feedback / projection | AT-058–AT-060、IT-029–IT-030、MT-019、MT-023 | valid/invalid、icon+text、Confirm gate |
| Wall highlight / fade cleanup | IT-031–IT-032、MT-024、MT-032–MT-033 | blocker-only fade、all cleanup paths、READY Owner tuning、formal-content recheck |
| Stable IDs / production snapshot boundary | AT-001–AT-010、AT-032–AT-034、AT-065–AT-066、IT-033–IT-034 | AT-065：RoomId + ordered Walls + ordered 64 Floors 经 `RoomSurfaceLayout.FromSnapshot` 重建；AT-066：ordered Surfaces + Instance attachments 经 `WallMountedLayout.FromSnapshot` 原子重建 occupancy；deterministic round trip、无 Phase 17 UI/storage |
| Responsive / Safe Area | IT-002、IT-008–IT-010、IT-017、IT-035、MT-002、MT-004–MT-005、MT-026、MT-032 | tabs、nested scroll、Compact Surface footer、`0.16s`、Confirm/Cancel bounds/raycast/safe containment |
| MainCafe / prior Phase regression | RT-001–RT-012、IT-033–IT-035、MT-027、MT-034 | Phase 1/2/4/5/6、production Scene、responsive、Console |
| Formal asset visual acceptance | MT-029–MT-031、MT-033、Asset gates G-A2/G-A3 | Floor、Wall Surface、Wall-mounted 与 post-integration review；technical placeholder 与 Studio Owner visual decision 分离 |

### 15.1 Implementation-plan Task mapping

| Plan Task | RED / focused primary cases | Direct regression / later gate |
|---|---|---|
| Task 1 — Stable Room Surface data | AT-001–AT-010、AT-065 | RT-001、RT-003 |
| Task 2 — Atomic Wall-mounted layout | AT-011–AT-022、AT-066 | RT-002、RT-003 |
| Task 3 — Typed Catalogues | AT-023–AT-029 pure contract；AT-067 typed-count model；AT-068 Sprite-only DTO contract | RT-004、RT-005；production asset/Prefab evidence remains Task 9 |
| Task 4 — Surface Preview transaction | AT-035–AT-050；AT-051 same-session gate；AT-052 pure appearance/no-cross-domain-reference portion | AT-001–AT-010、RT-001；global/Scene evidence closes later |
| Task 5 — Wall-mounted Preview transaction | AT-053–AT-064 | AT-011–AT-022、RT-005 |
| Task 6 — Multi-Mode UI | IT-001–IT-020、IT-035 | RT-004、RT-006、MT-001–MT-006、MT-026 |
| Task 7 — Rendering / projection / fade | IT-026–IT-032；AT-052 Scene component/Collider/Nav evidence | RT-003、RT-006、MT-016、MT-023–MT-024 |
| Task 8 — Mode routing / input integration | IT-003–IT-007、IT-018–IT-025；AT-051 global cross-session/coordinator gate | RT-006、RT-007、MT-003、MT-011、MT-020、MT-025 |
| Task 9 — Assets / migration / validators | AT-030–AT-034；AT-067 production IDs/order/availability；AT-068 prefab/component graph；IT-033–IT-035 | RT-003–RT-007、canonical Window migration、G-A1 |
| Task 10 — Formal model intake | AT-028–AT-031、AT-064 | MT-031、MT-033、G-A3 |
| Task 11 — Baseline MainCafe / responsive closeout | IT-033–IT-035、RT-008–RT-012 | superseded manual gate continues in Tasks 12–17 |
| Task 12 — Wall multi-layer transaction amendment | AT-035–AT-040、IT-007 | AT-001–AT-010、IT-026、MT-012–MT-015 |
| Task 13 — Floor Single Grid Scene feedback | AT-043–AT-048、IT-021 | IT-027、MT-008–MT-011 |
| Task 14 — Surface footer and Catalogue label | IT-008–IT-017、IT-035 | RT-004、RT-006、MT-002、MT-004、MT-026、MT-032 |
| Task 15 — Wainscoting normal/shadow correction | AT-030–AT-031、IT-028 | MT-016、MT-030 |
| Task 16 — Wall Decor ghost wall-local pose | AT-055–AT-060、IT-029–IT-030 | MT-017–MT-023、MT-031 |
| Task 17 — Regression and Owner review package | RT-001–RT-012 | MT-001–MT-034、G-A4 |

## 16. Self-review findings

- **Case inventory:** total `149`；Automated EditMode `AT=68`、Integration/PlayMode `IT=35`、Regression `RT=12`、Manual `MT=34`；IDs unique、连续且按文档顺序排列。
- **Placeholder scan:** 无未解决的占位标记；`WAIT-ASSET` 是明确 gate 状态，不是遗漏。
- **Coverage:** normal、invalid、boundary、recovery 均覆盖；AT-065 单独验证 production `RoomSurfaceSnapshot` 的 ordered Room/Wall/64-Floor round trip，AT-066 单独验证 production `WallMountedLayoutSnapshot` 的 ordered Surface/Instance attachment 与 atomic occupancy rebuild；2 Walls、`8×8` Floor、`8×2` Wall Slots、cross-wall atomicity、四 Modes、Surface/Wall-mounted transactions、responsive matrix、safe area、exit discard 均有 automated/integration/manual evidence。
- **Conflict check:** Wainscoting 高度仍来自 shared waist reference，且无 procedural crosshatch/fence-like shadow；fade opacity、Bottom Sheet exact heights、card size、drag threshold保持可调，但 approved transition 固定为 `0.16s`。Wall不含Apply All，Floor仍保留Apply All。
- **Scope check:** 两个 production `[Serializable]` snapshot contracts 仅为 data-only capture/rebuild boundary，使用 serializable ordered entries；不是 test-only wrapper，不加入 Phase 17 UI/storage。其余未加入 wall build/delete、real opening、price/currency/inventory/unlock、wall-mounted Rotate 或 drag-to-paint。
- **Known dependency:** MT-029–MT-033 的正式素材已接入并可执行，但仍需要 Studio Owner visual decision；自动化通过不能替代。
- **Implementation-state caveat:** Baseline Tasks 1–10 source 已存在；2026-08-27 amendment expected results 只来自 approved design，不从当前实现反推。Tasks 12–17 必须重新建立可信 RED。

## 17. Amendment approval required

Studio Owner 请 review 2026-08-27 amendment：

1. cases 是否忠实覆盖 approved player behavior；
2. placeholder technical acceptance 与 formal visual acceptance 的 gate 是否可接受；
3. 是否批准冻结本 matrix 并继续 Task 12。

在明确批准前，不得把本文件当作 Task 12 implementation 或 Phase completion 授权。

## 18. Round 1 QA fix note

本轮根据 independent QA review 修正 matrix，并仅为 spec conflict 同步 implementation plan：

- Wainscoting 改为 shared waist reference / canonical-wall derived cutoff，不再写死 tuning value；
- MT-018 当时曾指定 Validation Scene/test-only fixtures；2026-08-28 manual-review amendment 已用可操作的 MainCafe production `2×1/1×2` 流程取代，large fixture 精确合法性继续由 AT-011/AT-012 判定；
- MT-029/MT-030 分离正式 Floor 与正式 Wall Surface Owner acceptance；
- AT-065/AT-066 增加 stable identity rebuild 与 serializable snapshot round trip；
- IT-035 增加完整 responsive bounds/raycast/safe-area matrix；
- MT-002 补齐 folder-tab shape、front/back、shadow 与 touch PASS/FAIL；
- MT-032 改为 placeholder 可执行的 `READY` fade/UI tuning，MT-033 单独等待正式素材 recheck；
- AT-067 固定首批 Surface count/availability，AT-068 固定 pre-generated Sprite/no Camera/no RenderTexture；
- MT-016/MT-024 移除截图无法证明的 data/Collider/Nav 结论，改指向 automated authority；
- traceability 与 Task mapping 已同步新 IDs、actions/exclusions、formal asset gates；
- Status 仍为 `Draft for Studio Owner review`，本轮没有 implementation 或 Phase completion claim。

## 19. Round 2 QA fix note

- Implementation plan 的 Runtime domain 新增 production `[Serializable]` data-only `RoomSurfaceSnapshot.cs` 与 `WallMountedLayoutSnapshot.cs`，明确 serializable ordered entries，排除 test-only wrapper 与 Phase 17 UI/storage。
- Task 1 增加 `RoomSurfaceLayout.CaptureSnapshot()` / `FromSnapshot()` public contract、RoomId + ordered Walls + ordered 64 Floors schema，以及 deterministic capture → serialize/deserialize → rebuild RED/GREEN。
- Task 2 增加 `WallMountedLayout.CaptureSnapshot()` / `FromSnapshot()` public contract、ordered Surfaces + mounted Instance attachments schema，以及 occupancy rebuild、production validation 与 atomic rejection RED/GREEN。
- AT-065 现在只验证 Room Surface production snapshot；AT-066 现在只验证 Wall-mounted production snapshot。两者都使用唯一明确的 capture → serialize/deserialize → `FromSnapshot` path，不再使用可替代路径措辞。
- Task mapping 已修正为 Task 1 只拥有 AT-065，Task 2 只拥有 AT-066；traceability 与 self-review 同步。Case counts 保持 `149`。

## 20. 2026-08-27 Surface transaction amendment note

- 保持既有 `AT=68 / IT=35 / RT=12 / MT=34` 和唯一 case IDs，不新建重复矩阵；改写冲突 cases。
- Wall 删除 Apply All，改为 current-target Base + Wainscoting multi-layer atomic transaction；补 HasChanges、no-change retarget、changed target lock 与 fault rollback。
- Floor Single Grid 补 Selected highlight、ArmedStyle 与 Previewed Grid Scene checks 的完整 lifecycle。
- Floor/Wall actions 进入 Bottom Sheet fixed Surface footer；Compact 保留 Tabs + footer，transition 固定 `0.16s`，Catalogue label 必须存在。
- Wainscoting 补 no-crosshatch / no-fence-shadow contract；Wall Decor ghost 补五个 production prefabs 的 wall-local pose/bounds matrix。
- 正式 Surface 和五个 Wall-mounted assets 已接入，因此 MT-029–MT-033 均为 `READY`，但仍未获得 Studio Owner visual acceptance。
- Implementation mapping 增加 Tasks 12–17；本次只更新文档，没有 implementation、Git 或 Phase completion claim。

## 21. 2026-08-27 automated closeout evidence

- Final graphics EditMode：`1436/1436`，failed/skipped/inconclusive `0`。
- Final graphics PlayMode：`590/590`，failed/skipped/inconclusive `0`。
- Final focused fixture evidence：RealTouch `18/18`、Phase 7 MainCafe Scene `15/15`、Phase 6 MainCafe Scene `9/9`。
- Final guarded working-copy audit：`273/273`，drift `0`。完整 raw/semantic serializer classification 记录在 Task 17 report。
- Eight frozen regressions 均有 final automated evidence；视觉手感、card size、fade opacity、Surface/Wainscoting material feel、Wall Decor framing 仍由 Studio Owner manual review 决定。
- `MT-001`–`MT-034` Status 和 Studio Owner decision 仍为空；automated green 不等于 Phase 7 Completed。

## 22. 2026-08-28 Task 19 interaction amendment evidence

- 不增加新的 case ID；IT-016、IT-023、MT-004、MT-007、MT-008、MT-017 改写为本轮已批准 contract。
- Wall Decor 明确区分空白 Scene drag（Camera pan）与 active ghost drag（只移动物品）。
- Surface Cancel/Confirm 为加宽单行按钮且不显示 tooltip；Floor footer 固定两行，Whole Room 灰化三个 Single Grid utility；Furniture/Wall Decor恢复跟随Preview的小圆icon actions，Wall Decor不显示Rotate。
- focused PlayMode `102/102`、focused EditMode `44/44`、P7 Scene `54/54`、Phase 6 Scene/RealTouch `27/27` 全绿。
- full PlayMode `600/607` 的 7 个失败均为 Unity Input System `statePtr` suite-order 污染；对应 Task 9 `2/2` 与 Phase 5 `5/5` 在干净 Editor 隔离复跑全绿。
- 以上仍不代替 Studio Owner 的 MT-001–MT-034 manual acceptance。

## 23. 2026-08-28 Task 20 thumbnail and UI self-audit evidence

- 历史 Task 20 当时使用 5 张 warm-wall Sprites；2026-08-29 后续 Studio Owner 决定已覆盖此视觉规则，现改为保留 mounted-angle 的 transparent object-only Sprites。正常 builder 仍不运行 Camera / RenderTexture，也不隐式覆盖 authored art。
- 新 Validator case 用有效的全黑 PNG 证明 `P7-MOUNTED-THUMBNAIL-BACKDROP` 从 RED 变为 GREEN；原文件在 `finally` 恢复。
- Production MainCafe audit 找到实际 content inset `9.237625 px`，低于 approved `12 px` minimum；authoring inset 从 `24` 调至 `40` 后 focused 与完整 Scene regression 全绿。
- Contact sheet：`outputs/phase7-self-audit/Phase7_Mounted_Thumbnail_ContactSheet.png`。Automated/截图 evidence 不代替 MT-031 的 Studio Owner hands-on visual acceptance。

## 24. 2026-08-29 M26/M29 manual-review amendment evidence

- 不增加 case ID；按 Studio Owner 后续要求改写 IT-018、IT-031、IT-032、MT-025 与 MT-029。
- M26 Exit Modal 从 Bottom Sheet runtime 移到 full-screen `Screen Canvas` layer，加入 dim blocker 与独立暖色圆角 card；Continue/Discard 保持 pointer-retention contract。
- M29 Floor Mode 进入即对全部 confirmed Furniture 使用 rendering-only `35%` fade；不修改 Collider、occupancy、Layout 或 Save；Mode switch、Cancel/Discard、exit、disable/fault 精确恢复 Materials 与 MaterialPropertyBlocks。
- RED：M26 `0/1`（旧 parent=`Phase7_UIRuntime`）；M29 `0/1`（Furniture 未 fade）。Focused GREEN `3/3`。
- Direct GREEN：MainCafe `23/23`、UI `55/55`、Surface `40/40`、Wall Mounted/Touch `51/51`、AssetBuilder `16/16`。Migration 首轮 `29/30` 暴露旧 test 仍从 Bottom Sheet root 计数 Modal buttons；按新 hierarchy 更新 regression 后 focused `1/1`、final `30/30`。
- 所有 final XML 的 failed/skipped/inconclusive 为 `0`；仍不代替 Studio Owner 对 M26/M29 和后续 MT-031–MT-034 的 hands-on acceptance。

## 25. 2026-08-29 M31/M32 follow-up evidence

- M31：新 Wall Decor Confirm 后保持 Compact，下一次 scene tap 可立即选中刚提交的 instance；focused `1/1`，完整 Wall Mounted/Touch regression `52/52`。
- M32：camera-to-target blocker distance 使用 rotated Wall 的真实 plane，而非 renderer world AABB near face；focused `1/1`，完整 Surface/Fade regression `41/41`。
- footprint：projection 位于最外层 Wall/Wainscoting/trim 再向外 `1 mm`，避免 z-fighting 与深浅绿变化；ghost/confirmed item 仍贴 Base Wall；双墙参数化用例 `2/2`。
- thumbnails：五张 production mounted item Sprite 使用 mounted-angle、object-only genuine alpha；AssetBuilder `16/16`，Validator drift `19/19`。
- 所有以上 final XML 的 failed/skipped/inconclusive 为 `0`；automated evidence 不替代 Studio Owner 对 M31/M32 的 hands-on acceptance。

## 26. 2026-08-29 final Phase 7 closeout evidence

- Studio Owner 在 production `Assets/Scenes/MainCafe.unity` 中完成 MT-001–MT-034：`34/34 PASS`，decision `GO`；Floor、Wall、Wall-mounted、fade 与 Bottom Sheet 的视觉和操作均已人工接受。
- Final compatibility review 发现旧 Phase 6 migration exact-graph guard 会把 canonical Phase 7 scene-owned `SurfaceFooterHost/FloorRange` subtree 误判为 hostile drift。新增 focused regression 先得到 RED `0/1`，再以只过滤该 canonical subtree 的 minimal fix 得到 GREEN `1/1`。
- Final Phase 6 migration：`127/127`；Final Phase 7 MainCafe migration：`30/30`。
- Fresh full EditMode：`1443/1443`；fresh full PlayMode：`625/625`；failed/skipped/inconclusive 均为 `0`。
- Final static/version-control review 未发现 open Critical/Important finding；`TestResults/`、self-audit outputs、temporary InitTestScene 与 generated solution files 不进入 PR。
- 已知 scope 保持不变：不建造/移动/删除墙，不切割真实 Wall opening；confirmed Window 仅在当前 runtime session 存在，reload 后清除；未宣称 player build evidence。

## 27. 2026-09-01 merge-review follow-up evidence

- Review finding 1：Wall Preview Cancel 会清除 selected Wall，但没有恢复 persistent `Select a wall to edit` guidance。Focused regression 先 RED `0/1`，minimal controller fix 后 GREEN `1/1`。
- Review finding 2：Wall Decor Store Confirm 是 terminal action，却没有立即恢复 blocker fade。真实 `WallOcclusionFadeView` + Material/MPB regression 先 RED `0/1`，minimal controller fix 后 GREEN `1/1`。
- Direct GREEN：Wall Mounted/Touch `57/57`、Surface/Fade `42/42`、Decoration UI `59/59`。
- Full PlayMode 首轮 `633/634`：旧 Phase 6 real-Mouse drag timing case 首帧位移为 `0`；该 case 隔离复跑 `1/1`，fresh full rerun `634/634`。Fresh full EditMode `1444/1444`；final failed/skipped/inconclusive 均为 `0`。
- Studio Owner 选择 nearest-slot 方案 A：preferred Surface 优先，再按 Manhattan distance → stable Surface ID → Column → Row；保留当前墙面直觉并保持 deterministic fallback。
- 本轮 automated evidence 不重新替代已完成的 MT-001–MT-034 manual acceptance。Studio Owner 后续已单独授权 merge；PR #6 于 2026-09-01 以 merge commit `925213af6132597592aa60d815434259b18b8ed1` 合入 `main`。Merge-day regression：EditMode `1444/1444`、PlayMode `634/634`，failed/skipped/inconclusive 均为 `0`；local `main` 与 `origin/main` 的 commit、tree 和 diff 一致。
- Post-merge remediation（2026-09-02）：新增 explicit Surface Kind、duplicate-root continuation、standalone committed-state preflight、Wall Decor Preview/Ghost/Mesh 与 Fade Material reuse、destroyed blocker-root cleanup、disk-backed staged Scene rollback、`.meta` byte verification及 target `SceneAsset` selection restore regressions。首次 full EditMode `1424/1445`，根因是 Windows IO 1224 与其 20 个 downstream failures；后续 full-order `1450/1451` 又定位到 hostile hash-drift test 的同类直接覆盖问题。Raw、derived 与 thumbnail drift 现统一用 test-only hash seam 注入，不再改写 canonical binaries；补充 focused `raw-hash` `1/1`、`derived-hash` `1/1`，且两套 Scene recovery parent 均在测试后不存在。Focused：rollback failure injection `6/6`、Phase 6 transaction `133/133`、Phase 6 validator `194/194`、Phase 7 validator `30/30`、surface/fade `46/46`、Wall Mounted Touch `57/57`；standalone committed-state preflight `1/1`、fresh full EditMode `1451/1451`、fresh full PlayMode `638/638`，failed/skipped/inconclusive 均为 `0`。
