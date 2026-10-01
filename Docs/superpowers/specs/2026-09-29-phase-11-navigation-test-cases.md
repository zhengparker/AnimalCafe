# Phase 11 — Navigation Test Cases

> 状态：Phase 11 开发与验收完成（现有 `codex/phase-9-order-domain` 分支）；Owner 确认 **M-001～M-011 全部 PASS**。Owner 已授权 review 后 push 和创建 PR；最新 P9–P11 review、完整回归与交付状态统一见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。尚未 merge main，P12 未开始。先前失败截图和代理 GUI 拖拽未完成记录保留为历史。
> 日期：2026-09-29，状态更新 2026-09-30。此前逐次 XML 结果、完整与 focused 区分、SKIP 和限制见 [验证报告](../../Phase11_Validation_Report.md)；操作见 [Beginner Guide](../../Phase11_Beginner_Guide.md)。此前 walk 1.5 试调 focused Edit 8 / Play 8、crowd8 / detour 修复 focused Edit 14 / 直接 Play 66 PASS + 1 opt-in SKIP；预览/teardown focused Play 14 PASS / 0 FAIL / 0 SKIP。更早的原版完整 PlayMode 为 1102 PASS / 0 FAIL / 5 SKIP、指定 Integration 为 27 PASS / 0 FAIL / 1 SKIP；完整 Edit 原始 2432 PASS / 1 FAIL，唯一失败由精确补测解决。各次结果分别保留，不合计为最新完整回归；原版结果不证明 1.5 幅度版。

## 1. 验收原则

- 必须遵守：不瞬移、不整个人互相穿过、不穿墙或穿过实体家具、从当前位置 retry 一次。
- 自动化验证 collision proxy 的安全；Owner 验证 Mesh 表现。到达、转身、避让时短暂轻微边缘穿插可以接受，不再要求全部动画 Mesh 零交叠。
- “没撞到”不足以通过正常路径测试；正常场景必须实际到达，不能所有角色都停住。
- 堵死场景允许明确 Failed；不能把 Failed 当作到达。
- 测试对每段批准的移动区间和端点做独立检查，不只检查每帧最终位置，不只复用 production guard 的返回值。
- 时间均为 game-seconds；Pause 与 2x 另做 wall-clock 比较。
- proxy 数值比较容差 0.001 m，不能拿它替代视觉判断；持续明显嵌入或整个人互穿仍失败。
- 不以强制改 Transform 的方式在正常测试中修复 actor；专门的非法外部位置测试例外，并必须报告失败。

## 2. 原版只读资源检查

| ID | 检查 | 结果 |
|---|---|---|
| R-001 | Shiba / Westie 默认 walk 源文件存在且 Blender 可打开 | PASS |
| R-002 | 两份文件 Armature 当前 Action 为 `AC_Core_Walk_Default` | PASS |
| R-003 | 两份文件包含 packed base-color image | PASS，仅说明源文件内嵌图片 |
| R-004 | 原尺寸与身体范围采样 | PASS，已扩充为 81 个姿势（每 0.25 帧）；约 1.30 m 高、最大水平半径约 0.533 m。仅通过测量，不是通行 PASS |
| R-005 | FBX、Unity Import、Material、Prefab、Root Motion、loop | 原版自动 PASS：最终 NavigationAssetTests、validator、动画/runtime cycle 用例；1.5 幅度版 focused EditMode 8 PASS / PlayMode 8 PASS，证据见验证报告 |
| R-006 | 模型外观与移动手感 Owner 验收 | M-001、M-002（含 1.5 幅度版）、M-011 均获 Owner PASS |
| R-007 | 既有尺寸批准记录 | FOUND：P3 已批准 1.30 m 视觉基线；明确未验收 NavMesh / 通道 / Anchor |
| R-008 | 当前正式 Counter Collider 与相邻 Anchor 静态对照 | REVIEWED：0.72 m 柜台高度、相邻中心距边缘 0.50 m；正面源几何有余量，转身需额外空间。Unity 原 Counter Anchor 到达/转向自动 PASS（最终完整 Play 与 Integration）；M-011 已获 Owner PASS |

