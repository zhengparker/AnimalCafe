# Phase 14 — Integrated Takeout Cafe Loop Design

> 日期：2026-10-19。状态：Approved for Implementation — 文档审查修订完成，Owner已明确批准按配套plan在当前chat逐Task实现，并允许sub-agent。
> 原Task1–9、Owner追加warning／顾客持杯／多侧交互、Shared导航及长柜Facing补修已实施。此前Facing252／133、Shared334／169等仅保留历史源码范围；最新进度与证据见配套plan和Validation Report，不将设计批准或技术PASS当作Owner验收。
> Owner旧三项visual PASS保留；2026-10-23新批准的自然单列、P14收银1秒与多人矩阵已通过最终同源直接Edit267／Play171，均0F/0S；19原家具／rapid变体及2个controlled恢复case分列，独立QA已关闭自动化问题。普通Editor与清理见Validation Report，新观感留Owner确认；旧256／164是2秒历史。Player/device仍为 `NOT_RUN`，Phase未Completed。

## 1. 目标、范围与来源

> 2026-10-23 Owner批准追加：自然单列优先＋P14收银1游戏秒，制作仍3秒、来客仍随机1～5秒；原P13独立模式仍收银2秒。此条覆盖本文旧P14收银2秒说明，旧测试结果保留其历史源码范围。按原Task9继续开发与测试，不重复请求已批准的方案。

### 本轮已批准的队形与节奏

- Pick-up稳定等候链优先沿当前队列方向延伸在前客身后。安全直行仍可用时，不为取得更多物理名额而回溯改成横向铺开；直行被真实几何／入口／完整路径／员工往返约束拒绝后才安全转弯。转角按世界空间队列方向判断，不用屏幕X坐标或“朝前客”代替站位断言。
- 若只剩较短安全队伍，按真实物理站位限流，不先收费后丢位置；已付费客的Order／Visit／FIFO／原reservation保持。让位、领取、离场的暂时错位允许，稳定后回到原安全链；原完整送杯返程通道、有限让位、暂时交通等待及明确Blocked不放松。
- P14集成模式收银1游戏秒、Barista制作3秒；P12／P13旧独立模式、Nav速度／身体／容差／规划预算不改。沿现有Runner时长参数显式传入P14值，不直接改共享默认值；暂停与2×只缩放一次，disable／Resume不能恢复成2秒或重复收费。
- 多人验证覆盖原合法家具类别及四向参数：分别尝试稳定2／4／6人。空间不足需证明真实可用站位上限与未获预留者不收费；人数不足不能直接标该人数PASS。用Hold Collection积累真实Paid队列，再恢复验证前移、让位／复位与全部原token排空；另跑自然来客与普通／加速各20单，不承诺正常经营永远多人排队。
- 本轮不新增玩家队列绘制UI、不改Scene／prefab／Save／来客频率，不重构旧Counter算法。功能和代表现场由Agent完成，Owner只验新增队形／动作观感；旧三项visual PASS保持。

让玩家在有效布局中看到第一个自动运行的外带循环：

```text
顾客入店 → Counter FIFO → Cashier 服务 1 游戏秒
→ 付款成功并创建一张基础 Coffee Order → Pick-up FIFO 等候
→ Barista 按 Order FIFO 制作 3 游戏秒 → 持杯送达
→ 正确的队首顾客实际取餐 → Order Completed → 顾客实际离店
```

包括：两个固定岗位、每次到店一单一杯、真实 Navigation、订单与杯子 ownership、容量生命周期、只读状态 UI、Blocked 恢复、装修清场，以及 Done 后自动接待。

不包括：Coins、价格、Revenue、实际资金 transaction / refund、Inventory、Recipe/Menu 编辑、多商品、多员工调度、优先插单、顾客耐心/超时取消、全新取杯／喝咖啡动作、营业日/打烊/Report、Save/Offline、正式 mobile 适配。Owner于2026-10-20验收反馈后批准复用已有Carry动作的顾客持杯离店，纳入本阶段；P15/P16/P17其他内容不提前实现。

来源：

