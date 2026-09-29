# Phase 10 — Capacity & Reservation Test Cases

> 状态：Owner于2026-09-29批准的测试基线。实施已授权；各项实际结果在执行记录更新，尚无证据的项目保持 **NOT_RUN**。
> 日期：2026-09-29。对应 [design](2026-09-29-phase-10-capacity-reservation-design.md) 与 [plan](../plans/2026-09-29-phase-10-capacity-reservation.md)。
> 规则、测试步骤和技术合同已获整套文档批准。case 行数不等于 NUnit 展开后的 XML total。

## 1. 分层与公共断言

| 层次 | 执行者 | 证明范围 |
|---|---|---|
| E：EditMode domain / source adapter | Codex + NUnit | 公式、面积来源、原子预留、权限、状态、缩容和 P9 合同边界 |
| D：EditMode Editor工具 | Codex + NUnit | session、命令委托、数据重置和用户状态保护 |
| P：PlayMode | Codex + Unity Test Runner | 跨帧和时间边界；不证明真实NPC/队伍 |
| M：Manual / GUI | 默认 Owner；另获授权时可由 Codex 代跑并记执行者 | 可见字段、真实Play切换、Domain Reload、MainCafe操作smoke |
| R：Regression / review | Engineering / QA + Owner | 完整回归、覆盖、scope及阶段验收 |

所有结果使用 PASS / FAIL / SKIP / BLOCKED / NOT_RUN。未执行不是PASS；SKIP不计PASS。保留fixture保护性跳过的原记录，仅在有必要时隔离补跑并按exact fullname核对，不能改写原XML。

公共fixture：Unity 6000.5.5f1；每个test全新service；默认rules=4格/人、Counter50%；owner为V1/V2/V3（visit ID）；默认Floor=16，三项limits=(4,2,4)。元组顺序始终为Total / Counter / PickUp。

- **U（失败无副作用）：** 操作前后比较全部 Limits、capacity snapshot字段、全部token的身份/owner/kind/state、列表顺序、CanAdmit。另在独立fixture或比较之后probe下一次成功token Id、原owner能否继续合法操作，验证没有暗改ID或owner索引；probe不是被测失败操作的一部分。
- **I（不变量）：** 各kind `Used=Reserved+Occupied>=0`，Available=max(0,Limit-Used)，OverCapacity=max(0,Used-Limit)；统计与全部active token严格对应；每owner/kind最多一个active；Id正数、不重复/复用。仅合法缩容可产生超额，Reserve不能制造超额；Occupy不改变Used，Release只对原token生效。
- **成功result：** Succeeded=true、FailureReason=None、Reservations为本次相关snapshot（Reserve按kind排序，Occupy/Release为一条，Update为空）。失败为false、明确错误码、空Reservations。所有集合与snapshot不可用于修改service。
- 只能用公开API构造Reserved/Occupied/Released，不反射修改service。token不能通过公开constructor伪造；必要反射检查仅用于确认public API表面。
- Layout fixtures使用真实CafeLayout/regions API；只读source adapter不操作Scene。Floor移除用新的已确认Layout fixture及显式Update模拟，不增加Layout删除功能。
- 测试保存并恢复自身修改的Time.timeScale、窗口与设置；不覆盖用户Scene、Prefab、Selection或已有窗口。无法隔离时记BLOCKED，不强制保存/关闭用户工作。

## 2. E — 公式与 Floor 来源（Task 1）