早期源文件检查通过 Blender background、`--disable-autoexec` 完成，当时未保存或导出源文件；上表另列原版 Unity 实施验证。2026-09-30 两份母版在备份后只试调 8 条上下肢旋转 curve，frame 1～21 / FPS 不变；模型尺寸和行走速度目标不变。备份见 `outputs/phase11/walk-amplitude-20260930/source-backup/`；新版本导出与 Unity 验证尚待记录。Shiba 30 fps / scene 1–20，Westie 24 fps / scene 1–21；两者 Action 均为 1–21。

## 3. EditMode / 可控逻辑测试

| ID | 情形 | 必须结果 |
|---|---|---|
| E-001 | 合法请求与完整路径 | 创建唯一 handle，保存起点、目标和 layout revision |
| E-002 | NaN、Infinity、负半径、零速度、空 actor、已失效 actor | 拒绝；不改变世界状态 |
| E-003 | 同 actor 再次 MoveTo | Busy；旧请求不被悄悄替换 |
| E-004 | 初始目标不可达 / partial path / 无路径 | 明确失败，不启动行走 |
| E-005 | 起点不在 NavMesh | 失败；从未 Warp 或改到采样点 |
| E-006 | 第一次卡住 | 仅一次 retry，从此刻真实位置重算 |
| E-007 | 再次卡住，无 recovery point | Failed；无第三次原目标请求 |
| E-008 | 配置 recovery point | 只启动一次 walk-only recovery；成功结果 Recovered，原任务未完成 |
| E-009 | recovery 无路径、卡住或超时 | Failed，保留原原因和 recovery 原因；无递归恢复 |
| E-010 | 左右抖动 / 局部进展 | 行程总时限仍到期，不能无限续命 |
| E-011 | Pause、恢复、2x | Pause 时间不消耗；2x 无重复倍乘；长帧运动限步不能截掉超时累计 |
| E-012 | 长帧超过步数上限 | 截掉超额预算，不能一次大位移穿过障碍 |
| E-013 | Arrived / Failed 后 Cancel、重复 disable/destroy | 每个请求 callback 恰好一次 |
| E-014 | callback 重入提交请求或抛异常 | 旧请求先结束；不重复回调、不破坏新请求或其他角色更新 |
| E-015 | Layout revision 变化，迟到 path 结果返回 | 旧结果不再应用，旧请求 LayoutChanged 一次 |
| E-016 | 正在移动与停止的 actor 争用同一位置 | 停止角色仍有占地；不能强行挤入 |
| E-017 | 同步交叉/交换位置 | 连续区间互斥；终点合法也不能中途穿过 |
| E-018 | Layout Preview / cancel / 外观修改 | 不改变正式 Navigation revision 或请求状态 |
| E-019 | 合法实体 Confirm / Store | Navigation dirty，旧 path 失效；readiness 重算次数不冒充几何变化 |
| E-020 | 同一 Anchor 给两个请求 | 最多一个占据，另一个等待/超时；不引入 P10 业务容量写入 |

## 4. 真实 NavMesh / PlayMode