- [Game Design](../../AnimalCafe_Project_Design.md)：3.1–3.4、10.7–10.9。
- [Roadmap](../../AnimalCafe_Development_Roadmap.md)：P9–P14 dependencies 与 P14 scope；营业日属于 P15，Economy 属于 P16。
- [Phase Development Process](../../AnimalCafe_Phase_Development_Process.md)：Task focused、Phase 集中验收。
- [P9 Order design](2026-09-28-phase-9-order-domain-design.md)、[P10 Capacity design](2026-09-29-phase-10-capacity-reservation-design.md)、[P12 Customer design](2026-10-04-phase-12-customer-queue-design.md)、[P13 Employee design](2026-10-13-phase-13-employee-task-flow-design.md)。
- Owner 在本 chat 的六项决定及“已取餐离场顾客不清空”的修正，优先于旧文档中冲突的清场规则。

基线：`8c1582f7388191ff155d63b824b69c10d47cff3c`；Unity `6000.5.5f1`；工作区 `E:/Unity/Project/AnimalCafe/.worktrees/phase-14-integrated-takeout-loop`，branch `codex/phase-14-integrated-takeout-loop`。

主 checkout 的 P13 merge receipt 已核对；Roadmap 中部分合并前文字仍是历史状态。P13历史CI/验收不算P14 PASS；当前living documents及Unity执行结果按配套plan/Validation Report持续更新。

## 2. 已确认的六项决定

| 编号 | Owner 决定 | P14 的边界 |
| --- | --- | --- |
| D1 | 收银动作后记录付款成功并创建 Order，防止重复付款 | 不计算金币、价格或收入；这是本地业务状态，不是资金扣款 |
| D2 | Pick-up 单列 FIFO | 队首只能取自己的 Order，后客不能越过；实际前移与避让由 Navigation 执行 |
| D3 | 每杯绑定 Order，桌面最多显示三杯，空位后补显示 | 超出三杯的已交付记录不丢失，不创建隐藏桌杯；正确顾客到位后即时取走，随后一份Visit拥有的手杯配合已有Carry动作离店，不另加耗时取杯动作 |
| D4 | 确认装修修改后的清场，保留已取餐离场顾客 | 清空其他顾客、当前全部订单、桌杯/员工手杯及旧任务；保留已取餐者原Actor/手杯/剩余token及员工身份岗位；只看/Cancel Preview 不清场 |
| D5 | 短暂受阻先等待，失败后保留现场并手动恢复 | 不重复收银、制作、交付；不无界 retry，不自动超时取消或退款；无完整服务组合时停止新接待 |
| D6 | 装修 Done 后有效布局直接开始自动接待 | 不需要“开始接待”按钮；主动 Pause 保留；无效布局保持暂停并给原因 |

本阶段不新增“开始/停止接待”的正式产品按钮。恢复入口属于恢复操作，状态 UI 本身只读取 runtime snapshots。

## 3. 整合结构与身份

采用一个 Cafe Loop 协调层连接现有 Order、Capacity、Customer、Employee、Navigation 和 Decoration；不让 UI 或验证工具成为第二套业务写入口，不引入全局 event bus 或新 singleton。

P14 模式下协调器是唯一业务 Step 驱动者：Customer/Employee 的自动 Update 不再各自推进，旧 P12/P13 手动离场/派单按钮不能同时写 P14 状态。NavigationWorld 仍是唯一角色位移执行者，保留自身更新。未安装 P14 的旧场景继续使用原 Update 和受控测试入口；不能以 disable 整个旧控制器代替模式切换，因为它会触发已有 cleanup。

### 3.1 各层责任

| 层 | 责任 |
| --- | --- |
| Cafe Loop 协调层 | 付款/Order/Visit/task 对应关系、业务调度、一次性里程碑、恢复及清场顺序 |
| OrderService | 唯一 Order 状态写入口；保留 P9 六状态与 ClaimNext FIFO |
| Customer flow | Counter/Pick-up 实际站位、实际到达/离位、取餐与出口移动；不自行判定制作成功 |
| Employee flow / task runner | 固定岗位、工作时长、真实持杯/送杯/返回；输出带身份的任务结果 |
| CapacityService / admission | owner/token 的 reserve、occupy、release；实际事件由协调层连接 |
| Cup display | 已交付且未领取的 Order 展示投影，不拥有订单库存或付款状态 |
| Status UI | 只读显示状态与失败原因，不因刷新创建/领取/完成 Order |

### 3.2 身份和过期事件

