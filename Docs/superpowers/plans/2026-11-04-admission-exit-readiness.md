# Admission Exit Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Root sole Assets/Unity writer; one independent review at end. No Git writes.

**Goal:** 尚未出生的来客首段会挡已取餐客人的固定出口时，有限暂缓，保留原身份与累计预算。
**Architecture:** 每个原 CounterAdmissionAnchor 绑定一个 internal sealed TrafficAdmissionReadiness，只读取当前unborn身份及合法collected actual-exit keys。Core读取这些原Entry的出口目标，以缓存的实际首段作几何检查；只影响尚未获权的admission准备，不增Frozen依赖。原流程重新证明后才可出生/共同提交。
**Tech Stack:** Unity6000.5.5f1, C#, NUnit/EditMode, Native PlayMode.
**Spec:** outputs/phase15/photo-pickup-wait-20261103/admission-exit-hold-proposal.md（Owner 2026-11-04“继续开发”批准）。

## Global Constraints
- 不改NavMesh/碰撞半径/付款/容量/营业时间；不保存Scene，无serialized/save migration。
- 原同一anchor/key/FirstFrame/query/Frozen/有限累计等待不重置，不自动重试Terminal。
- 只用实际首腿，不扩大为整条future suffix或AnyExiting全局禁行。
- 此任务直接回归；不启动完整Edit/Play/长GUI。保留修前两项既有FAIL。

## Review Focus
- 已Bound但未实际离店的collected意图仍保护出口：callback保留到真实Exit完成，Native检查gate生命周期。
- Prepared缓存/重入提交可绕过新gate：refresh和commit同样检查，模型Prepared→hold、旧permit反例。
- hold前无Frozen时预算被普通WAIT重置：一次AdmissionWaitStarted与WaitOrFail/Tick共用WaitRemaining，模型交替等待。
- hold期间无Actor/claim但仍计活动work：模型未占权且HasPendingWork，Closing退休/epoch/reset反例。
- stale callback/替换descriptor错误释放：descriptor一次绑定、回调前后原Entry/snapshot guard，模型identity/exception/reentry拒绝。

### Task 1: typed readiness and finite Core gate
**Files:** ServiceTrafficContracts.cs, ServiceTrafficCoordinator.cs, ServiceTrafficCoordinatorTests.cs.
**Interfaces:** internal sealed TrafficAdmissionReadiness(TrafficIntentKey,Func<bool>,Func<IReadOnlyList<TrafficIntentKey>>); internal bool TrySetAdmissionReadiness(TrafficDecision,TrafficAdmissionReadiness).
- [x] 新模型先RED：真实原API输入/解析几何，不私改Entry；反射调用internal port。
- [x] 加最小typed descriptor及一次绑定。Core在完整原primary proof之后、发Prepared Grant之前检查第一LegEnds及精确端点。
- [x] 入店已Prepared刷新/最终TryCommit再检；hold无Active/Prepared/CurrentPath/Journey权利，只Deferred状态，不新WAIT依赖。
- [x] 持有期间Ready只重读当前descriptor与真实exitEntry并用cached firstleg几何；解禁仅Queued原来源，正常重证。
- [x] AdmissionWaitStarted首次沿原FiniteLimit/waitSeconds初始化一次；若已有Frozen沿原WaitRemaining。Tick和首次普通WaitOrFail共用剩余额，Pause不扣，耗尽Terminal。
- [x] 跑新模型GREEN，再跑当前完整Core class一次，保旧636保护。

### Task 2: CFC integration and same Native reproduction
**Files:** CustomerFlowController.cs, CafeServiceMovementTests.cs; approved spec/plan/live report/roadmap.
- [x] CounterAdmissionAnchor持有一个只读view；IsCurrent验证原anchor/noActor/noQueue/noCapacity，pendingExits只读真实collected Exiting visit及已存在actual-exit key。
- [x] PrepareAdmissionAnchor首次Submit之后一次绑定view；所有普通refresh保同descriptor。合法离店已Bound仍保留key直到真实移除。
- [x] 模型/Native结果绑定当前source。原截图case保至少4pickup+全部真实Exit/staffIdle/zeroCapacity断言；增加gate真实观察，禁止工具恢复。
- [x] 运行原截图一次；若失败按原拒绝证据定位，不盲目重复。成功后仅P15Admission16+BeforeReady1短回归。
- [x] independent Engineering review检查上述风险；重要问题一轮修正并相应RED/GREEN。
- [x] 更新report/roadmap/ledger实际counts，P15保持IN_PROGRESS，不commit。

**Ruling:** 首次hold前尚无Frozen时，原request.FiniteSeconds与Core waitSeconds已是原有限来源，允许首次初始化一次共享WaitRemaining；随后任何hold/普通WAIT都取原剩余值，不分配新额。代价：密集拥堵可能在原30游戏秒后明确失败，保既定安全规则。

**完成记录：** 最终Core654/654、Native18/18 PASS，细节见 photo-pickup-wait-20261103/readiness-final-results.json。两次review问题有真实RED后修复；Nav生命周期同样RED→GREEN。一次补丁校验后错误启动旧源码的多余Core654（653P/1已知NavFAIL）保留原结果，最终Native当时未启动；修正脚本退出检查后，在74F9冻结源重新完成两种模式。无Git提交/完整suite/Scene保存。