| ID | 情形 | 必须结果 |
|---|---|---|
| P-001 | Shiba、Westie 各自走直线和绕家具 | 规定时间内到达；proxy 不穿障碍、不瞬移，轻微视觉边缘穿插留待人工验收 |
| P-002 | 墙背后目标、同墙两侧近距离采样 | 不因 SamplePosition 成功而跨墙到达 |
| P-003 | 部分路径、断开的 NavMesh、pathPending | 分别明确失败 / 有限等待，不假到达 |
| P-004 | 有空间的迎面相遇、交叉通行 | 角色实际绕行或轮流通过，双方到达；collision proxy 及其扫掠不相交 |
| P-005 | 无侧方空间的窄路迎面相遇 | 不互穿；允许等待，retry 后有限时间结束，不永久挂起请求 |
| P-006 | 同向跟随、前方角色停下 | 保持身体间距；停止角色不被推走 |
| P-007 | 8 个 Shiba/Westie 实例汇向狭窄区域 | 全程安全；请求在期限内到达或明确失败，不宣称任意规模性能通过 |
| P-008 | 原尺寸 + 0.45 m 移动半径通过 1.00 m 直通道，对照 0.85 m 通道 | 正例必须能到达，负例拒绝；bake 与 guard 使用相同半径。不能临时关闭碰撞或改尺寸使单个测试通过 |
| P-009 | 高速测试 / 长帧 / 2x | 无薄墙 tunneling、无角色交换位置 |
| P-010 | Pause 期间存在活动请求 | 位置、walk、超时均不推进；恢复后继续 |
| P-011 | 1x 与 2x 同一路线 | 2x wall-clock 到达约为 1x 一半，容许起停差异；不是四倍 |
| P-012 | 人为将 actor 放出 NavMesh / 初始重叠 | 明确诊断；不自动挪回或推开，不报告正常到达 |
| P-013 | walk-only recovery 可达 / 被堵 | 实际连续走到安全点并 Recovered，或 Failed；不能原目标 Arrived |
| P-014 | 到达、等待和失败后的 walk | 无原地持续走路；静止姿势无根位置跳变 |
| P-015 | FBX 的单位、轴向、脚底、骨骼与 loop seam | 高度符合批准尺寸；没有翻转、地面漂浮或循环 root 漂移 |
| P-016 | 所有 walk 帧及插值姿势、任意朝向 | proxy 不随摆臂缩放，不要求 Mesh 完全包在 proxy 中；输出代表画面供 Owner 判断边缘穿插，明显整体穿模不接受 |
| P-017 | 取消、角色 component / GameObject disable、场景卸载、角色销毁 | 结果一次，回调前停止 driver / presenter；disabled 不移动、不转向、不接受重入请求。仍存在的身体保留 proxy 占用，重新启用从原位置验证新请求；安全清理后其他角色继续有效工作 |
| P-018 | 正在运动的模拟位置领先真实位置 | guard 阻挡后实际角色停下；Agent 不把下一帧角色拉到模拟位置 |
| P-019 | 真实 1×1 / 1×3 Counter 的原 Anchor、停步和转向 | 保留模型原尺寸，优先原 Anchor，不强制向外偏移；proxy 安全、到达位置/速度/facing 达标；轻微边缘穿插由 Owner 判断 |
| P-020 | 必要的 sampling 超出 0.15 m、换到柜台对侧或位置被占 | 拒绝 / 等待后失败，不挪动 actor 或改变 Anchor source；无障碍正例必须实际到达 |
| P-021 | 平面障碍、Trigger、Preview、动态角色混在 bake 附近 | bake 只包含正式地面/墙/实体家具；角色和 Preview 不造成假堵路，实体家具不能漏算 |
| P-022 | 两个 world 或多次建/卸载验证场景 | 只销毁本 world 的 NavMeshData，旧 revision 无法发布，退出后无 static 注册残留 |
| P-023 | 比到达距离更近但隔墙；速度为零但 pathPending/blocked；Facing 未达到 | 均不假报 Arrived；位置、完整路径、实际速度和朝向条件全部成立才回调 |

## 5. 装修与时间 integration

在独立验证场景使用真实 CafeLayoutRuntime、DecorationModeController、Time UI 和家具资源，不能仅用 mock 声称集成通过。

I-005～007 的实际用例为 `I005_I006_I007_RealFurnitureConfirmBlocksCoffeeStationThenRepairRestoresFastAndFreshMove`：Counter Preview 在 Grid `(4,4)` Confirm，阻挡 Coffee `(4,3)` 的 Employee station；同一 Counter 移至 `(0,6)` Confirm 后恢复原 Fast，Westie_8 从未挪动的起点到原 Coffee Anchor 并 Arrived。旧 root-drift 用例仅证明配置失效恢复；实际鼠标/触屏操作仍属 M-009。