- Visit 使用稳定、唯一的 runtime Visit ID；不拿 Scene object equality 当业务身份。
- 业务引用包含 session、Order epoch、Order ID、Visit ID、employee ID、task handle / generation；涉及移动时还验证 request ID 和 layout revision。
- 装修可清空 Order store，但必须退休旧 epoch。即使新 store 从相同数字 ID 开始，旧 callback 也不能影响新订单。
- 保留的离场顾客保留原 Visit ID、Actor 和 admission owner，不因新 Order epoch 重新生成；旧 Completed Order 清空后仍能独立离店。
- callback 可能同步触发；先建立上下文再调用外部 API。清场/取消先退休上下文，再执行外部 cleanup；重复或重入 callback 无副作用。
- 不在每次 Update 重复提交业务事件；从 snapshot 观察同一成功状态，也只消费一次。

## 4. 正常服务与 FIFO

### 4.1 接待与收银

沿用 P12 的 1–5 游戏秒随机到店、到期保留一位、无批量补发、唯一 Counter FIFO、真实空间门禁及碰撞保护。P10 三项 reservation 仍在入店前原子申请。

只有实际到达 Counter 队首的顾客才能触发 Cashier。Cashier 使用与顾客相同 CR 的 Employee Side，P14服务1游戏秒（原P13独立默认2秒保持）；服务中保留队首归属，不能将后客误收费。

开始收费前检查：必要设备和完整服务路径有效，基础 Coffee 可服务，有安全可分配的 Pick-up 等候位置；设备忙不等于不可服务。逻辑 Pick-up reservation 不替代真实站位和路线检查。空间不足时保留顾客原站位，不把它推入无站位区域。

检查不是一次只读查询：收银开始前为该 Visit 独占一个真实 Pick-up 站位分配，付款提交或恢复时验证该分配仍有效；拒绝服务/清场时只退还该 Visit 的物理分配。不能让多个 Counter 服务同时以同一个“空位”收费。

Cashier 完成服务后，以该 Visit 的一次性事件提交付款成功与一张 Coffee Order。若创建失败，不留下“已付款但没有订单”的半完成状态，也不允许下一帧生成第二张 Order；显示失败并在明确恢复时重试提交。这里不涉及外部资金系统。

P14 每次 Visit 只有一单一杯；这是整合层规则，不收紧 P9 可为同一 customerId 创建多单的通用 API。

### 4.2 Pick-up 队伍

