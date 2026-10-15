# Phase 13 — Employee Task Flow Test Cases

> 状态：Approved Test Design — Owner 于 2026-10-14 确认开始并使用 subagent，按 [Implementation Plan](../plans/2026-10-13-phase-13-employee-task-flow.md) 执行；自动与 manual 用例的设计批准不代表已执行或通过。
> 设计日期：2026-10-13；执行更新：2026-10-14。以下表格保留批准的验收要求；自动 tests 已实现，实际执行与逐项 coverage 见 [Validation Report](../../Phase13_Validation_Report.md)。Owner manual 仍全部 `NOT_RUN`。
> Task 阶段 focused 验证；Phase 收尾完整回归。代码、场景与 platform 证据分开报告。

当前已设计 **38 项 EditMode、18 项 PlayMode、16 项 manual** 验收场景。ID 是需求场景，不等同于最终 NUnit test-case 数；一个场景可展开为多个参数化 tests。覆盖映射见 plan，各项执行结果在实际实现后填写。

## 1. EditMode：状态、binding、任务与时间

| ID | 场景 / 输入 | 预期 |
| --- | --- | --- |
| E-001 | 初始化两名员工 | 唯一稳定 ID、固定 Role、独立状态；不复用 Customer visit ID |
| E-002 | Cashier 服务 | 正确 CR Employee Anchor、2 个游戏秒 working、服务成功后原地 Idle |
| E-003 | Barista 制作/交付 | CM 工作3秒 → carrying到Pick-up → 放杯成功 → 空手返回 → 实际到位才Idle |
| E-004 | 工作/送杯/返回时重复点击 | 第二任务拒绝；同一员工最多一个 active handle，无自动积压队列 |
| E-005 | 同时触发两岗位 | 两名员工独立推进；各自只清理自己的请求与手持杯 |
| E-006 | 多台 CR/CM/Pick-up，打乱列表顺序 | Cashier 与 P12 SelectedRegisterId 一致；CM/Pick-up 稳定选择可达组合 |
| E-007 | 唯一可用候选缺少 capability、Anchor 或完整路径 | 明确失败，不用错误 station、无限等待或提交成功 |
| E-008 | 旧 station 在任务中失效 | 不临时切换设备；退休任务，按规则返回或 Blocked |
| E-009 | 分段 scaled delta 恰好达到2/3秒 | 只到期一次；帧余量不触发第二任务或重复交付 |
| E-010 | 零 scaled delta / Pause | 所有 task 时间不推进、不放杯、不开始新受控任务 |
| E-011 | 1x/2x 实际时间 | Cashier 2/1现实秒，Barista 3/1.5现实秒；不重复乘speed |
| E-012 | 非有限/负delta、无效duration配置 | 明确拒绝或按约定防御，不产生NaN、提前成功或永久等待 |
| E-013 | MoveTo 同步成功/失败callback | generation/handle先建立；状态不被调用后的赋值覆盖 |
| E-014 | 过期generation、request ID、layout revision callback | 不改变当前任务，不放杯，不把已取消任务恢复为成功 |
| E-015 | Recovered、中间路点Arrived、超时、取消 | 不作为最终交付成功 |
| E-016 | 重复cancel、cancel后callback、双重disable | cleanup exactly-once；同杯/同请求不重复提交或清理 |
| E-017 | 工作或送杯阶段取消 | 未交付手持杯移除；有效岗位可步行回位，否则明确Blocked |
| E-018 | 已放杯但回CM失败/返回时cancel | 保留交付成功与桌面杯；回位失败单独可见，不伪报Idle |
| E-019 | 布局修复后恢复 | 从真实位置验证并回岗位；不瞬移/重生，无每帧无界retry |
| E-020 | Pause/Decor/Blocked时测试入口 | 拒绝新任务并说明原因；不新增正式Pause派单规则 |

## 2. 杯子与装修生命周期