| ID | 前置 → 操作 | 预期结果 |
|---|---|---|
| P10-E-001 | 默认rules；F=0,1,3,4,7,8,12,20,25,32,48,64 | 对应(0,0,0),(0,0,0),(0,0,0),(1,1,1),(1,1,1),(2,1,2),(3,2,3),(5,3,5),(6,3,6),(8,4,8),(12,6,12),(16,8,16) |
| P10-E-002 | F=int.MaxValue；默认及cellsPerCustomer=1/counterPercent=100 | 默认(536870911,268435456,536870911)；后者三项int.MaxValue；无浮点舍入/乘法overflow |
| P10-E-003 | rules=(5格,25%)，F=25；另测percent=1/100，F=4 | 前者(5,2,5)；后两者均(1,1,1)，ceil正确；rules不可写 |
| P10-E-004 | 负面积Calculate；cellsPerCustomer=0/-1；percent=0/-1/101 | 分别ArgumentOutOfRangeException；有效rules不受影响 |
| P10-E-005 | Interior region(0,0),8×8，bounds同范围 | CountInteriorCells=64，Calculate得到16/8/16；是基线结构fixture，不冒称真实Scene测试 |
| P10-E-006 | 空Layout；只有Exterior region；null Layout | 前两者0；null抛ArgumentNullException |
| P10-E-007 | Interior(0,0)4×4和(2,0)4×4；另有分离1×1 | union分别24、25，不重复重叠，不计中间空格；相同区域不同ID也只计一次 |
| P10-E-008 | bounds=(0,0)4×4，Interior=(-1,-1)6×6；另测完全在bounds外的region | 16；外部region不加面积；没有bounds的合法小fixture按真实区域计数 |
| P10-E-009 | 同布局放置/移动家具、添加EntranceClearance/Blocked；另保存旧区域查询 | 面积不变；adapter无Layout写入；外观Preview不作为adapter输入 |
| P10-E-010 | 1×1区域位于(int.MaxValue,0)；然后2×1；负坐标小区域 | 1×1计1；2×1抛OverflowException，不把坐标回绕；负坐标合法且准确。domain最大格数由E-002验证，不创建数十亿格fixture |

## 3. E — 预留、原子性与身份（Task 2）

| ID | 前置 → 操作 | 预期结果 |
|---|---|---|
| P10-E-011 | 新service(16)；另以负Floor或非正firstTokenId构造 | 正常Used全0、CanAdmit=true、历史空；非法构造ArgumentOutOfRangeException |
| P10-E-012 | TryReserveAdmission(V1) | 三个Reserved，Ids1/2/3按kind排序，Used=(1,1,1)；未自动Occupy |
| P10-E-013 | 参数化1/2/3种合法组合，输入倒序；每组新fixture | 每种1个token，结果/ID按kind顺序；未申请项不变，I |
| P10-E-014 | V1/v1/合法中文visit ID分别申请单项 | ordinal区分、保留原文；同customer的不同visit ID可独立存在 |
| P10-E-015 | owner=null/空串/空白/tab/首尾空白 | InvalidOwnerId+U，后续合法请求得到原next Id |
| P10-E-016 | kinds=null/空/重复/未定义enum；合法项混非法项且放在不同位置 | InvalidKinds+U；不能先写合法部分 |
| P10-E-017 | V1已有Counter Reserved或Occupied；再申请[Total,Counter] | OwnerAlreadyReserved+U，不先取得Total；对另一单独kind可申请；Released不算active |
| P10-E-018 | F=16，两次Admission后V3再Admission | Counter满，InsufficientCapacity+U；释放V1 Counter后V3成功，Used=(3,2,3) |
| P10-E-019 | F=8，V1/V2只预留PickUp；V3 Admission | PickUp满而其他空，InsufficientCapacity+U；释放一个PickUp后重试成功 |
| P10-E-020 | F=8，V1/V2只预留Total；V3 Admission | Total满，InsufficientCapacity+U；F=0任意kind也拒绝；恢复后可申请 |
| P10-E-021 | firstTokenId=Max-2申请Admission；另fixture first=Max-1先Admission再两项 | 第一组可用Max-2/Max-1/Max，后续TokenIdExhausted；第二组Admission整体耗尽失败，两项仍取得Max-1/Max，证明不消耗ID；全部U/I |
| P10-E-022 | capacity失败后释放并重试；非法请求后重试；同帧先后两个owner争一个空位 | 失败不消耗ID/owner权限；仅第一个合法请求成功，无线程并发声明 |
| P10-E-023 | 保留Reserve输入List和result；成功后改输入List，再成功申请其他owner | service记录、原result不随外部改变；公开API无可变token身份或public token constructor |