- 取餐队伍按成功点单顺序加入，单列 FIFO，以选中Pick-up的合法等候头位向后延伸安全站位。等候头位和本次实际领取侧分别拥有明确目标；备用侧不会建立额外隐形队伍。
- 等候朝向沿同一slot链：队首朝既定取餐方向，后客朝前一个等候位；初次到位与每次前移都按当前slot更新。合法队列因空间转弯时，后客不能仍统一面向柜台而呈现肩并肩。实际领取另用本次选定服务侧方向，沿用原身体／路径安全规则。
- 逻辑容量和物理站位都必须允许；所有排队等候者均计入 Pick-up reservation，不新增不计容量的隐藏等待队伍。
- Counter 顾客走向已分配的 Pick-up 站位；实际离开 Counter 才释放 Counter，不因 Order 创建就提前释放。
- 收银完成／订单成功创建后同次推进即请求转队，不等Waiting／Preparing订单变Ready；真实空间门禁仍保留。需用实际收银里程碑、原客持续移动及真实Counter腾位验证，不能仅凭移动>.1m起步掩盖随后在转角等待内部重算。默认Shared空通道首客应在Barista仍Working时完成Counter实际释放；有前客时先去已分配队尾再前移属于正常FIFO。
- Pick-up 顾客可在自己的咖啡送达前等候。队首未 Ready 时不跳过；后客即使 Ready 也不能越过队首。
- Owner于2026-10-20明确批准多侧交互：枚举Pick-up所有外露且可达方向；和桌子连着、墙／家具遮挡的内侧不参与。员工交付和顾客领取按具体侧占位，不将整个Pick-up锁给一个人；Barista占一侧时，已Ready队首可从其他空侧实际到位领取，不等员工回CM。
- 同侧互斥，必须实际离位才释放；不同侧仍保留完整身体间距及路径验证。只有一侧可用时，顾客在合法wait位等员工实际让开。选择可达空侧后，本次交互的目标、generation/request/revision保持一致，旧侧回执不能提交新侧；Pause／disable／Done按原生命周期退休或保留owned资源。
- 选领取侧也须避开其他顾客已经发出的完整剩余路线，不能把后客正在经过的绕行点抢成新服务目标；有冲突时保持队首等待或选择另一合法侧。通过检查的同一路线才交给实际逐段移动，不在检查后重新换路。
- “可达”贯穿P14 readiness、wait头位预留、员工选边及实际往返路线；旧首选侧不连通不能否决其他完整可服务的候选。Preparing时的wait位不得堵住唯一可交付侧，静态单侧与部分侧被动态身体占用均遵守此规则；沿用原身体／路线安全检查及有界恢复。
- 拥挤时仍保留原wait目标：原有限规划未找到路线时，可把既有正式交互侧作为最多四个绕行候选，逐段核验同一全量身体／owned障碍和完整native路径，再由原移动逻辑实际经过；不扩展入店、Counter或离店规划，不增加waypoint／segment／replan预算。
- 保留旧每role一对anchor接口供独立旧模式使用，P14额外消费合法侧候选。按侧分配不更改三类capacity/FIFO，杯子仍归原Order和Surface Slot，Completed不以员工返回为条件。
- 等候位也属于真实队列与容量；无可用合法站位/完整往返路径时明确不可用，不靠传送、穿人或额外隐藏站位维持营业。
- Owner于2026-10-22批准送杯拥堵补修：队列规划不能只避开交互点，应至少保留一条经完整身体／Nav验证的CM到合法交付侧及返回通道。站位不足时限制物理分配，不修改三项逻辑容量、不先收费再把顾客挤进无安全位置处。
- 出发前先检查现有外露侧及有限绕路。若所有可验证通路被本系统已站稳等候客人占住，可暂停相关普通前移并在有限预算内安排安全让位；客人必须实际走到合法临时目标，不改Paid FIFO、Visit／Order／原capacity token，不伪造Arrived或释放容量。临时让位不是新队伍；旧request／generation不能提交新目标，已选送杯侧及活动lease不被途中换侧。
- 正在正常穿行／腾位的角色只触发暂时等待，不因一帧冲突立即Blocked。让位没有安全空间、静态布局无完整通路或有限尝试已耗尽时，明确报告“送杯通道堵塞，请调整布局”及具体原因并进入可恢复Blocked；不无限静默等待、不每帧无界重规划，也不能隔空交付。咖啡保持原制作进度，不重复收费／制作／提交Ready。Pause／disable／Done仍沿原生命周期与清场规则处理。
- 临时交通也包括当前有效收银任务的真实队首：仍有原Pick-up预留、当前session／epoch的Cashier确实Working且有有限剩余时长，或当前Work已验证的ServiceFinished仍在canonical队列等待消费。只在完整反事实通道证明该客人是相关阻挡者时等待，不据此派发穿人；完成事件消费、失去原owner／取消／停用后不能继续充当临时交通。任意Queued、任意Finished或固定宽限帧数不构成此事实；P14收银按最新批准1游戏秒。
- 本阶段一组活动服务组合；多台设备沿用 P13 的稳定且可达选择规则。任务内不临时换站，多组并行调度不在范围内。

### 4.3 制作、送达、领取和返回

Barista 空闲且已实际回到 CM 后，才领取 OrderService 的下一个 Waiting Order。按 P9 `Waiting → Claimed → Preparing` 推进，制作 3 游戏秒。

Claim成功但实际任务派发被拒时，保留同一Claimed Order与初始checkpoint并明确Blocked；恢复只重派该单，不重新Claim、改FIFO或每帧无界重试。同步成功/失败callback先校验已建立的任务上下文，不能由调用返回后的赋值覆盖新状态。

制作完成的业务事实与杯子视觉分开保存。Barista 持杯走到绑定 Pick-up；商品还在途中时仍为 `Preparing`。实际成功送达后才 `ReadyForPickup`，并建立一次性、带 Order key 的 delivery 记录。送达与 Order 提交失败不能留下可领取但没有对应 Order 的杯子。