| ID | 场景 / 输入 | 预期 |
| --- | --- | --- |
| E-021 | 同一module连续交付1、2、3、4、10杯 | visible count为1、2、3、3、3；第四杯起仍成功；无隐藏对象积压 |
| E-022 | 三杯展示位置 | 三个不同的家具局部offset构成三角形，不沿直线堆放 |
| E-023 | 多个module/支撑家具 | 展示归属正确，互不串杯；不自动生成额外Pick-up或家具占用 |
| E-024 | 工作站失败或未到达Pick-up | 桌面count不增加；没有业务成功或新桌面杯 |
| E-025 | 桌面已有三杯，再触发任务 | 不以展示饱和或Cup Collider拒绝任务，不改变P10 Capacity |
| E-026 | 进入Decor后未Confirm就退出 | 同handle/剩余时间/手持杯继续；桌面展示不清空 |
| E-027 | Confirm正式布局变化，多次Confirm后退出 | 旧任务cleanup一次；身份/岗位保留，旧callback无效，实际步行回新工作位 |
| E-028 | Confirm家具位置/旋转变化 | 桌面杯跟随支撑家具与Slot；不留在旧world坐标 |
| E-029 | 删除支撑家具、对应Pick-up或使Slot失效 | 只清理对应桌面展示；员工显示station不可用或重新有效绑定 |
| E-030 | Cancel装修Preview | 正式布局、binding、任务和杯子均不被Preview清理 |
| E-031 | P12 Decor batch reset | 只清理Customers；员工对象、稳定ID、岗位不被删除 |
| E-032 | 员工disable/re-enable/destroy | 旧任务不复活；disable期间身体保护保持；destroy只清理该员工owned资源 |
| E-033 | 新session收到上一session的callback | session/generation/handle均不匹配时忽略；旧杯或旧任务不复活 |
| E-034 | 第1/2/3杯的visual创建失败或Cup配置缺失 | 不增加display count、不清理后误报交付成功；失败后按规则返回或Blocked |
| E-035 | cleanup/cancel回调重入并再次提交 | 先退休旧handle再通知外部；拒绝同员工重叠任务，cleanup只一次 |
| E-036 | Pause/Decor期间意外收到到达callback | 不在零游戏时间放杯；暂停期间保存合法后续动作，恢复后只处理一次；取消后的不处理 |
| E-037 | 装修改变Pick-up绑定，旧对象仍未Destroy | 按正式Layout/InstanceId/Slot判断有效性，不能凭Scene对象存在继续交付 |
| E-038 | MainCafe空店→添齐设备→删掉设备 | 空店仍可正常装修；有效布局才生成员工/启用任务；失效不绕过原business gate |

## 3. PlayMode：真实 Navigation 与动画

| ID | 验证 | 预期 |
| --- | --- | --- |
| P-001 | 真实场景生成两岗位 | 正确工作位/朝向，初始身体不与角色/家具重叠；无安全位置时明确不可用 |
| P-002 | 两岗位working | 复用Work_Generic_Busy，2/3游戏秒内可见；不被Walk/Hold覆盖 |
| P-003 | Barista端杯走路 | 下半身Walk，上半身Carry；杯子在hand_socket.R，实际位移由NavigationWorld控制 |
| P-004 | 送杯遇动态阻挡 | 实际停下时不原地走；端杯保持；有界失败/recovery，无穿人/瞬移 |
| P-005 | 正确交付与返回 | 到达Pick-up后手杯消失、桌面增加一杯；返回普通Walk，到CM真实到位后Idle |
| P-006 | 饱和展示连续制作 | 第4杯及更多仍可完成；可见桌面对象最多3，无额外task或Cup leak |
| P-007 | Pause/2x与working/carry同时测试 | 动画/位移/计时一致；Pause全冻结，2x不重复缩放 |
| P-008 | station删除、layout revision与过期callback | 正确取消/Blocked；无错误交付，不把Recovered当Arrived |
| P-009 | Customer与员工共同在MainCafe运行 | 同一CR两侧正确；队列绕真实员工；P12 FIFO、admission、Total与原body guards保持 |
| P-010 | Decor未修改/确认修改/删除station | 分别继续原任务/退休旧任务回位/明确不可用；员工身份不被Customer reset删掉 |
| P-011 | Cup Prefab与展示Collider | 手持/桌面杯不阻挡导航或抢装修输入；原Cup Asset合同未被全局修改 |
| P-012 | 默认P11/P12角色动画直接回归 | 原Walk/Hold、实际速度驱动、暂停、碰撞与角色尺寸保持 |
| P-013 | 两种species、disable重新启用 | 骨骼/Socket/Mask正确，无丢材质、duplicate prop、原地walk或旧任务复活 |
| P-014 | 导出与导入资产 | Action/frame rate/scale/forward/loop/bone paths有效，母版未保存，原P11资产未覆盖 |
| P-015 | Domain Reload开/关分别重复进入Play两次 | 每次仅两员工、一套HUD、正确的新session；无旧callback/subscription、旧杯或重复初始化 |
| P-016 | 旋转Grid root、移动/旋转多Slot桌子 | 工作与交付位置转换正确；杯子在绑定module，三角形随家具旋转，不串到相邻Slot |
| P-017 | 缺Cup配置、交付visual创建失败 | Console/状态给出明确配置失败；未成功放杯不提交交付成功 |
| P-018 | 横屏/竖屏测试HUD、重复build、退出Play | 两任务按钮可点且不遮挡时间/装修操作；build不重复接线，不污染其他Scene或导入资源 |

