# AnimalCafe Phase Development Process

> 状态：Approved Project Process
>
> 生效时间：2026-08-22
> 适用范围：Phase 6 剩余收尾及之后所有 Phase

## 1. 目的

AnimalCafe 默认采用 Phase 1–5 的开发方式：**Task 内小步验证，Phase 末集中完整验收**。

目标是在保留 TDD、regression 和用户手工验收的同时，减少重复启动 Unity、重复 review、重复截图和过重的 evidence 工作。

## 2. 核心原则

- 一个 Phase 解决一个主要风险，并拥有一份 approved design、test cases 和 implementation plan。
- Task 是 Phase 内部的开发步骤，不是独立 release。
- Task 完成时跑 focused tests 和直接相关 regression。
- 完整 regression、跨部门 review、截图和手工验收集中在 Phase 收尾执行一次。
- Critical 和 Important 必须修复；Minor 默认进入 Phase polish backlog，不阻挡后续 Task。
- 自动化不能替代玩家可见功能的 Studio Owner 手工验收。

## 3. Phase 开始

每个 Phase 开始时只完成一轮准备：

1. 读取 Game Design、Roadmap、当前代码与已完成 dependencies。
2. 明确 Goal、Included、Not Included、玩家可见结果和主要风险。
3. 编写一份 Phase design/spec。
4. 编写正常、异常、边界和 recovery test cases。
5. 编写一份 Phase implementation plan。
6. Studio Owner 批准后开始实现。

除非 Phase 本身被正式拆分，不为每个 Task 重复创建独立 brief、spec、plan 和 approval gate。

## 4. Task 开发循环

每个 Task 默认使用：

```text
确认 approved behavior
→ 写 focused test
→ 确认可信 RED
→ 最小实现
→ focused GREEN
→ 直接相关 regression
→ 简短记录变更
→ 进入下一个 Task
```

Task 完成条件：

- focused tests 全部通过；
- 直接相关 regression 全部通过；
- 没有已知 Critical 或 Important；
- 没有越过 approved Phase scope；
- 没有留下会阻止后续 Task 的 Scene、Prefab、data 或 dependency 问题。

Task 阶段默认不执行：

- 全项目或全部旧 Phase regression；
- Engineering、QA、Art/UX 三方完整审查；
- A/B/C 多顺序矩阵；
- 大量多尺寸截图；
- source、XML、log、Scene dependency 的逐文件 hash manifest；
- 独立 Task closeout report；
- docs-only 修改后的 Unity rerun。

## 5. 何时升级 Task 验证

只有出现以下情况，Task 才升级验证：

- 修改 shared architecture、public contract、Save、migration、input ownership 或 Scene transaction；
- 修改已完成 Phase 的核心行为或 serialized data；
- 涉及可能丢失用户数据、损坏 Scene/Prefab 或阻断启动的风险；
- 出现 Critical 或 Important finding；
- Studio Owner 明确要求额外审查。

升级应与风险相称，只增加必要测试和 reviewer，不自动扩大为完整 release audit。

## 6. Phase 最终收尾

整个 Phase 只集中执行一次完整收尾：

1. 跑完整 EditMode 和 PlayMode regression。
2. 跑本 Phase 必需的真实 Scene、Input 或 platform integration tests。
3. Engineering 做一次 source/runtime review。
4. QA 做一次 test coverage、regression 和 manual-readiness review。
5. 只有发生明显视觉变化时才进行一次 Art/UX review。
6. 只生成能证明关键状态的代表截图。
7. Studio Owner 完成一次手工 Play Mode / device acceptance。
8. 修复 Critical 和 Important。
9. 对修复项跑 focused regression；最终再跑一次完整 regression。
10. 更新 Phase report、Beginner Guide 和 Roadmap 状态。

## 7. Review 规则

- **Critical**：必须修复，并由原 reviewer 复核。
- **Important**：必须修复，并由原 reviewer 复核。
- **Minor**：记录到 polish backlog；除非影响可用性、可访问性或 Studio Owner acceptance，否则不阻挡 Phase。
- Re-review 只检查原 finding 和修复影响范围，不重新从头审整个 Phase。
- Reviewer 不应在每轮继续增加与原 scope 无关的非阻断要求。
- 文档拼写、路径、owner 名称或 hash 引用错误，只做 docs-only 修正和静态验证，不运行 Unity。
- 已通过的独立部门不因无关 docs-only 修改重复 review。

## 8. Evidence 规则

普通 Phase 的最终 evidence 只需要：

- 最终测试 XML/log 和准确 counts；
- focused RED → GREEN 的简短摘要；
- 关键 Scene、Prefab 和 integration 状态；
- 必要的代表截图；
- 已知限制；
- Studio Owner 手工验收结果。

以下重型 evidence 默认不需要：

- 每个 Task 的 SHA manifest；
- 所有 XML/log 的逐文件 hash 表；
- 全 source closure 或 Scene-transitive closure；
- 多层 external freeze；
- 每个状态的全部尺寸 before/after；
- 无行为变化时重复运行 order matrix。

只有 release、Save/migration、破坏性 transaction、外部合规或 Studio Owner 明确要求时，才启用重型 evidence。

## 9. Unity 运行控制

- 同一时间只运行一个 Unity test/build process。
- Task 阶段合并相关 focused filters，减少 Editor 启动次数。
- 不因为一个 docs-only 修改重跑 Unity。
- 测试 fixture 或 runner failure 与 production failure 分开记录。
- 没有可信 RED 时不修改 production 来迎合不稳定 fixture。
- 完整 regression 主要在 Phase 收尾运行，而不是每个 Task 后运行。

## 10. Phase 6 过渡决定

从 2026-08-22 起，Phase 6 剩余收尾立即采用本流程：

- 已通过的 Engineering、QA 和 Art/UX gate 不重复执行；
- 不再新增 manifest、hash closure 或 Unity regression；
- 下一步直接进行 Studio Owner 的 M001–M005 手工验收；
- 若手工验收发现问题，只对实际问题做 focused fix、相关 regression 和必要复核；
- Phase 6 完成后按本流程记录最终 evidence，并进入下一 Phase gate。

## 11. 权限与版本控制

- Studio Owner 保留 design、scope、manual acceptance、merge 和 release 决定权。
- Codex 未经明确授权不 commit、push、merge、删除 branch/worktree 或跨 Phase。
- 保留用户无关改动；只修改当前 approved scope 内的文件。