P14交付侧确定后，若直达native路径被身体挡住但存在完整安全绕路，可在原有限预算内实际逐段行走；每段发出前重新检查当前真实位置、完整native路径及全部owned障碍／顾客remaining route。中段到达不交付，最终侧与lease／handle保持固定，只有末段真实到达才提交。P14实际消费同次验证的固定返程各段，只有最终实际抵达CM才Returned／Idle；显式恢复Returning时从实际位置重新认证返程，不重复送杯。下一返程段仅被相关真实活动交通短暂挡住时，保留原目标等待，交通结束后仍通过完整gate才发请求；Pause不查询／发请求，Cancel／退休清除待发目标。Pause／同revision导航恢复继续当前段，退休清理整条路线；旧P13默认调用保持原规则。

只有 Pick-up 队首、Visit ID 正确、Order 为 Ready、实际抵达合法取餐位置，且时间允许推进时才取走。先在同一业务提交中使 Order `Completed`、移除逻辑 delivery 并记录该 Visit 已取餐，再移除/刷新杯子视觉；不能先销毁杯子再尝试 Complete。业务拒绝时上述事实全部保持，失败保留可恢复状态，不丢杯或重复完成。

取走后即时持杯离场，Order Completed不等待动画或实际离店。顾客领取前没有手杯；确认领取后才创建一份Visit拥有、无碰撞的手杯，复用Shiba／Westie已有Carry动作叠加真实位移驱动的Walk／Hold，不新增取杯等待或喝咖啡动作，不改变导航／身体／容量。Pause冻结动画，Blocked仍保留杯子；disable／reenable与重复恢复不重复杯。跨正常修改Done保留已取餐Actor时同时保留其原手杯，即使旧Order已清空；真实离店随Actor清理。表现失败不撤销已提交Completed或重新发货，保留诊断供修复。Barista送达后空手返回CM；返回途中仍忙。顾客可在Barista返回时取餐，返回失败不撤销已经成功送达或取走的商品。

接线使用两份`Assets/Art/Phase14/Customers/PF_*_P14Customer.prefab`：复用P13已验收完整Carry模型/骨架与原Nav/body配置，移除EmployeeVisualPresenter，P14顾客单独驱动表现。P11旧模型缺相关socket，故不在其上假定动画绑定完整；旧P11/P13资源不改，不新增动画clip或runtime换模型。

### 4.4 提交与外部副作用

付款、交付和领取的成功点均在协调层的单线程业务提交内：先验证 session/epoch/owner/状态/已完成标记，准备逻辑变更，调用合法 OrderService API，再提交付款/delivery/已取餐记录。提交中不调用外部 ports、UI、Instantiate/Destroy 或对外 callback；完成一致状态后才通知移动/视觉。OrderService 拒绝时不发布任何关联事实。可预期失败必须在变更前检查，避免用不可逆业务状态回退充当补偿；不承诺进程崩溃持久化或任意内存异常下的 transaction。

真实送达先通过当前工作位置/Slot/互斥权限验证，再提交 Ready+delivery；视觉刷新失败只标记视觉待修复，不重做已送达Coffee。取餐后的视觉移除失败也只刷新展示，不能把Completed退回Ready或重复领取。下一次渲染以delivery投影为准，不将遗留可见对象解释为可领取库存。

## 5. 杯子展示

- 按正式 Pick-up / Surface Slot 保存已送达、未领取的 Order keys；同一订单至多一个 delivery 记录。
- 选择最早交付的前三个未领取 Order 显示为三角形，保留 P13 的局部 offset、尺寸、材质、无导航阻挡设置。
- 第四杯以后只增加 delivery 数据，不增加隐藏/禁用 GameObject、不拒绝 delivery，也不修改 Capacity 上限。
- 领取时移除正确 Order 的记录；若它可见，释放对应展示并补入最早未显示记录。不能只把总杯数减一而丢失 ownership。
- 新显示杯的视觉创建失败时显示可诊断原因，保留真实 delivery，不复制订单或重复制作；业务领取不以视觉对象存在作为 ownership 依据。
- 正式支撑变化处理按第 8 节清场，不再把旧 P13 无 Order 的展示保留到新营业批次。P13 独立受控验证模式保持原合同。

## 6. 容量与真实位置