## 4. E — Occupy / Release / 不可变查询（Task 3）

| ID | 前置 → 操作 | 预期结果 |
|---|---|---|
| P10-E-024 | 单种Reserved → Occupy，参数化三个kind | Reserved-1、Occupied+1、Used/Available不变，返回新snapshot |
| P10-E-025 | Occupied → 再Occupy | InvalidTransition+U，不重复扣额度 |
| P10-E-026 | Reserved → Release | Released，Reserved-1、Used-1，可再申请；无需先Occupy |
| P10-E-027 | Occupied → Release | Released，Occupied-1、Used-1；其他kind/owner不变 |
| P10-E-028 | 同owner对同Released token Release两次 | 每次Success，返回Released；所有计数、nextId不变 |
| P10-E-029 | Released → Occupy | InvalidTransition+U |
| P10-E-030 | 三种state各用错误owner Occupy/Release；另非法owner | WrongOwner或InvalidOwnerId+U；Released也不得绕过身份检查 |
| P10-E-031 | serviceA/B都签发Id=1；跨service使用token及null | InvalidToken+U，两个service均不变；不可仅按Id匹配 |
| P10-E-032 | V1同kind先A→Release，再取得B；再Release/Occupy A | Release A成功无变化；Occupy A拒绝；B不变，Used=1，A/B不同Id |
| P10-E-033 | 保存旧GetCapacities/GetReservations后进行合法变更 | 旧snapshot/集合固定，不能通过cast或引用修改service；新查询按kind/Id排序且含Released |
| P10-E-034 | 三state×两操作×正确/错误owner完整矩阵 | 12组均符合design状态表；每个拒绝均U，每个成功均I |
| P10-E-035 | 同时多个错误：非法owner+null token；错误owner+Released；Reserve非法kinds+重复owner、重复owner+满额、满额+ID耗尽 | 依次返回InvalidOwnerId、WrongOwner、InvalidKinds、OwnerAlreadyReserved、InsufficientCapacity，U |

## 5. E — 动态容量、恢复与 P9 边界（Task 4）

| ID | 前置 → 操作 | 预期结果 |
|---|---|---|
| P10-E-036 | F=16，有Reserved/Occupied混合；Update到32 | Limits变8/4/8，token身份/状态/历史不变；Update result成功空Reservations，未自动填空位 |
| P10-E-037 | F=16，V1/V2各Admission；缩到4 | Limits=1/1/1，Used=2/2/2，Available全0，OverCapacity=1/1/1；原tokens保留，新Reserve返回CapacityOverLimit+U |
| P10-E-038 | 承E-037缩至0；把全部旧Reserved依次Occupy再Release | Occupy均成功且Used不变；Release后最终全0；Limits=0仍不允许新客 |
| P10-E-039 | F=32，四名owner只Reserve Counter，缩至16；申请只Total | CounterUsed4>Limit2，其他有余量也CapacityOverLimit；release两个后不再超额，但CanAdmit仍false；此时只Total可申请 |
| P10-E-040 | F=32、V1..V4各Admission，缩到16；逐步释放Counter至Used=1，再释放一个Total/PickUp | Total/PickUp曾满4也不能Admission；最终三种都有余量才CanAdmit=true；显式申请才出现新token。另用扩容恢复，不自动补齐 |
| P10-E-041 | 有reservation时Update(-1/int.MinValue) | InvalidFloorCellCount+U；原tokens仍可正确Occupy/Release |
| P10-E-042 | 相同Floor反复Update；变更Floor但未跨公式阈值 | Limits.FloorCellCount准确更新；所有tokens不变；计数不丢失、不重复发布reservation |
| P10-E-043 | 真实Layout fixture 4×4→增加相邻4×4→用新确认2×2 Layout替换来源；每次Count后Update | F=16→32→4；旧tokens保留；只读adapter不修改Layout，模拟已确认面积刷新而非实际扩建 |
| P10-E-044 | seed=17，200次Reserve/Occupy/Release/Update及错误owner/token混合 | 每步I，失败U；日志seed/step/命令；缩容之外不产生新的超额；不另建生产算法副本作为oracle |
| P10-E-045 | 两service独立：V1 Admission并Occupy Total/Counter；按P9真实API创建订单；显式Release Counter后Occupy PickUp；P9领取、准备、送达、正确Complete；随后Release PickUp、Release Total | 创建订单或Complete不会隐式释放容量；Release Counter前其仍Occupied；Complete后PickUp/Total仍Occupied；Release PickUp后Total仍Occupied；Release Total后全归还；Order保持Completed |
| P10-E-046 | P9 WrongCustomer失败/Fail/临时不操作；保留Capacity snapshots | Capacity不隐式变化；明确放弃尚未入店的预约可Release Reserved；不把Order Failed等同于顾客已经离店 |
| P10-E-047 | 查询100次；改变独立P9 service；两个CapacityService同owner | 不产生token、状态或容量变化；service互相隔离 |
| P10-E-048 | 独立fixtures：F=16，用单项预留让某kind恰好满，或让三种各余1；超额组从F=32成功预留，再Update到16构造，不能靠非法Reserve制造超额 | CanAdmit严格按三种额度；该属性true也不能替代原子申请（用owner重复/ID耗尽证明可仍失败） |