真实 Scene integration 必须包含独立 P13 validation Scene 和 MainCafe 接线；unit/fake driver PASS 不代替实际角色 movement。所有运行同一时间仅允许一个 Unity test/build process。

## 4. manual 验收与Owner委托的场景代测

| ID | 简单操作 | Owner观察点 | 状态 |
| --- | --- | --- | --- |
| M-001 | 默认打开场景 | Cashier在CR Employee Side，Barista在CM前；朝向/体型/待命位置正确 | PASS（Owner） |
| M-002 | 分别按两个任务按钮 | Cashier working 2游戏秒；Barista working 3游戏秒，不突然切回Hold | PASS（Owner） |
| M-003 | 观察Barista送杯再返回 | 手中有杯、上身稳定、下身走路；放杯后空手返回，没有漂浮或重复杯 | PASS（Owner） |
| M-004 | 连续做至少5杯 | 桌面依次1/2/3杯，三杯三角形；之后仍只显示3杯且可继续制作 | PASS（Owner） |
| M-005 | 制作/送杯时按Pause、1x、2x | 工作/携杯/移动一起冻结或加速；暂停期间测试按钮给出明确反馈 | PASS（Codex场景代测） |
| M-006 | 任务中进装修，不确认修改就退出 | 原任务继续，剩余计时与手杯保留 | PASS（Codex＋Owner复验） |
| M-007 | 任务中确认移动/旋转相关家具 | 员工不消失，旧手杯清理，按新位置实际回岗位；桌面杯跟随家具 | PASS（Codex场景代测） |
| M-008 | 删除station、堵路，再修复 | 明确无法工作；不穿墙、不瞬移、不无限原地走；修复后能回位 | PASS（Codex场景代测） |
| M-009 | MainCafe与P12顾客同场 | 两侧岗位与队伍正确、人物不整身互穿、员工不随顾客reset删除 | NOT_RUN |
| M-010 | 检查Console | 没有本阶段新增Error、MissingReference或持续异常日志 | PASS（Codex场景代测） |
| M-011 | 连点与两个员工同时工作 | 各员工只持有一个任务；重复点击给反馈；两岗位互不取消 | PASS（Codex场景代测） |
| M-012 | 多台设备与绑定失效 | Cashier与顾客同CR；Barista目标固定；装修后稳定重新绑定，不在任务中换设备 | PASS（Codex场景代测） |
| M-013 | 制作/送杯/已放杯返回时取消 | 未交付手杯清理；已放杯不撤回；员工实际回位或明确Blocked | PASS（Codex场景代测） |
| M-014 | Inspector停用再启用员工 | 旧任务取消，身体留原位；重新启用先验证回位，不瞬移或复活旧杯 | PASS（Codex场景代测） |
| M-015 | 两种Domain Reload设置重复Play | 无重复员工/HUD/杯子/旧任务；设置最后恢复 | PASS（Codex场景代测） |
| M-016 | 空店装修与多Slot/横竖屏回归 | 空店可装修；杯只落在绑定module；按钮与既有UI仍可用 | NOT_RUN |

### 4.1 通用准备与恢复

Scene、菜单和 Inspector helper 已创建；以下为批准的验收脚本，实际入口和可重复布置说明见 [Beginner Guide](../../Phase13_Beginner_Guide.md)。2026-10-14 Owner委托Codex完成M-005～008、M-010～015共10项，实际场景代测PASS；Owner已确认M-001～004及M-006 PASS，M-009/M-016仍NOT_RUN。代测使用真实Editor及既有场景API，不冒充物理鼠标操作或Owner观感接受，逐项证据见Validation Report。