| 实际事件 | Counter | Pick-up | Total |
| --- | --- | --- | --- |
| 入店前接纳 | Reserved | Reserved | Reserved |
| 顾客实际入店 | 保持 | 保持 | Occupied |
| 实际抵达 Counter 队伍 | Occupied | 保持 | 保持 |
| 点单成功但尚未离位 | 保持 | 保持 | 保持 |
| 实际离开 Counter 站位 | Release 一次 | 保持 | 保持 |
| 实际抵达 Pick-up 等候位 | 已释放 | Occupied | 保持 |
| 实际取餐，Order Completed | 已释放 | 保持，直到实际离位 | 保持 |
| 实际离开 Pick-up 位置 | 已释放 | Release 一次 | 保持 |
| 实际出口 Arrived | 已释放 | 已释放 | Release 一次 |

保留 P10 的面积计算、Reserved+Occupied 用量、缩容保留旧 owner/额度与停止新接纳规则。停用组件或 Blocked 不等于实际离店，不得 Dispose 正在店内的 admission。

装修清除未取餐顾客是 Owner 明确批准的强制清场例外，可以清理这些 Actor 并释放其 token；保留的已取餐离场顾客不适用例外。不能对整个 CapacityService Reset 或新建一个空实例来抹掉它们的 Total 占用。

## 7. Blocked 与恢复

短暂挡路先沿用现有 Navigation 的有限避让/恢复；实际失败时保存业务进度并显示原因，不把暂时受阻变成 Order `Failed`。P14 不新增每帧无界 retry。

Pick-up绕行点已实际到达、下一段尚未发出时，如果完整owned路径只被正在实际移向通道外的活动身体暂占，则保留原candidate原地等候；实际全body检查通过后才提交下一段。等候不消耗segment／replan预算，也不每帧A*重算；静态不可达、正式预留目标或停止／取消后仍占位的身体不适用此门禁，回到原有有限重算与Blocked流程。Pause、owner hold、装修及显式恢复仍遵守原生命周期；已发出的Navigation请求失败合同不变。

恢复通过明确入口（沿用/扩展 Inspector 验证组件的恢复菜单）执行；状态 UI 无派单、完成订单或付款按钮。每次恢复先验证当前设备、路径、队首/Order/handle 身份，再从实际位置重提移动或继续尚未完成的工作。

| 中断位置 | 必须保留的事实 | 恢复要求 |
| --- | --- | --- |
| 收银尚未完成 | Visit、服务进度、未付款状态 | 不给后客创建订单，不把已完成里程碑重做 |
| 已点单、等待制作 | 原 Order、付款状态、FIFO 位置 | 不重新 Create/收费，不插到队尾 |
| 制作中 | 剩余游戏秒、原 Claimant/Order | 恢复剩余制作，不从头重复制作 |
| 已制作、送达前 | 已制作事实、未送达状态 | 即使 P13 cleanup 已移除手杯，也只重建视觉并续送，不再次工作 3 秒 |
| 已送达、员工回位失败 | delivery/Ready、员工实际位置 | 顾客仍可合法领取；只恢复员工回位，不重送 |
| 顾客取餐前移动失败 | 原排位、Order、容量 | 原位置重寻路，不让后客越过，不自动释放 |
| 已取餐离场失败 | 已取餐事实、Total、实际位置 | 独立恢复离场，不依赖已清空的 Order store |

P13 当前失败会退休 handle、清理未交付手杯并清空计时；因此不能只调用现有 `TryStartTask` 就声称恢复成功。P14 需要业务 checkpoint 与受控恢复边界，既不复活旧 handle，也不重复业务动作。

没有完整有效服务组合时暂停新接待，保留店内状态。局部受阻不要求冻结所有无关角色；已有安全出口路径的顾客可以继续离开。若现有全局 readiness gate 持有 Pause，则如实显示并等待修复，不能绕过 gate。严重受阻可由玩家确认装修修改执行第 8 节清场。

## 8. Decoration、清场与自动接待

### 8.1 不修改的装修

打开 Decoration Mode 自动 Pause。仅查看、移动 Preview 后 Cancel、或没有正式 Layout 修改就 Done：保留顾客、Order、task 业务进度、杯子和队伍；恢复原流程。玩家主动 Pause 的选择不被 Done 覆盖。

### 8.2 已确认修改后的 Done

沿用 `DecorationModeController.CompletedLayoutChangeVersion` 的“完成一次含正式修改的装修”边界，不在进入 Decor 或每次单家具 Confirm 时反复清场。但不能仅在下一帧观察version才清场：现有代码在PrepareExit和暂停释放之后才增加该version。

