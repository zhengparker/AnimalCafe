# Phase 10 容量调试窗口：新手操作指南

本窗口只模拟容量规则。它不会生成顾客、安排真实队伍、连接 MainCafe 的营业流程，也不会自动替你占用或归还名额。窗口顶部写着「P10 Debug / 临时容量模拟」。

## 打开窗口

1. 用 Unity `6000.5.5f1` 打开 `E:\Unity\Project\AnimalCafe\.worktrees\phase-9-order-domain`，等脚本编译完成。
2. 选 **Window > AnimalCafe > Phase 10 Capacity Debug**。建议同时打开 Console，并打开已保存的 MainCafe Scene 做人工检查；测试前先处理自己尚未保存的工作。
3. 每个独立例子先点 **Reset Session**。关闭重开或进入、退出 Play Mode 也会清掉本窗口临时数据。

初始值是 **Floor cells = 64、Owner ID = V1、Token ID = 1**，三个 Kind 全部勾选。已应用的 64 格对应 **TotalCustomers 16 / CounterQueue 8 / PickUp 16**。修改 Floor 输入不会立即改容量；点 **Apply Floor** 后才会更新「Applied floor cells」和表格。负数会在 **Last result** 显示 `InvalidFloorCellCount`。

## 六个按钮怎么用

| 按钮 | 行为 |
|---|---|
| Apply Floor | 将输入的 Floor cells 应用到当前临时 service；已有 token 保留。 |
| Reserve Admission | 为当前 Owner ID 一次申请 TotalCustomers、CounterQueue、PickUp 各一个名额；全成或全败。 |
| Reserve Selected | 只申请勾选的 Kind，方便构造某一种名额已满的情况；全不选会显示 `InvalidKinds`。 |
| Occupy | 把当前 Owner ID 的 Token ID 从 Reserved 改为 Occupied。 |
| Release | 当前 Owner ID 明确离开相应位置后，把 token 改为 Released；同 owner 再次 Release 同一 token 可重复成功。 |
| Reset Session | 清除所有 token、结果和输入，重新从 Floor 64、Token ID 1 开始。 |

每次 Reserve 后，先在 **All tokens** 找到 token 的数字 ID、owner、Kind、State，再把目标 ID 填进 **Token ID**。窗口只会查当前 session 签发的 token；随便输入一个不存在的 ID 会显示 `InvalidToken`。Owner ID 不匹配会显示 `WrongOwner`。窗口不替你伪造 token，也不自动推断顾客的位置。

**Capacities** 表逐项显示 `Limit / Reserved / Occupied / Used / Available / OverCapacity`。`Used = Reserved + Occupied`。缩容后可能暂时 `Used > Limit`，已有预约仍有效，但新预约会被拒绝。**All tokens** 会保留 Released 历史；**Last result** 只是最近一次按钮结果，判断当前状态请看每次重新查询的表格。`CanAdmit` 表示三项目前都有可用名额、没有超额；它不能保证某个 Owner ID 的下一次申请一定成功。

例如，Reset 后输入 Floor 8 并 Apply，三个上限变成 **2 / 1 / 2**。用 V1 点 Reserve Admission，会拿到 #1 TotalCustomers、#2 CounterQueue、#3 PickUp。想模拟「离开柜台」，输入 Token ID 2，保持 V1，点 Release；取餐与离开 PickUp、实际离店分别需要显式 Release #3 和 #1。正确取餐对应 P9 Order 的 Completed，也不会在这个工具里自动释放名额。

Pause、1x、2x 时按钮仍可用于诊断。等待和扩容都不会自动生成顾客、自动补满或自动释放。P12 才设计 random arrival interval、真实队列、位置可达性和容量调用时机。P28 若把「区域解锁」与「铺设 Floor」分开，Floor 来源需要重新设计；当前容量只计算已确认 Interior region 的有效格 union，家具或装修预览不会扣减 Floor cells。这个窗口输入 Floor 只是实验数字，并未接到 MainCafe Scene。

## 本次新增和修改的文件

| 位置 | 用途 |
|---|---|
| `Assets/Scripts/Capacity/` | 容量公式、已铺室内格数来源、原子预留、占用/释放、缩容保护。 |
| `Assets/Editor/Phase10/` | 本指南中的临时调试窗口和独立 session。 |
| `Assets/Tests/EditMode/Phase10/`、`Assets/Tests/PlayMode/Phase10/` | 自动检查容量规则、错误操作、P9 边界和跨帧稳定性。 |
| `Docs/superpowers/specs/`、`Docs/superpowers/plans/` 的 P10 文档 | 已批准设计、原测试用例和实际实施记录。 |
| Project Design、Development Roadmap | 同步本次已确认规则及 P12、P28 的后续接口边界。 |

所有改动都在原 P9 checkout 的 `codex/phase-9-order-domain` 分支上。没有存档迁移，也没有修改 P9 OrderService 或现有 Scene/Prefab。

## 验收与证据

人工操作请直接按[原 Phase 10 测试用例](superpowers/specs/2026-09-29-phase-10-capacity-reservation-test-cases.md)中的 **P10-M-001…010** 执行和记录，不在本指南创建第二份结果表。M-008 要在 Reload Domain 开、关两种设置下实际进出 Play，并观察有输入焦点的字段；结束后恢复原 Editor 设置。M-009 要亲眼比较 MainCafe 的 Camera 拖动、缩放、时间控制和 Decor 操作。自动测试或直接调用窗口回调不能替代这些 GUI 证据。

2026-09-29 自动验证结果：P10 EditMode **108/108 PASS**，P10 PlayMode **3/3 PASS**，P9 直接回归 EditMode **132 PASS**、PlayMode **3 PASS**。独立 Engineering source review 和 QA 自动部分均已通过，审查范围内没有未关闭的 Critical / Important / Minor 问题。

完整回归的 EditMode 有效覆盖为 **2363 PASS、0 FAIL、0 未解决 SKIP**：原始报告是 2008 PASS / 355 SKIP，这 355 项因旧测试的 Scene 保护条件跳过，随后在三个独立进程中按测试全名逐项补跑通过，原始报告保留。完整 PlayMode 为 **1042 PASS、0 FAIL、4 SKIP**；四项均是需要主动开启的截图或布局导出测试，不计为 PASS。原始 XML、补跑对应关系和最终 QA 状态见[原测试用例的最新执行快照](superpowers/specs/2026-09-29-phase-10-capacity-reservation-test-cases.md#9-最新执行快照2026-09-29)。

**2026-09-29 GUI验收：M-001…010全部通过，final overall Engineering／QA review均PASS。** Owner授权Codex操作真实Unity窗口，原始截图经独立审查；工具未能确认的镜头拖动已由Owner用真实鼠标补验，明确回复打开P10窗口前后“两种情况下都能移动”。Owner收到P10范围说明后接受本轮委托验收，并授权整体review后commit/push到P9同一分支。输入焦点、Reload Domain ON/OFF、缩放、时间控制和Decor均已实际检查，无需重做。P10尚未merge至main，正式Roadmap closeout留待之后。

已确认的运行边界：目前按小型布局、单线程和临时session设计；巨大地图、长期不Reset的海量历史没有性能验收。具体范围裁决及测试过程偏差见[实施计划](superpowers/plans/2026-09-29-phase-10-capacity-reservation.md)。Codex GUI结束后已恢复1x、退出Play、关闭窗口；Owner补验回复后，场景和项目设置仍与操作前一致。本次提交范围仅P10代码、测试及文档；XML、截图和scratch review留在本地。