## 6. D / P — Editor 与帧边界（Task 5）

| ID | 操作 | 预期结果 |
|---|---|---|
| P10-D-001 | 新debug session，修改输入但不Apply，再Apply | 默认F64、16/8/16、V1、Id1、全选；输入与已应用值分离；Apply仅委托service |
| P10-D-002 | 窗口按钮各一次，含Reserve Selected空选、负Floor、未知Token ID | 成功/错误与domain一致，LastResult正确；无异常、无第二份业务规则 |
| P10-D-003 | 有tokens且改变所有字段→Reset | 新service；F64/V1/Id1/全选/滚动0/结果null；旧token对新serviceInvalidToken |
| P10-D-004 | 测试拥有的窗口关闭/重开；生命周期回调 | 数据清空、注册/解绑对称；回调测试仅是单元证据，不替代M-008真实Play |
| P10-D-005 | 拍下Scene dirty、Selection、timeScale及用户Scene引用，打开/查询/Reset/关闭自有窗口 | 前后不变；无GameObject、Save/PlayerPrefs写入；不能强关用户窗口 |
| P10-D-006 | 连续操作后读取窗口表格数据 | 状态与当前service一致，Released仍可见；不沿用result内的旧snapshot作当前表格 |
| P10-P-001 | 入店预留→跨帧Occupy→缩容→跨帧释放→恢复 | 每帧保留正确token/owner/count；仅显式命令改变状态 |
| P10-P-002 | 同一测试保存时间，分别timeScale=0/1/2各等待帧与realtime；之后显式Reserve/Release | 不自动释放/推进/补满；显式命令仍遵守domain；finally/TearDown恢复时间 |
| P10-P-003 | 跨帧创建新service，持有旧service token | 新service空，旧tokenInvalidToken；不继承全局/static数据 |

## 7. M — 真实 GUI 验收（Task 6）

公共准备：批准后的P10 checkout，Unity 6000.5.5f1；打开已保存MainCafe与Console；处理自己的未保存工作。打开 `Window > AnimalCafe > Phase 10 Capacity Debug`。除M-008外先进入Play，每例先Reset。表格核对Used与R/O，不只看Success。

Token选择：Reserve Admission按Total/Counter/PickUp生成连续Id；每次重置从1开始。选token时复制表中的Id，并输入该visit的Owner ID。以下“占用/释放某token”均使用Occupy/Release按钮，不表示真实顾客行动。