实施时增加一个可选的同步准备通知，在已经确定本次为含修改的正常Done、仍持有装修Pause、尚未PrepareExit前触发。P14收到后先取得自身resume block，再退休旧epoch和执行下列选择性清场；新version发布、清场一致、导航与布局验证完成前不得释放该block。旧场景无P14订阅时保持原流程；重复通知按目标version幂等。清场失败时保留block和原因，不能以恢复按钮盲目放行。

1. 装修期间保持 Pause；家具确认仍执行既有碰撞/身体保护，不允许把家具确认到保留顾客或员工的身体上。
2. 退出含正式修改的装修时，先退休旧订单/task/delivery 的业务 epoch，使旧 callback 无效。
3. 对**尚未实际取餐**的顾客清场，包括 Counter、去 Pick-up、等候、Blocked 顾客；释放各自 owned tokens、站位和请求。
4. 清空**当前全部订单记录**，包括 Completed；清空所有关联桌杯／员工手杯及旧业务任务。员工身份、岗位和Actor保留，从实际位置重新验证并回工作位；已取餐顾客持有的杯子由该Visit独立拥有，不属于旧delivery投影。
5. 对**已实际取餐**且尚未到出口的顾客，保留Actor、Visit ID、已取餐事实、原手杯与剩余admission。包括走动中、Paused、disable-held或离场Blocked；不能只看当前`Exiting` enum，因为受阻时可能显示`Blocked`。
6. 退休这些保留顾客的旧移动请求，按新 layout revision 从实际位置验证出口路线；有路则正常离开，没路则明确 Blocked 等待修复，不传送、不删除。
7. 保留同一 CapacityService 及上述顾客仍占用的额度，刷新新 Floor limits。实际到出口后只释放它们自己的剩余 token 一次。
8. 有效布局且没有主动 Pause 时自动接待，新客仍按完整 1–5 游戏秒随机间隔到来，无批量补发；布局无效保持暂停并显示具体原因。无需额外“开始接待”。

装修清场属于暂停期间允许的生命周期操作；第7节/测试中“Pause不推进业务”指禁止正常收费、制作、交付、领取和生成，不禁止这次明确批准的退休/清理。清场后的离场路径只在version发布并允许游戏时间推进时真正开始移动。

有效初始布局沿用自动接待能力；空店先完成必要布置，Done 后进入同一 gate，不要求先点 P13 的 Enable Customer Arrivals 菜单。

### 8.3 营业中家具保护

经营状态不提供直接移动/旋转/Store 必要家具的旁路；操作统一进入 Decoration Mode 并 Pause。允许通过装修删除必要设备并保留该布局，但无效时不能恢复营业，仍允许再次进入装修修复。P14 不引入必须先打烊的 P15 流程。

## 9. 状态 UI 和验证入口

只读状态 UI 至少提供：接待/暂停/装修/Blocked 原因；Counter 与 Pick-up 排位/Visit ID；当前 Order key、owner 与 state；两员工 role/state/current Order；桌面可见杯数与已交付未领数量；三种 Capacity used/limit。可折叠/简化排版，最终视觉由 Owner review。

不显示虚假的 Coins、Revenue、营业日或库存。显示 ID 帮助查错属于验证信息，不能用 ID 输入框进行付款/领取。正式 UI 的排版不是本轮写文件的交付物。

实现阶段提供一套明确标记为验证用途的组件菜单，支持恢复、暂停自动领取以构造 >3 杯、可逆的运行时阻挡与错误 callback 注入、读取 snapshots。具体 GameObject、菜单名和准备步骤在 test cases 中定义为拟建接口，当前并不存在；不能把“菜单尚未实现”记录成用例 FAIL 或 PASS。

## 10. 预期改动边界、兼容与风险

当前实际文档范围为本design、配套test cases和implementation plan。计划列出新增协调层、Pick-up queue、业务snapshots和对应tests的精确文件清单；这些功能文件尚未创建。

已确认需要评估的现有文件：

