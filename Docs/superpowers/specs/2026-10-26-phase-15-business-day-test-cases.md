# Phase 15 — Business Day Lifecycle Test Cases

> 日期：2026-10-26。状态：Owner 已确认 D1／D2／D3，并批准用 sub-agent 按配套 [Implementation Plan](../plans/2026-10-26-phase-15-business-day.md) 开工；按 [P15 Design](2026-10-26-phase-15-business-day-design.md) 执行。测试设计批准不等于实际测试通过。
> 2026-10-27当前：原批准第4节M01–M04均为Validation Scene手测，本轮Agent补测PASS；结合既有M05出口分支／M06／M07正常UI，原七项均有Agent通过记录。Guide扩展的Main正常装修、直接完整付款集合仍未完成，Debugger异常UNKNOWN；详见[Agent报告](../../Phase15_Agent_Playtest_Report.md)及[Validation Report第16节](../../Phase15_Validation_Report.md)。当前源定向Play122 PASS／0 FAIL／1原有SKIP，旧全量Edit2832／Play1445＋5 SKIP保持修复前source边界。Owner观感／8h／device／CI／VCS未验收，不宣告Phase closure；下方批准测试要求不变。

> 最新展示修正：Main P5玩家UI／Validation黑色诊断分离已验证，Edit59 PASS、直接Play122 PASS／1原有SKIP／0FAIL，独立review及限定真实UI通过。旧manual与完整suite保持source边界；见[Validation Report第17节](../../Phase15_Validation_Report.md#17-main-玩家界面与-validation-诊断分离2026-10-27)。批准测试要求未改。

## 1. 这个文档是做什么的

验证咖啡厅会自动营业、打烊后把已有客人服务完，再休息并自动开始下一轮。例如咖啡已被客人取走，但客人还没走出店门时，不能提前开始休息。

## 2. Test Policy

- D1 现实营业28800秒／Rest7200秒，Pause／2x只影响Scene；D2真实排空后Rest；D3保留旧清场并记录装修中断，均已获Owner明确确认。
- 纯逻辑使用注入时间；integration 使用同一正式业务路径的短时配置，不另写伪订单／伪容量。
- 同一时间只运行一个 Unity test process；Task focused，Phase 末集中完整 regression。
- 保留失败历史；测试 fixture／compiler failure 不冒充产品 RED。当前表仅定义计划，不记录虚构 PASS。

## 3. Normal / Boundary / Failure / Recovery

| ID | 场景 | 预期结果／需证明的事实 |
|---|---|---|
| P15-E01 | 初次配置有效 cafe | Day 1／Open 自动开始；没有 Start 点击；原 readiness／Pause gate 仍有效 |
| P15-E02 | 营业到期前、刚好到期、超过边界 | 边界只触发一次 Closing，新接待同帧关闭 |
| P15-E03 | 同帧自动来客／直接 TrySpawn | Closing／Rest 两种入口都拒绝；特别覆盖截止已到但 driver 尚未运行，缓存仍 true 的直接请求；不增加 reservation／付款 |
| P15-E04 | Closing 中尚未付款的已接待者 | 正常排队、收银与创建一次订单，不因打烊被跳过 |
| P15-E05 | Closing 中 Waiting／Working／Prepared／Ready | 保留原 Order／FIFO／任务／商品，继续正常流程 |
| P15-E06 | 已领取商品、仍在离店 | Completed 不等于排空；保留原 Actor、手杯与容量直到实际释放 |
| P15-E07 | 最后一位已走但 Barista Returning | 仍 Closing，实际返岗后才可 Rest |
| P15-E08 | 显示杯为空但逻辑 delivery 未空 | 不判排空；不能用视觉事实覆盖业务事实 |
| P15-E09 | 历史 Completed Orders／Paid FIFO 非空 | 所有 live work 已排空时可以 Rest；历史不清空、不重复日结 |
| P15-E10 | 三项容量的 Reserved／Occupied 任一非零 | 不 Rest；不是只看 Total 或 live object count |
| P15-E11 | 顾客／员工 disabled 或真实 Blocked | 不假装离店／返岗；明确原因，按 D2 处理 |
| P15-E12 | 缺配置／binding／capacity ledger | NotReady，不把 null／缺员工当成空店 |
| P15-E13 | callback 尚未消费／恢复集合非空 | 不 Rest；消费或显式恢复后再次验证 |
| P15-E14 | 重复 Frame／callback／Configure／enable | 不重复 transition、增加 Day、收费或创建任务 |
| P15-E15 | Rest 到期自动开下一轮 | 无 Next Day／Report 点击，Day 只加一次；旧员工身份保留 |
| P15-E16 | Rest 内计划新客或手动 TrySpawn | 不接待；查看 UI 不改变时间或资源 |
| P15-E17 | 主动 Pause 与 2x | Scene 保持旧暂停／缩放；周期独立推进，Rest两小时不受Scene速度影响 |
| P15-E18 | 装修自动 Pause，Cancel Preview 后 Done | 保留旧无修改行为；周期仍推进，未记录中断，不自动解除玩家 Pause |
| P15-E19 | Closing 中确认真实装修修改 | 保留P14清场；清除前按version记录LayoutInterrupted，重复不累计，不计为正常全部服务完成 |
| P15-E20 | 路径修复后 ResumeBlockedFlow | 沿既有有界恢复完成原服务，不重复付款／制作／交付；从实际排空时才开始两小时Rest |
| P15-E21 | Rest 到期但布局无效／主动 Pause | 自动周期不绕过 readiness／Pause；状态和原因可理解 |
| P15-E22 | 负数、NaN、Infinity 时间／非正时长 | 清晰拒绝且原状态不变；零增量不产生重复副作用 |
| P15-E23 | 运行中大时间差／长帧 | 不把大 delta 注入 NPC补发队列；不跳过真实 Closing 排空 |
| P15-E24 | 缺少 P15 component 的 P14 场景 | 原自动来客、连续服务、手动来客与时间行为不改变 |
| P15-E25 | required policy 在加载前缺失、未注册、disabled、destroyed | 新接待 fail closed；覆盖组件从未执行 OnEnable／OnDisable 的情况，不靠缺失者关门；旧 Scene opt-in=false 保持原行为 |
| P15-E26 | 日边界 getter／UI refresh 重入 | 查询只读，不收费、不改变 Day、不清历史 |
| P15-E27 | 无 runtime economy／Save | 不生成伪 Coins／Revenue，不落地临时存档或假离线报告 |
| P15-E28 | Day／transition 计数极限 | 明确拒绝溢出，不回绕为负数或重新 Day 1 |

## 4. Scene / Owner Manual

| ID | 操作 | 观察与证据 |
|---|---|---|
| P15-M01 | 打开独立 P15 validation Scene 后 Play | 短时验证标记清楚，自动经营，无 Start 按钮 |
| P15-M02 | 等到打烊，观察原先已有顾客 | 不再出现新客；已有顾客真实服务与持杯离店，员工返回 |
| P15-M03 | 等 Rest 再进入下一轮，连续观察两个边界 | 阶段／倒计时／Day 可读，无确认日报或下一天操作 |
| P15-M04 | Open／Closing／Rest 分别 Pause 与 2x | Scene动作暂停／加速，周期照常推进；没有自动解除主动 Pause |
| P15-M05 | 制造已批准的受控阻塞，再修复恢复 | 原业务事实保留、原因可读，处理符合 D2，没有瞬移或假完成 |
| P15-M06 | 查看与取消装修；另测批准的真实修改 | 不把装修清场记成正常打烊；没有旧 callback 污染新周期 |
| P15-M07 | 在横／竖窗口查看阶段 UI 与 Console | 无关键裁切、输入穿透或新增未处理 error；Editor 不代表真实 mobile |

## 5. Direct Regression / Phase Closeout

- 新 admission／pending query：P14 Service、Driver、Contract 与实际 continuous loop 的直接相关 tests。
- `TrySpawn` policy 与 serialized opt-in：旧 P12／P14 直接来客、自动来客、重复注册／解绑、新 owner 替换与加载时 provider 缺失。
- 新 employee 返岗 query：P13 Runner／Controller 与真实返回路径；沿原容差，不放宽身体或到达规则。
- Step 接点：P14 Customer／Employee recovery 与 Decoration lifecycle；无 P15 Scene 保持原默认。
- Scene builder：保护 MainCafe 和旧 P14 Scene／prefab；重复执行不得生成重复 owner。
- Phase 收尾才集中完整 EditMode／PlayMode、必要 Scene integration、独立 review 与 Owner manual。
- 真实八小时运行、退出后收益、Android／iOS suspend／resume 和设备 UI 单列；未实际执行保留 `NOT_RUN`。