1. 在 Unity Hub 打开 P13 worktree：`E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`，使用 `6000.5.5f1`。不要用主 checkout 验收未合并的 P13。
2. 等待导入/编译结束，保存当前自己编辑的 Scene；退出 Play。实现完成后使用 `Tools > AnimalCafe > Phase 13 > Create Employee Task Validation Scene`，打开 `Assets/Scenes/Validation/Phase13EmployeeTasks.unity`。
3. 在 Console 记录进入 Play 前已有的 warning/error；不要通过 Clear 隐藏本阶段新增异常。验证场景提供 CR、CM、Pick-up 和两员工；默认关闭自动进客，M-009 单独测试顾客同场。
4. 按 Play，选择 `1x`。任务按钮名为“收银服务”“制作咖啡”；状态区显示两岗位状态、剩余游戏秒和当前module可见杯数。
5. 每个独立用例从退出/重新进入 Play 开始，恢复默认设备与0杯。M-004连续制作期间不要重启；M-006/M-007必须在同一次Play观察恢复行为。
6. 布局操作用现有 Decoration UI 完成，先选空位，避免将家具移动到暂停员工的身体上；如果被身体保护拒绝，这不是P13失败。取消/停用用例使用下述Inspector helper，不需要改源码。
7. 每项记录 `PASS / FAIL / BLOCKED`、观察结果与必要截图。工具或配置不足写BLOCKED，不把未执行写PASS。完成后退出Play，不保存运行时测试修改；M-015还须恢复进入Play设置。

### 4.2 每项 manual 的可执行步骤