| 文件 | 后续用途与影响 |
| --- | --- |
| `Assets/Scripts/Customers/CustomerFlowController.cs`、`CustomerContracts.cs` | 增加真实服务/取餐阶段与选择性清场；P12 独立验证仍保留 |
| `Assets/Scripts/Customers/CustomerAdmission.cs` | 当前只有 Counter 离位与取消未用 Pick-up；需补实际 Pick-up occupy/leave 的幂等边界 |
| `Assets/Scripts/Employees/EmployeeFlowController.cs`、`EmployeeTaskRunner.cs`、`EmployeeContracts.cs` | 加入 Order key、可靠里程碑、进度 checkpoint 与恢复；保留原受控任务模式 |
| `Assets/Scripts/Employees/PickupCupDisplay.cs` | Order-aware 交付与取走，兼容 P13 的独立视觉验证 |
| `Assets/Scripts/Orders/OrderService.cs` | 优先复用现有 API，整合层不能绕过状态机；不预先增加通用取消/退款/任意 SetState |
| `Assets/Editor/Phase12/Phase12CustomerQueueSceneSetup.cs`、`Assets/Editor/Phase13/Phase13EmployeeTaskSceneSetup.cs` | 核对 MainCafe builder 的共存/幂等与 P14 接线，防止重复控制器；不在文档阶段改 Scene |
| `Assets/Scripts/Decoration/DecorationModeController.cs` | 在正常含修改Done的PrepareExit前提供可选准备通知，供P14持有清场门禁；无订阅时旧时序不变 |
| `Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md` | 实施时同步获批装修例外和当前状态，旧 spec 保留历史，不把 P14 标 Completed |

无 Save schema migration；现有 P9/P10 domain contracts 尽量保持。主要返工集中在 P12 清场、P13 failure cleanup 与 cup ownership，必须回归其旧模式。禁止覆盖已完成导航的 no-teleport、身体尺寸、碰撞、skin 或暂停合同。

主要风险：单列 Pick-up 与员工争用同一位置导致死锁；清空订单后离场顾客丢失容量；旧 callback 命中新 epoch；Ready 后返回失败重复送杯；Pause 期间消费 callback；杯子显示上限误当库存。每项在 test cases 中有对应 E/P/M 场景。

## 11. 验证与下一阶段

配套：[P14 Test Cases](2026-10-19-phase-14-integrated-takeout-loop-test-cases.md)。Task 按 RED → 最小实现 → GREEN → 直接回归执行；Phase 末集中完整 EditMode/PlayMode、真实场景、Engineering/QA 和必要视觉 review、Owner manual acceptance。

至少连续完成 20 个真实 automated Orders，另独立记录 manual 所观察的数量。所有 P14 runtime 证据当前 `NOT_RUN`；P13 PASS 不继承。Player/device 单独报告，不由 Editor 证据替代。

Owner 已要求先review design/cases后写plan，因此本修订供配套 [Implementation Plan](../plans/2026-10-19-phase-14-integrated-takeout-loop.md) 使用；plan待Owner review后才实施。未执行 commit、push、PR、merge、CI 调度或其他 Phase 工作。

## 12. 本轮review修正

| Finding | 修正与验证要求 |
| --- | --- |
| R1：Done先恢复后发布version，下一Update清场会出现窗口 | §8.2增加PrepareExit前同步通知与resume block；E-033/P-021/M-021 |
| R2：原文先移杯再Complete，拒绝时会丢杯；视觉与业务成功点混杂 | §4.3–4.4规定先无外部callback业务提交，再视觉投影；E-034–035/P-022 |
| R3：多控制器Update与旧验证按钮可能重复推进/写入 | §3明确唯一Step与独立旧模式；E-036/P-023/M-022 |
| R4：收银前只查空位，未独占真实Pick-up站位 | §4.1明确按Visit分配；E-037 |
| R5：disable会触发P13 cleanup，需要先保存checkpoint | §7与plan明确保持里程碑、剩余时间；E-038/P-024；旧模式不改 |
| R6：Hold Collection在共享anchor时可能堵住后续交付 | test准备明确等待真实合法站位且不占共享服务点，另提供分离anchor fixture；E-039/P-024 |
| R7：正常Done、shutdown与session teardown应分开 | 准备通知仅正常含修改Done；退出Play清整个session，保留离场例外仅适用装修；E-040/P-018 |

这些是已确认玩法的技术一致性修正，不新增价格、退款、timeout、岗位或营业日决定。本轮review和文档检查不构成runtime PASS。

独立review最初提出2项Important（R1/R2）和1项Minor（R6）；修订后已逐项只读复核，在文档层面全部CLOSED。R3/R4/R5/R7为作者核对现有代码时补充的实施边界。独立复核未涵盖implementation plan；plan由作者按writing-plans流程自检，等待Owner review。