| ID | 具体步骤 | 预期、恢复与记录 |
|---|---|---|
| P10-M-001 | Apply F=8；V1 Reserve Admission；Occupy 1/2；观察；Release 2；Occupy 3；模拟取走时先不按Release；再Release 3、最后Release 1 | 依次Used=(1,1,1)→(1,0,1)→(1,0,1)→(1,0,0)→(0,0,0)；看得懂“取走≠离位≠离店”；无真实Order按钮，不冒称验证P9 GUI；Reset |
| P10-M-002 | F8，V1 Admission；V2 Admission失败；改V1 Release Counter #2；V2重试；再V3 Admission | 首次V2失败因Counter=1且nextId未消耗；重试取得#4/5/6；V3被拒。释放V2 Counter仍不足以入店，因为Total/PickUp满；Reset |
| P10-M-003 | F8只选PickUp；V1、V2各Reserve Selected；V3 Reserve Admission；V1 Release #1，V3重试 | PickUp独自满也阻止整笔申请，失败不占Total/Counter；释放后才成功；Reset |
| P10-M-004 | V1单项Reserve；V2尝试Occupy/Release该Id；输入空owner、未知Id、空选、负Floor并执行 | 分别错误且表格不变，错误可读，Console无新增Error/Exception；恢复合法字段仍能成功；Reset |
| P10-M-005 | V1单项Reserve→Release两次；V1同项再Reserve新token；再次操作旧Id | 重复Release旧token成功但新token不动；旧tokenOccupy报InvalidTransition；Reset |
| P10-M-006 | F16，V1/V2各Admission；Apply F4再F0；用原owner逐一Occupy旧tokens；新V3申请；逐一释放；Apply F8；V3申请 | 超额数字正确且不删除旧tokens；旧预约仍能占用；新申请被拒；全部释放且扩容后显式重试成功，不自动填满；Reset |
| P10-M-007 | 有名额时Pause/1x/2x，各观察数秒；显式申请/释放；再只Apply扩容并等待 | 等待不产生spawn/token或释放；显式命令正常；恢复原时间（通常1x），Reset |
| P10-M-008 | 修改Floor/Owner/Token并保持输入焦点，Reset；关闭重开；在Reload Domain ON与OFF下真实进出Play，OFF连续两轮，每轮生成token再切换 | 可见字段与数据同步回默认，不是仅内部属性正确；历史/结果清空，重新申请从1开始；设置由执行者切换并恢复原值。无法安全切换记BLOCKED；保留ON/OFF证据 |
| P10-M-009 | 比较开窗前后MainCafe：真实Camera拖动/缩放、Pause/1x/2x、Decor预览→Cancel→Done；检查Console与Scene dirty | 操作可用，布局未变，无新增Error/Exception；恢复原时间/退出Play/关闭P10窗口，不保存临时改动。Camera拖动必须实际观察，不沿用P9 Owner确认 |
| P10-M-010 | Apply F=20、32、48、64，读取三项；检查输入未Apply时的显示；V1只选PickUp，Reserve Selected后Release该token，再查Released历史及CanAdmit | 分别5/3/5、8/4/8、12/6/12、16/8/16；归还后历史仍显示Released、CanAdmit=true；Owner能区分Reserved/Occupied与实际排队，窗口清楚标注临时模拟；Reset |

M-001是容量命令语义的可理解性验收；Order与Capacity的独立性由E-045/046验证，实际NPC位置和离位事件留到P12/P14。P10不提供人工放置真实队伍的功能，不能把manual模拟记为营业或空间布局PASS。

## 8. R — 收尾、证据与执行记录

| ID | 检查 | 验收要求 |
|---|---|---|
| P10-R-001 | P10 focused + P9直接regression；最后完整EditMode/PlayMode | XML记录精确PASS/FAIL/SKIP；执行串行；无变化不重复长回归；修复后按Phase流程补验 |
| P10-R-002 | Engineering source/runtime review | 无P9合同修改/Scene wiring；确认token签发、batch失败、shrinking、source映射和数值安全 |
| P10-R-003 | QA coverage/evidence review | 每个case有对应test或manual evidence；RED真实，未运行不填PASS；所有Important/Critical关闭 |
| P10-R-004 | Owner必要manual与文档核对 | M-001…010逐项记执行者/日期/版本/结果/证据；未完成的必要项阻止P10完成，朋友P11后review不替代 |