| ID | 准备与操作 | 明确通过条件 | 恢复 |
| --- | --- | --- | --- |
| M-001 | 默认场景Play，不点任务；从角色正面/侧面查看CR与CM | Cashier站CR员工侧而非队列侧；Barista站CM正面；两员工朝向设备、无整身嵌入 | 继续M-002或重新Play |
| M-002 | 1x按“收银服务”，完成后按“制作咖啡”；观察状态与remaining game seconds | 两者都可见working；Cashier计满2游戏秒服务成功，Barista计满3游戏秒才开始送杯；无中途被Hold覆盖 | 等待返回Idle |
| M-003 | 从0杯触发Barista，完整观察CM→Pick-up→CM | 去程手杯跟随右手、上身稳定、腿走路；停下不原地walk；正确到位立即放杯、手杯消失，空手回到CM | 重新Play |
| M-004 | 同一module做5杯，每次等Barista回CM并Idle再触发 | 可见杯数依次1/2/3/3/3；三个构成三角形；第4/5次仍有完整working/送杯且完成，无额外漂浮/隐藏堆杯 | 重新Play清空视觉 |
| M-005 | 分别在working和送杯时Pause，等待至少2现实秒，再1x；另一次在2x做完整服务/制作；Pause时点任务 | Pause动画/位置/remaining不变且不放杯；恢复继续；2x工作现实时间约减半；Pause点击提示不可开始、不积压 | 返回1x，重新Play |
| M-006 | working中途和送杯中途分别进入Decor，只打开关闭；再做一次移动Preview但Cancel后关闭 | 原任务、剩余计时、手杯/桌面杯保留，继续原流程，没有重新制作或误判取消 | 重新Play |
| M-007 | 先交付一杯；在另一杯working/送杯时进Decor，确认把CM或支撑桌移到安全位置/旋转90°，退出；另测连续两次Confirm | 旧未交付手杯清理；员工身份/岗位仍是原两位，从原位置步行回新站位；已有桌面杯跟随桌子；旧任务不重复放杯 | 重新Play恢复默认布局 |
| M-008 | Decor删除CM或Pick-up并退出，再进入Decor添回；另做一次可确认但阻断工作路径的布局，再修复 | 明确无法工作/路径被挡，不假Idle、不穿实体/瞬移、不无限原地walk；修复后经原恢复入口能回位工作 | 重新Play；无法构造安全堵路时记BLOCKED |
| M-009 | 打开MainCafe；在`P13 Employees`的`EmployeeTaskValidationController`菜单用`P13/Enable Customer Arrivals`启用P12自动进客，等待至少4顾客，触发两任务；用`P13/Let Front Customer Leave`让队首受控离场并观察前移；确认一次装修 | Cashier与队伍使用同CR两侧；顾客绕真实员工；FIFO/身体保护保持；装修只reset顾客、不删员工；Cashier测试成功不自动消费真实顾客 | 用`P13/Disable Customer Arrivals`关闭进客，退出Play |
| M-010 | 检查M-001～M-016运行期间Console；退出Play后再查看 | 没有新增Error/MissingReference/持续异常日志；既有warning单独列，不靠Clear取得干净结果 | 保存必要日志，不改资源 |
| M-011 | Cashier/Barista各自working、送杯、返回时连续点所属按钮3次；另一次同时点两岗位按钮 | 每位只有一个任务，连点显示忙碌；不会突然多出手杯、重复计时或互相取消；两岗位可独立工作 | 等Idle或重新Play |
| M-012 | 用Decor添第二台CR、CM、Pick-up；查看绑定状态；执行一次任务；确认移除原选中设备后重新执行 | Cashier始终与P12选中的CR一致；CM/Pick-up选择稳定且可达；同一任务不换目标；失效后明确重新绑定或Blocked | 重新Play |
| M-013 | 选Hierarchy中的`P13 Employees`，在`EmployeeTaskValidationController`组件菜单使用`P13/Cancel Barista Task`；分别在working、送杯和已放杯返回时调用两次 | 前两者手杯清理、不增加桌面杯；返回阶段已经放杯保留；重复cancel不重复清理，员工实际回位或Blocked | 重新Play；Cashier取消同理 |
| M-014 | 送杯时选员工GameObject，取消Inspector顶部Active勾选，等1秒再勾上；另一轮只停用EmployeeFlowController组件再启用 | 停用立即停止且原位置身体保护保留；手杯清理；启用后从原位置验证回位，无瞬移、旧任务复活或重复员工 | 恢复勾选，重新Play |
| M-015 | 先记录`Project Settings > Editor > Enter Play Mode Settings`；分别在Domain Reload启用/关闭下进入Play、做1杯、退出，再进入Play，各重复2次 | 每次都仅两员工/一套任务HUD/初始0杯；旧session的状态或callback不出现，Console无新异常 | 恢复原Enter Play Mode设置；不把临时设置提交 |
| M-016 | MainCafe空店Play，进入Decor添加CR、CM、Pick-up；添一个多Slot支撑桌并在其中一个module绑定Pick-up，做3杯后移动/旋转桌；在Game view切横屏/竖屏并点任务、时间与装修操作 | 空店不死锁，添齐有效设备才可工作；三杯只在绑定module并随桌移动；横竖屏按钮可点、不遮挡既有UI | 退出Play恢复布局；还原Game view尺寸 |

M-008涉及删除的素材、M-009的现有队首离场入口及M-012的绑定状态显示须由实现者先准备并在Beginner Guide指明位置。Manual helper只在验证Scene/调试组件暴露，不扩展正式经营任务或派单规则。

轻微的杯子视觉穿模按Owner已确认规则接受；working/carry整体观感、挂杯位置和三角形读图仍由Owner亲自观看决定。

## 5. 证据状态与后续计划

- 本文件保留验收需求；56个 E/P 场景的逐项自动覆盖、实际 XML 与失败修复记录见 [Validation Report](../../Phase13_Validation_Report.md)。10项Codex场景代测已PASS，Owner已确认M-001～004及M-006 PASS，M-009/M-016仍`NOT_RUN`，实际入口见 [Beginner Guide](../../Phase13_Beginner_Guide.md)。
- 2026-10-14最终自动证据：完整EditMode **2594P / 0F / 0S**；完整PlayMode **1253P / 0F / 5项旧opt-in SKIP**。其中P13为103项Edit和32项Play全部PASS；技术审查通过不替代Owner manual或授权P14。
- 实施时在同一 approved plan 记录可信RED→GREEN、focused和直接regression，避免每Task另写closeout。
- Phase末记录完整EditMode/PlayMode counts、FAIL/SKIP、真实Scene与Owner结果；不得将historical CI或Blender只读采样写为本阶段PASS。
- Player/device、cloud CI、commit/push/PR/merge本轮均 `NOT_RUN`，需要相应阶段的单独授权与证据。