| ID | 操作 | 必须结果 |
|---|---|---|
| I-001 | 移动中进入装修 | 先暂停；NPC 不在家具编辑时继续走 |
| I-002 | 把实体家具放到暂停角色 proxy 位置并 Confirm | 在正式 mutation 前拒绝、保留 Preview；所有 floor/rotated/mounted/实体 wall-decoration 入口统一保护，不先提交再回滚 |
| I-003 | Preview 穿过角色后 Cancel | 正式家具与 Navigation 无变化，角色不被推开 |
| I-004 | 合法移动/旋转/收起家具 | 旧路径失效；退出前完成 Collider 与 NavMesh 更新 |
| I-005 | 堵住必需服务路线后退出 | 可保留装修，但恢复营业受限；准确指出无效位置 |
| I-006 | I-005 后点击 1x/2x、重复退出 | 不能绕过 readiness gate；不出现一帧运动 |
| I-007 | 修复布局再退出 | readiness 通过后恢复原速度；从当前位置新请求可达 |
| I-008 | 重建失败、disable cleanup、场景关闭 | 不意外恢复无效移动；句柄与 callback 安全收尾 |
| I-009 | 调整纯外观、取消 Preview | 不无故重建或中断合法移动 |
| I-010 | 未配置 Navigation 的旧验证场景 | 保留旧阶段行为，无 missing reference 或额外暂停 |
| I-011 | 同一格 Pick-up 双方 Anchor、站位靠近实体 | 身体范围冲突可诊断；不改 Anchor source、不允许重叠 |
| I-012 | 一个非必需 station 不可达，但必要服务组合完整；反例为必要组合缺失或服务间断路 | 前者只标记该站不可用，后者阻止营业；沿用 P8 局部可用性 |
| I-013 | 两个 resume block 交错获取/释放、异常 disable、反复点击 2x | 单个释放不能解除另一个 block；不出现一帧错误恢复；block 消失也不自行开跑 |

## 6. Owner 手动验收

每项均需记录 PASS / FAIL / BLOCKED，并标明 Owner 手动或 Codex 自动证据。Owner 已确认 **M-001～M-011 全部 PASS**，无需重复手测；M-011 的 Test Runner 入口固定模拟帧、结束自动卸载，限制仍需保留，自动绿勾本身不能代替 Owner 反馈。此前 focused Play 14 PASS / 0 FAIL / 0 SKIP、失败截图和代理 GUI 拖拽未完成记录均保留为历史证据。最新自动回归及已授权的 push／PR 交付状态见 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)；尚未 merge main，P12 未开始。

| ID | Owner 观察内容 |
|---|---|
| M-001 | Shiba / Westie 保留已建立的约 1.30 m 比例，Unity 导入没有改变尺寸或丢失材质 |
| M-002 | walk 自然、速度可接受，停下不持续走，不突然跳到其他位置 |
| M-003 | 两只角色宽路相遇会避让，交叉时有顺序，不整个人互穿；轻微边缘穿插可接受 |
| M-004 | 窄路受阻时能看懂等待及最终失败，不穿过对方 |
| M-005 | 到达/转身/避让时的边缘穿插轻微短暂；没有明显持续嵌入墙/柜台或整个人穿过 |
| M-006 | Pause / 1x / 2x 的动作和位移一致 |
| M-007 | 从当前位置 retry 与走向 recovery point，全程没有瞬移 |
| M-008 | 装修不能把家具确认到角色身上；取消不影响正式布局 |
| M-009 | 堵路后不能恢复营业，修复后能恢复；状态信息清楚 |
| M-010 | MainCafe 原有装修、相机拖拽、选择和时间控制正常；Console 无新增 error |
| M-011 | 保留原尺寸时能走到柜台前并停下/转向，不为尾巴/摆臂零穿模而强制站远；站位仍清楚合理 |

## 7. 回归与报告

- Task focused：生命周期 / movement guard / animation / layout integration 各自新增测试及直接相关旧测试。
- 直接回归重点：Phase 8 Anchor/readiness、P8R 家具确认与取消、UiPauseCoordinator、GameTimeService、时间按钮与相机输入。
- Phase 收尾：按项目既有流程运行完整 EditMode / PlayMode、真实场景 integration；同一时间一个 Unity test process。
- 设计准备阶段仅修改文档，当时无需 Unity rerun。实施与后续修复保留 XML/log、测试计数、RED→GREEN 摘要与少量代表截图；最新完整回归统一记录于 [合并前 Review](../../Phase9_Phase11_Merge_Review.md)。
- 数字结果和 Owner 体验判断分开；任一必要人工验收未完成，Phase 11 仍未完成。