执行记录沿用本文件更新，开始时全部NOT_RUN。每Task仅记录focused RED/GREEN与direct regression摘要；详细XML/log放 `outputs/phase10/`，不提交生成证据。Beginner Guide链接本表，不另造第二套case ID。

以下Task 1–5是按执行顺序保留的历史快照，其中“尚未运行”只表示当时状态；当前状态以第9节为准。

Task1（2026-09-29）：E-001…010已由task1-green.xml验证，27个展开测试PASS、0 FAIL/SKIP；直接Layout regression 31 PASS。初始RED 27 total、3 PASS、24预期行为FAIL，保留task1-red.xml；低Floor零额度3项在占位实现上直接GREEN，如实记录。E-011…048、D-001…006、P-001…003、M-001…010及R-001…004当时NOT_RUN。执行者为Codex/sub-agent，均为domain fixtures，不代表Scene/GUI验收。


Task2补充（2026-09-29）：最终task2-green-final.xml共51 PASS（含Task1 27）；E-011…016、021、023及E-017…020/022的预留拒绝路径已验证。E-017 Occupied/Released、E-018…020及E-022 Release恢复段仍NOT_RUN，Task3补齐；初始RED为51 total / 28 PASS / 23预期FAIL。独立task review通过，2项Minor加强建议留Task3及最终review核对。

Task3补充（2026-09-29）：task3-green-all-a.xml 85 PASS、0 FAIL/SKIP，覆盖E-001…035及Task2全部延期段；task3-p9-direct-regression.xml 132 PASS。Occupy/两类Release/幂等各有实际RED和GREEN；身份保护测试直接GREEN，无独立RED。独立review功能与质量通过，E027另一个顾客当前record断言加强建议留最终核对。

Task4补充（2026-09-29）：task4-green-all-c.xml 102 PASS、0 FAIL/SKIP，全部E-001…048有domain覆盖；P9直接132 PASS。E-046目前仅证明P9调用前后容量不变，真实经过等待的稳定性由Task5 P-002验证。动态刷新是fixture显式调用，不能记MainCafe自动监听或实际扩建PASS。

Task5补充（2026-09-29）：E/D共108 PASS（task5-edit-green-b.xml），P-001…003共3 PASS（task5-fix1-play.xml），P9 PlayMode直接3 PASS。P-002实测三个帧及realtime等待、timeScale0/1/2，核对token身份、owner、kind、state及显式Release。窗口没有初始行为RED，仅编译失败；手工M-001…010及最终R-001…004仍待Task6记录。

## 9. 最新执行快照（2026-09-29）

环境：Unity 6000.5.5f1，checkout `.worktrees/phase-9-order-domain`，branch `codex/phase-9-order-domain`，验证基线HEAD `94cc48d7d8c6b43261b96e73bf2d2b6541fafbf4`。执行者为Codex、独立reviewer及补验拖动的Owner；Unity运行串行。**P10开发、自动/manual验收和final overall review均已通过，Owner授权在同一P9分支commit/push；尚未merge至main，Roadmap保持In Progress直至正式closeout。**

| Case | 当前状态 | 实际证据与限制 |
|---|---|---|
| E-001…048 | PASS | 102个domain展开用例；全部纳入task5-edit-green-b.xml的108 PASS及完整Edit回归。包括此前延期的Occupied/Release恢复段。 |
| D-001…006 | PASS | 6个Editor自动用例；同上108 PASS。回调与属性测试不替代真实焦点/Play切换。 |
| P-001…003 | PASS | task5-fix1-play.xml 3 PASS；全部纳入full-play.xml。 |
| R-001 | PASS（按记录允许的SKIP） | 完整回归见下表；4个opt-in Play Skip保留，不计PASS。P9直接Edit132 PASS、Play3 PASS。 |
| R-002 | PASS | 原Engineering source gate及Owner要求的final overall Engineering review均通过；覆盖全部23个P10 C#，0 Critical / Important / open Minor，无代码修改请求。 |
| R-003 | PASS | final overall QA独立核对原始XML、补跑对应关系、当前源码hash、manual截图与实际执行者；M-001…010及R-004已完成，0 Critical / Important / open Minor。最终文档同步后另行读回。 |
| R-004 | PASS | Owner授权Codex代跑GUI并补验M-009实际拖动；在收到P10范围说明和结果后，于当前对话要求“如果所有的manual test都通过过了，做一次final overall review就可以push到branch上面，和p9同一个branch就好”。作为接受本轮委托验收并授权review后提交/推送的依据，不代表Owner亲自执行了所有按钮检查。 |

完整回归的原始XML均保留在 `outputs/phase10/`，不提交生成证据：

| 文件 | Total | PASS | FAIL | SKIP |
|---|---:|---:|---:|---:|
| full-edit.xml | 2363 | 2008 | 0 | 355 |
| full-edit-isolated-phase6-migration.xml | 160 | 160 | 0 | 0 |
| full-edit-isolated-phase6-validator.xml | 194 | 194 | 0 | 0 |
| full-edit-isolated-phase8-safety.xml | 1 | 1 | 0 | 0 |
| full-play.xml | 1046 | 1042 | 0 | 4 |

Edit原始355个Skip由旧fixture的dirty Scene保护触发；在新Unity进程中补跑，未强制保存或关闭用户未保存工作。三份补跑与原355个Skip的fullname按ordinal逐项匹配：无遗漏、重叠或额外项。因此**有效Edit覆盖2363 PASS / 0 FAIL / 0未解决SKIP**，不是把2363与355相加。原始2363个leaf各有不同NUnit case ID；E013中三组array参数恰有相同fullname，不能按fullname去重丢掉两项。355个被补跑的Skip fullname则互不重复。对照表为 `full-edit-initial-skips.csv`，汇总为 `full-regression-summary.json`。

Play的4个Skip均来自 `AnimalCafe.Tests.PlayMode.EditorSceneLoading.`，具体类/方法与原始reason如下；没有为清除Skip开启截图开关：

| 类与方法（接上述共同前缀） | 原始reason |
|---|---|
| P8RCashRegisterSideIndicatorTests.CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery | Set ANIMALCAFE_CASH_SIDE_CAPTURE=1 for native Cash Register evidence. |
| P8RCompactChromeTests.SpacingAudit_WhenRequested_ExportGeometry | Opt-in spacing geometry audit. |
| P8RReadinessSafeAreaTests.ReadinessChecklist_CaptureNativeExamplesWhenRequested | Opt-in native Game View evidence; no synthetic screenshot compositing. |
| P8RUiEnhancementSceneTests.NativeUiExamples_WhenRequested_CaptureActualGameView | Opt-in real Editor Game View capture; not a synthetic UI image. |

收尾校验：23个P10 C#文件在full regression前后SHA-256一致，所需.meta存在；测试产生的已知旧材质/Scene/Prefab等改写已保存patch并限定路径恢复，既有Assets/Packages/ProjectSettings无tracked变化。没有修改P9源码、接线Scene或用户原checkout。校验结果为 `post-full-verification.json`。测试日志保留；NUnit无失败不等于Unity退出日志完全无诊断噪声。

必要manual执行表（2026-09-29，Owner在当前对话授权Codex代跑；所有操作为真实Unity GUI，独立sub-agent只审图，不操作Editor；不沿用P9结果）：

证据目录为 `outputs/phase10/manual-2026-09-29/`。下表文件前缀均指其中未修改的原生PNG；`capture-index.json`记录截图时间，[execution-journal.md](../../../outputs/phase10/manual-2026-09-29/execution-journal.md)记录操作与限制，`independent-gui-evidence-review.md`记录独立审图结果。

| ID | 结果 | 实际执行者 / 日期 / Unity版本 | 实际证据 |
|---|---|---|---|
| M-001 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m001-*`：预留、占用、逐项释放的Used变化正确；取餐后未Release时名额保留。独立审图PASS。 |
| M-002 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m002-*`：Counter满、Total/PickUp满分别拒绝，失败不消耗ID；重试取得#4/5/6。独立审图PASS。 |
| M-003 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m003-*`：仅PickUp满也拒绝整笔Admission；释放后显式重试成功。独立审图PASS。 |
| M-004 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m004-*`：错误owner/空owner/未知ID/空选择/负Floor均不改变预约，恢复合法输入后可成功；Console 0/0/0。独立审图PASS。 |
| M-005 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m005-*`：重复Release不影响新token，旧Released token不能Occupy。独立审图PASS。 |
| M-006 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m006-*`：缩至F4/F0保留6个旧token并允许Occupy，拒绝新客；释放、扩容后不自动填满，显式申请成功。独立审图PASS。 |
| M-007 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m007-*`：Pause/1x/2x等待均无隐式变化，显式命令正常；扩容等待不补满，恢复1x并Reset。独立审图PASS。 |
| M-008 | PASS（代跑） | Codex / 2026-09-29 / 6000.5.5f1 | `m008-*`：焦点字段Reset、关闭重开、Reload Domain ON进出及OFF连续两轮均恢复默认并从ID1重启；设置恢复，独立审图PASS。 |
| M-009 | PASS（Codex代跑＋Owner拖动确认） | Codex、Owner / 2026-09-29 / 6000.5.5f1 | `m009-before-*` / `m009-after-*`：Codex完成开窗前后缩放、Pause/1x/2x、Decor→Preview→Cancel→Done；恢复1x、退出Play、关闭窗口，Console 0/0/0，Scene无dirty。工具拖动未确认位移，随后Owner按要求用真实鼠标补验开窗前后并回复“两种情况下都能移动”，关闭此阻塞；不冒称Owner操作有Codex截图。 |
| M-010 | PASS（Codex技术检查＋Owner接受委托验收） | Codex、Owner / 2026-09-29 / 6000.5.5f1 | `m010-play-*`：F20/32/48/64为5/3/5、8/4/8、12/6/12、16/8/16；未Apply不改变容量；Released历史与CanAdmit正确，最终Reset。独立审图PASS；Owner在收到逻辑名额/真实排队的范围说明后授权final overall review并推送，接受本轮结果。 |

GUI收尾快照：`post-manual-verification.json`确认23个P10 C#文件与自动回归基线hash一致；MainCafe、EditorSettings、ProjectSettings三个文件与GUI操作前hash一致，既有Assets/Packages/ProjectSettings无tracked差异。GUI结束时branch/HEAD未变，当时未保存Scene、修改产品代码或commit/push/merge。此后仅同步文档和本地证据，未重复自动回归。

证据限制：M-010最初一组在Edit Mode，已补跑完整`m010-play-*`，仅补跑作为该项正式GUI证据；M-008浮动窗口越界截图不证明重开后的字段，采用`m008-reopened-visible-*`。M-009的`before-paused`是未成功的探测，实际Pause证据为`before-pause-confirmed`。Owner两次移回窗口只解决工具定位限制，不是代替测试或批准结果。

Owner补验记录（2026-09-29 19:39 UTC）：当前对话明确确认M-009开窗前后均能拖动，结果更新PASS；journal保留此前BLOCKED及后续解除记录。补验回复后重新核对Scene及两份设置hash，均与操作前一致。

本轮GUI与自动结果均不代表营业循环、随机到店、真实队伍、扩建或存档验收。Task 2中间50/51失败XML被同名重跑覆盖、Task 3身份测试直接GREEN、Task 5窗口没有初始行为RED等过程偏差保留，不补造RED。Owner接受委托验收并授权final overall review后推送；M-001…010全部通过，最终Engineering和QA整体review均PASS。P10尚未merge至main，不标Roadmap Completed。

Final overall review（2026-09-29）：Engineering独立重读23个P10 C#，QA独立解析原XML、355个补跑对应并核对manual证据，两者均PASS、0 Critical / Important / open Minor。本次没有源码修复；23个源文件与完整回归版本一致，原4个Play opt-in SKIP保留。Owner已授权同P9分支commit/push，提交范围56个源码/meta/文档文件；原始XML/PNG与scratch报告留本地。分支交付不代表main merge或下一阶段已获批。
